using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How Elarion's ultimate, Event Horizon, speeds up cooldowns, and how that stacks with the other sources of cooldown
/// acceleration: Highwind Arrow's own (its charges recharge with haste), Chronoshift's 800% cooldown recovery, the cooldown recovery
/// every legendary item gives, and Skylit Grace. Also what Event Horizon's own effects do to the cooldowns of Heartseeker Barrage
/// and Starfall Volley.
/// While Event Horizon is up Elarion has haste from Spirit of Heroism (+30%), and Event Horizon gives him cooldown acceleration equal
/// to his haste (in the sums below H is his haste then).
/// Dev: the timing tests set Event Horizon's effects up directly (Spirit of Heroism and the buff, at the start of the fight)
/// instead of casting it, so that they start with the cooldown under test: the cast takes 0.7s and a 1.5s global cooldown, which
/// would shorten the overlap with the cooldown by about a second. Event Horizon as a cast is in CastTimes.cs, AbilitiesSimple.cs and
/// ResourceEconomy.cs. A cooldown is timed by advancing the fight 10ms at a time until the ability is ready (all charges back for
/// Highwind Arrow), compared within 20ms. The rate of recovery is also read straight from the hero.
/// TODO: the tests of Event Horizon with Chronoshift and with the legendary cooldown recovery (but not those with Highwind Arrow
/// alone or with Event Horizon alone) are from the author's in-game measurements (the Python sim's, dated 8 April 2026): the
/// two effects multiply. The engine adds them into one pool of recovery (Hero.CooldownRate), so those tests are expected to fail.
/// </summary>
public class ElarionEventHorizonCooldown
{
    private const double StepTolerance = 0.02;
    private const double RateTolerance = 1e-9;

    private static StepDriver Fight(double haste = 0, int[]? talents = null, int weaponAbilityId = 0, int[]? legendaries = null, int enemies = 1)
    {
        var f = StepFights.Elarion(haste, enemies, talents, weaponAbilityId: weaponAbilityId, legendaries: legendaries);
        f.ForceProcs(key => key == CelestialShot.ImpetusProcKey ? false : null);
        return f;
    }

    /// <summary>Starts Event Horizon's effects now: Spirit of Heroism and the buff that gives cooldown acceleration.</summary>
    private static void StartEventHorizon(StepDriver f)
    {
        f.Hero.ActivateSpiritOfHeroism(f.Context);
        f.Hero.Auras.Apply(EventHorizon.Buff, f.Now);
    }

    private static double Haste(StepDriver f) => f.Hero.GetHaste(f.Now);

    private static double Rate(StepDriver f, Ability ability) => f.Hero.CooldownRate(ability, f.Now);

    private static double CastEnd(StepDriver f, string ability) =>
        f.Events.Last(e => e.Kind == StepEventKind.CastCompleted && e.Name == ability).Time.TotalSeconds;

    /// <summary>Advances the fight 10ms at a time until every charge of the ability is back; returns that time.</summary>
    private static double WaitUntilFull(StepDriver f, Ability ability)
    {
        var cooldown = f.Hero.CooldownOf(ability);
        while (cooldown.ChargesAt(f.Now) < cooldown.MaxCharges && f.Now.TotalSeconds < 400) f.Wait(0.01);
        return f.Now.TotalSeconds;
    }

    private static void AssertTime(double expected, double actual, string what, double tolerance = StepTolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: expected {expected:F3}, got {actual:F3}");

    /// <summary>The cooldown left, in cooldown seconds: how long it would take at the normal rate of recovery.</summary>
    private static double SecondsLeft(StepDriver f, Ability ability) => f.Hero.GetCooldownRemains(ability.Name, f.Now) * Rate(f, ability);

    /// <summary>
    /// Lets the fight run for <paramref name="seconds"/> 10ms at a time, reading the ability's cooldown at each step: the cooldown
    /// takes the rate of recovery as it is when it is read, so reading it only at the end would apply the last rate to the whole wait.
    /// </summary>
    private static void WaitReading(StepDriver f, Ability ability, double seconds)
    {
        var until = f.Now + SimTime.FromSeconds(seconds);
        var cooldown = f.Hero.CooldownOf(ability);
        while (f.Now < until)
        {
            f.Wait(0.01);
            cooldown.ChargesAt(f.Now);
        }
    }

    // ---- Highwind Arrow: its own acceleration, and Event Horizon's

    /// <summary>
    /// Player: Highwind Arrow's charges recharge with your haste (a 15 second recharge takes 15 / (1 + haste) seconds), which
    /// cancels out in play: faster casts, faster recharges.
    /// Dev: one Highwind Arrow; the rate of recovery is 1 + haste, and the spent charge is back 15 / (1 + haste) after the cast ends.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void Highwind_Arrow_Recharges_In_15_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = Fight(haste);

        f.Cast<HighwindArrow>();
        double castEnd = CastEnd(f, "Highwind Arrow");

        Assert.Equal(1 + haste, Rate(f, f.Ability<HighwindArrow>()), RateTolerance);
        AssertTime(castEnd + 15 / (1 + haste), WaitUntilFull(f, f.Ability<HighwindArrow>()), "Highwind Arrow recharge");
    }

    /// <summary>
    /// Player: Highwind Arrow's own acceleration (equal to your haste) and Event Horizon's (also equal to your haste) add up: during
    /// Event Horizon it recovers at 1 + 2 x haste, and a charge comes back in 15 / (1 + 2 x haste) seconds.
    /// Dev: at 0% and 20% haste, with Event Horizon on (haste H is 30% more); one Highwind Arrow; the rate of recovery is 1 + 2H and the
    /// charge is back 15 / (1 + 2H) after the cast ends.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Highwind_Arrow_Acceleration_And_Event_Horizons_Add_Up(double haste)
    {
        var f = Fight(haste);
        StartEventHorizon(f);
        double hasteInEventHorizon = Haste(f);
        Assert.Equal(haste + 0.3, hasteInEventHorizon, RateTolerance);

        f.Cast<HighwindArrow>();
        double castEnd = CastEnd(f, "Highwind Arrow");

        Assert.Equal(1 + 2 * hasteInEventHorizon, Rate(f, f.Ability<HighwindArrow>()), RateTolerance);
        AssertTime(castEnd + 15 / (1 + 2 * hasteInEventHorizon), WaitUntilFull(f, f.Ability<HighwindArrow>()), "Highwind Arrow recharge");
    }

    /// <summary>
    /// Player: owning a legendary item gives 10% cooldown recovery, and it multiplies with the rest: with Event Horizon, Highwind
    /// Arrow recovers at (1 + H) x 1.1 + H, so a charge is back in 15 / ((1 + H) x 1.1 + H) seconds.
    /// Dev: a legendary item worn (Starstriker's Ascent's, only for its flat recovery); Event Horizon on; one Highwind Arrow;
    /// the charge is back 15 / ((1 + H) x 1.1 + H) after the cast ends, with H = 30% (no gear haste).
    /// TODO: the C++ test this comes from calls the 10% "Mote of Creation"; in the engine that is the 10% Agility, and the recovery is
    /// the Strand of Eternity (see LegendaryStats). The engine adds the 10% into the pool (1.1 + 2H).
    /// </summary>
    [Fact]
    public void Legendary_Cooldown_Recovery_Multiplies_With_Event_Horizons_And_Highwind_Arrows_Acceleration()
    {
        var f = Fight(legendaries: [StarstrikersAscent.HauberkItemId]);
        StartEventHorizon(f);
        double h = Haste(f);

        f.Cast<HighwindArrow>();
        double castEnd = CastEnd(f, "Highwind Arrow");

        double rate = (1 + h) * 1.1 + h;
        Assert.Equal(rate, Rate(f, f.Ability<HighwindArrow>()), RateTolerance);
        AssertTime(castEnd + 15 / rate, WaitUntilFull(f, f.Ability<HighwindArrow>()), "Highwind Arrow recharge");
    }

    // ---- Chronoshift

    /// <summary>
    /// Player: Chronoshift's 800% cooldown recovery multiplies with Event Horizon's acceleration: during Chronoshift with Event
    /// Horizon, Heartseeker Barrage's cooldown ticks at 8 x (1 + haste), not 8 + haste. Each effect on its own gives its own rate, and
    /// they are back to 1 once both are gone.
    /// Dev: at 0% and 20% haste, the rate of recovery of Heartseeker Barrage (no haste recharge of its own) is read with the buffs
    /// applied to the hero: Chronoshift's barrier alone (8), then with Event Horizon's buff (8 x (1 + haste)), then Chronoshift's
    /// removed (1 + haste), then Event Horizon's (1). Haste is the hero's own: Spirit of Heroism is not started here.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Chronoshift_And_Event_Horizon_Cooldown_Recovery_Multiply(double haste)
    {
        var f = Fight(haste, weaponAbilityId: Chronoshift.AbilityId);
        var barrage = f.Ability<HeartseekerBarrage>();

        f.Hero.Auras.Apply(Chronoshift.Barrier, f.Now);
        Assert.Equal(8.0, Rate(f, barrage), RateTolerance);

        f.Hero.Auras.Apply(EventHorizon.Buff, f.Now);
        Assert.Equal(8.0 * (1 + haste), Rate(f, barrage), RateTolerance);

        f.Hero.Auras.Remove(Chronoshift.Barrier, f.Now);
        Assert.Equal(1 + haste, Rate(f, barrage), RateTolerance);

        f.Hero.Auras.Remove(EventHorizon.Buff, f.Now);
        Assert.Equal(1.0, Rate(f, barrage), RateTolerance);
    }

    /// <summary>
    /// Player: Event Horizon's acceleration takes 20 x H seconds off a cooldown over its 20 seconds. Chronoshift takes 21 seconds
    /// off (3 seconds of channel at 8 times the speed), and both together take 20 x H + 21 x (1 + H) off.
    /// Dev: at 0% and 20% haste, three fights (Event Horizon; Chronoshift; both), each casting Skystrider's Grace (whose own +30%
    /// haste is in H, so H = haste + 60%) and, for the Chronoshift ones, Chronoshift right after; 60 seconds after Grace was cast,
    /// its cooldown has 60 - 20 x H, 60 - 21 and 60 - 20 x H - 21 x (1 + H) seconds left.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Chronoshift_And_Event_Horizon_Take_Off_The_Cooldown_Of_Skystriders_Grace_Multiplicatively(double haste)
    {
        // Event Horizon alone
        var eventHorizon = Fight(haste);
        StartEventHorizon(eventHorizon);
        eventHorizon.Cast<SkystridersGrace>();
        double h = Haste(eventHorizon);
        Assert.Equal(haste + 0.6, h, RateTolerance);
        WaitReading(eventHorizon, eventHorizon.Ability<SkystridersGrace>(), 60);
        AssertTime(120 - 60 - 20 * h, SecondsLeft(eventHorizon, eventHorizon.Ability<SkystridersGrace>()), "with Event Horizon");

        // Chronoshift alone
        var chronoshift = Fight(haste, weaponAbilityId: Chronoshift.AbilityId);
        chronoshift.Cast<SkystridersGrace>();
        double castAt = chronoshift.Now.TotalSeconds;
        chronoshift.Cast<Chronoshift>();
        WaitReading(chronoshift, chronoshift.Ability<SkystridersGrace>(), castAt + 60 - chronoshift.Now.TotalSeconds);
        AssertTime(120 - 60 - 21, SecondsLeft(chronoshift, chronoshift.Ability<SkystridersGrace>()), "with Chronoshift");

        // Both
        var both = Fight(haste, weaponAbilityId: Chronoshift.AbilityId);
        StartEventHorizon(both);
        both.Cast<SkystridersGrace>();
        castAt = both.Now.TotalSeconds;
        both.Cast<Chronoshift>();
        WaitReading(both, both.Ability<SkystridersGrace>(), castAt + 60 - both.Now.TotalSeconds);
        AssertTime(120 - 60 - 20 * h - 21 * (1 + h), SecondsLeft(both, both.Ability<SkystridersGrace>()), "with both");
    }

    /// <summary>
    /// Player: with Event Horizon and Chronoshift, Highwind Arrow recovers at (1 + H) x 8 + H during the 3 second channel and at
    /// (1 + H) + H the rest of the time. Three charges (45 seconds of recharge, one after another) are back after
    /// 3 + (45 - 3 x during) / outside seconds.
    /// Dev: no gear haste, Event Horizon on (H = 30%); three Highwind Arrows, then Chronoshift; the time from the end of the first
    /// Highwind Arrow until all three charges are back is compared with the formula (the channel is inside it, wherever it started).
    /// </summary>
    [Fact]
    public void Three_Highwind_Arrows_Recharge_Faster_During_Chronoshift_And_Event_Horizon()
    {
        var f = Fight(weaponAbilityId: Chronoshift.AbilityId);
        StartEventHorizon(f);
        double h = Haste(f);

        f.Cast<HighwindArrow>();
        double start = CastEnd(f, "Highwind Arrow");
        f.Cast<HighwindArrow>();
        f.Cast<HighwindArrow>();
        f.Cast<Chronoshift>();
        double end = WaitUntilFull(f, f.Ability<HighwindArrow>());

        double outside = (1 + h) * 1.0 + h;
        double during = (1 + h) * 8.0 + h;
        AssertTime(start + 3 + (45 - 3 * during) / outside, end, "three charges");
    }

    // ---- Skylit Grace

    /// <summary>The acceleration a Starfall Volley gives Skystrider's Grace with Skylit Grace, per active volley: the talent's +120%.</summary>
    private const double SkylitPerVolley = 1.2;

    /// <summary>
    /// Player: with Skylit Grace, each Starfall Volley falling gives Skystrider's Grace 120% cooldown acceleration, and it adds
    /// to the rest. With Event Horizon and Chronoshift, Grace recovers at 8 x (1 + H) + 1.2 per volley, and without Chronoshift at
    /// (1 + H) + 1.2 per volley.
    /// Dev: the rate of recovery is read with the volleys' effect, Event Horizon's and Chronoshift's applied to the hero (the talent's
    /// buff stands for a volley falling). With one volley only: the rates are 1 + 1.2 alone, 8 + 1.2 with Chronoshift, then with Event
    /// Horizon 8 x (1 + haste) + 1.2, and 1 + haste + 1.2 without Chronoshift.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Skylit_Grace_Adds_To_Event_Horizon_And_Chronoshift_Recovery(double haste)
    {
        var f = Fight(haste, talents: [ElarionTalents.SkylitGrace.TalentId], weaponAbilityId: Chronoshift.AbilityId);
        var grace = f.Ability<SkystridersGrace>();

        f.Hero.Auras.Apply(ElarionTalents.SkylitGrace.Buff, f.Now);
        Assert.Equal(1 + SkylitPerVolley, Rate(f, grace), RateTolerance);

        f.Hero.Auras.Apply(Chronoshift.Barrier, f.Now);
        Assert.Equal(8 + SkylitPerVolley, Rate(f, grace), RateTolerance);

        f.Hero.Auras.Apply(EventHorizon.Buff, f.Now);
        Assert.Equal(8 * (1 + haste) + SkylitPerVolley, Rate(f, grace), RateTolerance);

        f.Hero.Auras.Remove(Chronoshift.Barrier, f.Now);
        Assert.Equal(1 + haste + SkylitPerVolley, Rate(f, grace), RateTolerance);

        f.Hero.Auras.Remove(ElarionTalents.SkylitGrace.Buff, f.Now);
        f.Hero.Auras.Remove(EventHorizon.Buff, f.Now);
        Assert.Equal(1.0, Rate(f, grace), RateTolerance);
    }

    /// <summary>
    /// Player: how long Skystrider's Grace takes to come back with Event Horizon, Chronoshift and Skylit Grace: its 120 seconds, less
    /// 8 x 1.2 for each Starfall Volley (8 seconds at 120% acceleration), less 3 x (8 x (1 + H) - 1) for the Chronoshift channel, less
    /// 17 x H for the rest of Event Horizon.
    /// Dev: Event Horizon on and Skystrider's Grace cast (its 30% haste is in H, so H = 60% at no gear haste), then 0, 1 or 2
    /// Starfall Volleys (with their cooldown reset between them) and Chronoshift (the second volley after the channel); the time
    /// until Grace is ready again is compared with the formula, from the cast of Grace.
    /// TODO: the C++ test this comes from takes H before Grace's own haste (30%); the 30% of Grace itself is added here, as in the
    /// Python sim. The engine gives the acceleration once while any volley falls, not per volley: the two-volley case is expected to
    /// fail, and the tests with the Chronoshift multiplication too (see the class's TODO).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Skystriders_Grace_Recovers_Faster_With_Event_Horizon_Chronoshift_And_Volleys(int volleys)
    {
        var f = Fight(talents: [ElarionTalents.SkylitGrace.TalentId], weaponAbilityId: Chronoshift.AbilityId);
        StartEventHorizon(f);
        f.Cast<SkystridersGrace>();
        double start = f.Now.TotalSeconds;
        double h = Haste(f);

        if (volleys >= 1) f.Cast<StarfallVolley>();
        f.Cast<Chronoshift>();
        Assert.False(f.Hero.IsCooldownReady<SkystridersGrace>(f.Now));
        if (volleys == 2)
        {
            f.Hero.CooldownOf(f.Ability<StarfallVolley>()).ResetAll();
            f.Cast<StarfallVolley>();
        }
        double end = WaitUntilFull(f, f.Ability<SkystridersGrace>());

        double expected = 120 - volleys * SkylitPerVolley * 8 - 3 * (8 * (1 + h) - 1) - 17 * ((1 + h) - 1);
        AssertTime(start + expected, end, "Skystrider's Grace");
    }

    /// <summary>
    /// Player: Skystrider's Grace's cooldown takes 8 seconds off for each Starfall Volley (1.0 acceleration for 8 seconds), with
    /// Skylit Grace, and Event Horizon and Chronoshift take off what they take off on their own.
    /// Dev: the Python sim's timing tests, at 0% and 20% haste with 1, 2 and 3 volleys (the volleys' cooldown reset between casts):
    /// 60 seconds after Grace was cast the cooldown has 60 - 20 x H - 8 x volleys left with Event Horizon, 60 - 21 - 8 x volleys with
    /// Chronoshift, and, 30 seconds after, 90 - 20 x H - 21 x (1 + H) - 8 x volleys with both (H = haste + 60%).
    /// TODO: the Python sim gives each volley 100% acceleration where the C++ test, the game's tooltip (+120%) and the tests above have
    /// 120%: skipped. Check in game.
    /// </summary>
    [Theory(Skip = "The Python sim has 100% acceleration per volley, the tooltip and the C++ test 120%: to check in game")]
    [InlineData(0.0, 1)]
    [InlineData(0.0, 2)]
    [InlineData(0.0, 3)]
    [InlineData(0.2, 1)]
    [InlineData(0.2, 2)]
    [InlineData(0.2, 3)]
    public void Skylit_Grace_Takes_8_Seconds_Per_Volley_Off_Skystriders_Graces_Cooldown(double haste, int volleys)
    {
        void CastVolleys(StepDriver f)
        {
            for (int i = 0; i < volleys; i++)
            {
                f.Hero.CooldownOf(f.Ability<StarfallVolley>()).ResetAll();
                f.Cast<StarfallVolley>();
            }
        }

        // Event Horizon
        var eventHorizon = Fight(haste, talents: [ElarionTalents.SkylitGrace.TalentId]);
        StartEventHorizon(eventHorizon);
        eventHorizon.Cast<SkystridersGrace>();
        double h = Haste(eventHorizon);
        double castAt = eventHorizon.Now.TotalSeconds;
        CastVolleys(eventHorizon);
        WaitReading(eventHorizon, eventHorizon.Ability<SkystridersGrace>(), castAt + 60 - eventHorizon.Now.TotalSeconds);
        AssertTime(120 - 60 - 20 * h - 8 * volleys, SecondsLeft(eventHorizon, eventHorizon.Ability<SkystridersGrace>()), "with Event Horizon");

        // Chronoshift
        var chronoshift = Fight(haste, talents: [ElarionTalents.SkylitGrace.TalentId], weaponAbilityId: Chronoshift.AbilityId);
        chronoshift.Cast<SkystridersGrace>();
        castAt = chronoshift.Now.TotalSeconds;
        chronoshift.Cast<Chronoshift>();
        CastVolleys(chronoshift);
        WaitReading(chronoshift, chronoshift.Ability<SkystridersGrace>(), castAt + 60 - chronoshift.Now.TotalSeconds);
        AssertTime(120 - 60 - 21 - 8 * volleys, SecondsLeft(chronoshift, chronoshift.Ability<SkystridersGrace>()), "with Chronoshift");

        // Both, 30 seconds after
        var both = Fight(haste, talents: [ElarionTalents.SkylitGrace.TalentId], weaponAbilityId: Chronoshift.AbilityId);
        StartEventHorizon(both);
        both.Cast<SkystridersGrace>();
        castAt = both.Now.TotalSeconds;
        both.Cast<Chronoshift>();
        CastVolleys(both);
        WaitReading(both, both.Ability<SkystridersGrace>(), castAt + 30 - both.Now.TotalSeconds);
        AssertTime(120 - 30 - 20 * h - 21 * (1 + h) - 8 * volleys, SecondsLeft(both, both.Ability<SkystridersGrace>()), "with both");
    }

    /// <summary>
    /// Player: with Skylit Grace, each volley falling adds 100% to Skystrider's Grace's recovery: one volley doubles it, two triple it,
    /// and it is back to normal when the volleys are over.
    /// Dev: the Python sim's test of that rate; volleys cast one after the other, the rate read after each, and after they end.
    /// TODO: the Python sim gives each volley 100% acceleration where the tooltip has 120% (skipped, as above).
    /// </summary>
    [Fact(Skip = "The Python sim has 100% acceleration per volley, the tooltip and the C++ test 120%: to check in game")]
    public void Each_Volley_Doubles_Skystriders_Graces_Recovery_With_Skylit_Grace()
    {
        var f = Fight(talents: [ElarionTalents.SkylitGrace.TalentId]);
        var grace = f.Ability<SkystridersGrace>();
        Assert.Equal(1.0, Rate(f, grace), RateTolerance);

        f.Cast<StarfallVolley>();
        Assert.Equal(2.0, Rate(f, grace), RateTolerance);

        f.Hero.CooldownOf(f.Ability<StarfallVolley>()).ResetAll();
        f.Cast<StarfallVolley>();
        Assert.Equal(3.0, Rate(f, grace), RateTolerance);

        WaitReading(f, grace, 12);
        Assert.Equal(1.0, Rate(f, grace), RateTolerance);
    }

    // ---- What Event Horizon does to the cooldowns of other abilities

    /// <summary>
    /// Player: during Event Horizon, each Highwind Arrow takes 0.5 seconds off Heartseeker Barrage's cooldown.
    /// Dev: Event Horizon's buff applied (no Spirit of Heroism, so recovery is at the normal rate); a barrage, then a Highwind Arrow on
    /// one enemy; the barrage is ready 0.5 seconds earlier than it was.
    /// </summary>
    [Fact]
    public void Event_Horizon_Highwind_Arrow_Takes_0_5_Seconds_Off_Heartseeker_Barrage()
    {
        var f = Fight();
        f.Hero.Auras.Apply(EventHorizon.Buff, f.Now);
        f.Cast<HeartseekerBarrage>();
        double readyBefore = f.Now.TotalSeconds + SecondsLeft(f, f.Ability<HeartseekerBarrage>());

        f.Cast<HighwindArrow>();

        AssertTime(readyBefore - 0.5, f.Now.TotalSeconds + SecondsLeft(f, f.Ability<HeartseekerBarrage>()), "Heartseeker Barrage ready at", 0.002);
    }

    /// <summary>
    /// Player: during Event Horizon, each Highwind Arrow takes 0.5 seconds off Heartseeker Barrage's cooldown only while Event Horizon
    /// is up.
    /// Dev: no Event Horizon; a barrage, then a Highwind Arrow; the barrage's ready time is unchanged.
    /// </summary>
    [Fact]
    public void Highwind_Arrow_Does_Not_Reduce_Heartseeker_Barrage_Without_Event_Horizon()
    {
        var f = Fight();
        f.Cast<HeartseekerBarrage>();
        double readyBefore = f.Now.TotalSeconds + SecondsLeft(f, f.Ability<HeartseekerBarrage>());

        f.Cast<HighwindArrow>();

        AssertTime(readyBefore, f.Now.TotalSeconds + SecondsLeft(f, f.Ability<HeartseekerBarrage>()), "Heartseeker Barrage ready at", 0.002);
    }

    /// <summary>
    /// Player: during Event Horizon, each arrow of Heartseeker Barrage takes 1 second off Starfall Volley's cooldown.
    /// Dev: Event Horizon's buff applied (recovery at the normal rate); a Starfall Volley, then a barrage on one enemy; the volley is ready
    /// as many seconds earlier as the barrage had arrows.
    /// TODO: the Python sim has 1.0 second per arrow; the engine has 1.5, from the ability's tooltip in the game data ("every hit from
    /// your Heartseeker Barrage reduces the cooldown of Starfall Volley by 1.5 seconds"). Check in game.
    /// </summary>
    [Fact(Skip = "The Python sim has 1.0 second per arrow, the engine 1.5 from the game data's tooltip: to check in game")]
    public void Event_Horizon_Heartseeker_Barrage_Arrows_Take_1_Second_Off_Starfall_Volley()
    {
        var f = Fight();
        f.Hero.Auras.Apply(EventHorizon.Buff, f.Now);
        f.Cast<StarfallVolley>();
        double readyBefore = f.Now.TotalSeconds + SecondsLeft(f, f.Ability<StarfallVolley>());

        f.Cast<HeartseekerBarrage>();
        f.Wait(0.5);

        int arrows = f.Damage("Heartseeker Barrage").Count;
        AssertTime(readyBefore - arrows * 1.0, f.Now.TotalSeconds + SecondsLeft(f, f.Ability<StarfallVolley>()), "Starfall Volley ready at", 0.002);
    }

    /// <summary>
    /// Player: how many global cooldowns you need after a Heartseeker Barrage during Event Horizon before Starfall Volley is ready again
    /// (with Fusillade and Piercing Seekers, two enemies): the more haste, the more arrows and the sooner it is ready, in steps.
    /// Dev: the Python sim's table, at haste 0% to 62%: the barrage has 2 x (16 to 24) arrows (each ricochets), the volley has the listed
    /// seconds left (within 10ms), and the given number of Focused Shots makes it ready. Event Horizon cast with 100 Spirit Points, then a
    /// Starfall Volley and a barrage.
    /// TODO: the numbers are the Python sim's, with 1.0 second per arrow and its own projectile flight: skipped with the test above.
    /// </summary>
    [Theory(Skip = "The Python sim has 1.0 second per arrow and its own projectile flight, the engine 1.5 seconds: to check in game")]
    [InlineData(0.0, 7, 9.172, 16)]
    [InlineData(0.06, 6, 8.018, 17)]
    [InlineData(0.14, 5, 6.814, 18)]
    [InlineData(0.22, 4, 5.609, 19)]
    [InlineData(0.30, 3, 4.404, 20)]
    [InlineData(0.38, 3, 3.199, 21)]
    [InlineData(0.46, 2, 1.994, 22)]
    [InlineData(0.54, 1, 0.790, 23)]
    [InlineData(0.62, 0, 0.0, 24)]
    public void Event_Horizon_Starfall_Volley_Is_Ready_Again_After_A_Barrage_By_Haste(double haste, int globalCooldowns, double secondsLeft, int arrowsPerEnemy)
    {
        var f = Fight(haste, enemies: 2, talents: [ElarionTalents.Fusillade.TalentId, ElarionTalents.PiercingSeekers.TalentId]);
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;
        f.Cast<EventHorizon>();
        f.Cast<StarfallVolley>();
        f.Cast<HeartseekerBarrage>();
        f.Wait(0.06);

        Assert.Equal(2 * arrowsPerEnemy, f.Damage("Heartseeker Barrage").Count);
        AssertTime(secondsLeft, SecondsLeft(f, f.Ability<StarfallVolley>()), "Starfall Volley left", 0.01);
        for (int i = 0; i < globalCooldowns; i++)
        {
            Assert.True(SecondsLeft(f, f.Ability<StarfallVolley>()) > 0);
            f.Cast<FocusedShot>();
        }
        Assert.Equal(0, SecondsLeft(f, f.Ability<StarfallVolley>()), 1e-9);
    }
}
