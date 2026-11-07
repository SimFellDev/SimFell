using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Event Horizon and cooldowns: for its 20 seconds it gives Cooldown Acceleration equal to your haste, its Highwind Arrows take time off
/// Heartseeker Barrage and its barrage arrows time off Starfall Volley. Also how its acceleration stacks with the other ways a cooldown
/// recovers faster: Highwind Arrow's hasted charges, Chronoshift and the Strand of Eternity every legendary item carries, The Monarch, and
/// Skylit Grace. Event Horizon's halved Focus costs are in ElarionResourceEconomy.cs, its damage and duration in ElarionAbilities.cs, its
/// cast time in ElarionCastTimes.cs, and Skylit Grace's acceleration on its own in ElarionCooldowns.cs.
///
/// Event Horizon gives Spirit of Heroism (30% haste, Generic/Ultimate.cs), so during it the hero's haste is his gear haste + 30%; the
/// tests call it h. A cooldown recovers at (1 + the recovery bonuses) x (1 + h) + the haste of hasted charges: the recovery bonuses
/// (Chronoshift's 7, Strand of Eternity's 0.1) add up, Event Horizon multiplies them, and Highwind Arrow's hasted charges add after it
/// (the game's formula, datamined from build 26320967), as do The Monarch's and Skylit Grace's accelerations. Every duration is checked by
/// the ability being not ready 0.1s before its end and ready 0.1s after; a recovery speed is read with <c>RecoveryRate</c>.
/// </summary>
public class ElarionEventHorizonCooldown
{
    // ---- Event Horizon's Cooldown Acceleration

    /// <summary>
    /// During Event Horizon your cooldowns recover with acceleration equal to your haste: a Heartseeker Barrage cast right after it is
    /// back in 20 / (1 + h) seconds instead of 20.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Event_Horizon_Gives_Cooldown_Acceleration_Equal_To_Haste(double haste)
    {
        var f = StepFights.Elarion(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();
        f.Cast<HeartseekerBarrage>();
        double h = haste + 0.3; // Spirit of Heroism's 30%
        double end = f.CompletedAt("Heartseeker Barrage") + 20.0 / (1 + h);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<HeartseekerBarrage>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<HeartseekerBarrage>());
    }

    /// <summary>
    /// The acceleration ends with Event Horizon: a Heartseeker Barrage cast 15 seconds into it recovers 5 x 1.3 seconds of its cooldown
    /// in the last 5 seconds of Event Horizon, then the rest at the normal speed.
    /// </summary>
    [Fact]
    public void Event_Horizon_Acceleration_Ends_With_Event_Horizon()
    {
        var f = StepFights.Elarion();
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();
        double horizonEnd = f.CompletedAt("Event Horizon") + 20.0;
        f.WaitUntil(horizonEnd - 5.0);

        f.Cast<HeartseekerBarrage>();
        double recovered = (horizonEnd - f.CompletedAt("Heartseeker Barrage")) * 1.3; // Spirit of Heroism's 30% haste
        double end = horizonEnd + 20.0 - recovered;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<HeartseekerBarrage>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<HeartseekerBarrage>());
    }

    // ---- Event Horizon with the other sources of faster recovery

    /// <summary>
    /// Highwind Arrow's charges recharge with your haste, added after Event Horizon's acceleration: during Event Horizon a charge comes
    /// back in 15 / ((1 + h) + h) seconds.
    ///
    /// The charges' haste is game data (cooldownTimeRateScalesOff Haste, HighwindArrow.cs). How a charge recharges with haste alone is
    /// in ElarionCooldowns.cs.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Highwind_Arrow_Haste_Recharge_Adds_To_Event_Horizons_Acceleration(double haste)
    {
        var f = StepFights.Elarion(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();
        f.Cast<HighwindArrow>();
        double h = haste + 0.3; // Spirit of Heroism's 30%
        double end = f.CompletedAt("Highwind Arrow") + 15.0 / ((1 + h) + h);

        f.WaitUntil(end - 0.1);
        Assert.Equal(2, f.Charges<HighwindArrow>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(3, f.Charges<HighwindArrow>());
    }

    /// <summary>
    /// Event Horizon's acceleration multiplies the 10% cooldown recovery of a legendary item's Strand of Eternity: during Event Horizon a
    /// Heartseeker Barrage is back in 20 / (1.1 x (1 + h)) seconds.
    ///
    /// Starstriker's Ascent is worn only for its Strand of Eternity (its power needs a Spirit Refund, and the bare hero has no Spirit).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Event_Horizon_Multiplies_Strand_Of_Eternitys_Recovery(double haste)
    {
        var f = StepFights.Elarion(haste: haste, legendaries: [StarstrikersAscent.HauberkItemId]);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();
        f.Cast<HeartseekerBarrage>();
        double h = haste + 0.3; // Spirit of Heroism's 30%
        double end = f.CompletedAt("Heartseeker Barrage") + 20.0 / (1.1 * (1 + h));

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<HeartseekerBarrage>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<HeartseekerBarrage>());
    }

    /// <summary>
    /// Event Horizon's acceleration multiplies Strand of Eternity's 10% cooldown recovery, and Highwind Arrow's hasted charges add after
    /// it: during Event Horizon, Highwind Arrow recovers at (1 + h) x 1.1 + h, and a charge is back in 15 seconds over that.
    ///
    /// Starstriker's Ascent is worn only for its Strand of Eternity, as above.
    /// </summary>
    [Fact]
    public void Legendary_Cooldown_Recovery_Multiplies_With_Event_Horizons_And_Highwind_Arrows_Acceleration()
    {
        var f = StepFights.Elarion(legendaries: [StarstrikersAscent.HauberkItemId]);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();
        f.Cast<HighwindArrow>();
        double h = 0.3; // Spirit of Heroism's 30%
        double rate = (1 + h) * 1.1 + h;
        double end = f.CompletedAt("Highwind Arrow") + 15.0 / rate;

        using var check = new Checks(f);
        check.Equal(rate, f.RecoveryRate<HighwindArrow>());
        f.WaitUntil(end - 0.1);
        check.Equal(2, f.Charges<HighwindArrow>());
        f.WaitUntil(end + 0.1);
        check.Equal(3, f.Charges<HighwindArrow>());
    }

    /// <summary>
    /// Chronoshift's barrier makes your cooldowns recover 8 times as fast, Event Horizon multiplies that by 1 + haste, and the speed is
    /// back to normal once both are gone: Heartseeker Barrage recovers at 8 during the channel, 8 x (1 + haste) with Event Horizon too,
    /// 1 + haste once the channel is over, and 1 after Event Horizon.
    ///
    /// The barrier and Event Horizon's buff are applied and removed directly, without Spirit of Heroism, so haste is the gear's alone.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Chronoshift_And_Event_Horizon_Cooldown_Recovery_Multiply(double haste)
    {
        var f = StepFights.Elarion(haste: haste, weaponAbilityId: Chronoshift.AbilityId);

        using var check = new Checks(f);
        f.Apply(Chronoshift.Barrier); // as Chronoshift's channel gives it
        check.Equal(8.0, f.RecoveryRate<HeartseekerBarrage>());
        f.Apply(EventHorizon.Buff);
        check.Equal(8.0 * (1 + haste), f.RecoveryRate<HeartseekerBarrage>());
        f.Remove(Chronoshift.Barrier); // the channel ends
        check.Equal(1 + haste, f.RecoveryRate<HeartseekerBarrage>());
        f.Remove(EventHorizon.Buff); // Event Horizon ends
        check.Equal(1.0, f.RecoveryRate<HeartseekerBarrage>());
    }

    /// <summary>
    /// The Monarch's Cooldown Acceleration on Major abilities adds to Event Horizon's, not multiplied by it: during Event Horizon,
    /// Skystrider's Supremacy recovers at (1 + h) + the glyph's 12% + 0.1% per 1% haste, after it at 1 + 12%.
    ///
    /// The Monarch is an ability's own acceleration, added after Event Horizon multiplies the rest (the game's formula).
    /// </summary>
    [Fact]
    public void The_Monarchs_Acceleration_Adds_To_Event_Horizons()
    {
        var f = StepFights.Elarion(glyphs: [new(TheMonarch.GlyphId, Rank: 4)]);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();
        double horizonEnd = f.CompletedAt("Event Horizon") + 20.0;
        f.Cast<SkystridersSupremacy>();
        double h = 0.3; // Spirit of Heroism's 30%
        double during = (1 + h) + 0.12 + 0.001 * h * 100;
        double recovered = (horizonEnd - f.CompletedAt("Skystrider's Supremacy")) * during;
        double end = horizonEnd + (45.0 - recovered) / (1 + 0.12);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<SkystridersSupremacy>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<SkystridersSupremacy>());
    }

    /// <summary>
    /// With Event Horizon and Chronoshift, Highwind Arrow recovers at (1 + h) x 8 + h during the 3 second channel and at (1 + h) + h the
    /// rest of the time: three charges (45 seconds of recharge, one after another) are back 3 + (45 - 3 x during) / outside seconds after
    /// the first arrow.
    ///
    /// The channel is inside that time wherever it starts, since the charges recharge one after another from the first arrow.
    /// </summary>
    [Fact]
    public void Three_Highwind_Arrows_Recharge_Faster_During_Chronoshift_And_Event_Horizon()
    {
        var f = StepFights.Elarion(weaponAbilityId: Chronoshift.AbilityId);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();
        f.Cast<HighwindArrow>();
        double start = f.CompletedAt("Highwind Arrow");
        f.Cast<HighwindArrow>();
        f.Cast<HighwindArrow>();
        f.Cast<Chronoshift>();
        double h = 0.3; // Spirit of Heroism's 30%
        double outside = (1 + h) * 1.0 + h;
        double during = (1 + h) * 8.0 + h;
        double end = start + 3 + (45 - 3 * during) / outside;

        f.WaitUntil(end - 0.1);
        Assert.Equal(2, f.Charges<HighwindArrow>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(3, f.Charges<HighwindArrow>());
    }

    /// <summary>
    /// With Skylit Grace, a Starfall Volley falling gives Skystrider's Grace 120% Cooldown Acceleration, added to the rest: Grace recovers
    /// at 1 + 1.2 with one volley, 8 + 1.2 during Chronoshift's channel, 8 x (1 + haste) + 1.2 with Event Horizon too, 1 + haste + 1.2
    /// once the channel is over, and 1 once the volley and Event Horizon are over.
    ///
    /// Skylit Grace's buff stands for one volley falling. The buffs are applied and removed directly, without Spirit of Heroism, so haste
    /// is the gear's alone.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Skylit_Grace_Adds_To_Event_Horizon_And_Chronoshift_Recovery(double haste)
    {
        var f = StepFights.Elarion(haste: haste, talents: [SkylitGrace.TalentId], weaponAbilityId: Chronoshift.AbilityId);

        using var check = new Checks(f);
        f.Apply(SkylitGrace.Buff); // one volley falling
        check.Equal(1 + 1.2, f.RecoveryRate<SkystridersGrace>());
        f.Apply(Chronoshift.Barrier); // as Chronoshift's channel gives it
        check.Equal(8 + 1.2, f.RecoveryRate<SkystridersGrace>());
        f.Apply(EventHorizon.Buff);
        check.Equal(8 * (1 + haste) + 1.2, f.RecoveryRate<SkystridersGrace>());
        f.Remove(Chronoshift.Barrier); // the channel ends
        check.Equal(1 + haste + 1.2, f.RecoveryRate<SkystridersGrace>());
        f.Remove(SkylitGrace.Buff); // the volley ends
        f.Remove(EventHorizon.Buff); // Event Horizon ends
        check.Equal(1.0, f.RecoveryRate<SkystridersGrace>());
    }

    // ---- Time Event Horizon takes off Heartseeker Barrage and Starfall Volley

    /// <summary>
    /// While Event Horizon is up, each Highwind Arrow takes 0.5 seconds off Heartseeker Barrage's cooldown; without it, none.
    ///
    /// Event Horizon's buff is applied without Spirit of Heroism, so the bare hero has no haste and so no acceleration: the cooldown runs
    /// at the normal speed and only the reduction moves it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Event_Horizon_Highwind_Arrow_Takes_0_5_Seconds_Off_Heartseeker_Barrage(bool horizon)
    {
        var f = StepFights.Elarion();
        if (horizon) f.Apply(EventHorizon.Buff);

        f.Cast<HeartseekerBarrage>();
        f.Cast<HighwindArrow>();
        double end = f.CompletedAt("Heartseeker Barrage") + 20.0 - (horizon ? 0.5 : 0.0);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<HeartseekerBarrage>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<HeartseekerBarrage>());
    }

    /// <summary>
    /// While Event Horizon is up, each hit of Heartseeker Barrage takes 1.5 seconds off Starfall Volley's cooldown; without it, none.
    ///
    /// Event Horizon's buff is applied without Spirit of Heroism, as above. The tooltip's 1.5 seconds (ability 1584); the older Python
    /// sim's game version had 1.0.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Event_Horizon_Barrage_Hits_Take_1_5_Seconds_Each_Off_Starfall_Volley(bool horizon)
    {
        var f = StepFights.Elarion();
        if (horizon) f.Apply(EventHorizon.Buff);

        f.Cast<StarfallVolley>();
        f.Cast<HeartseekerBarrage>();
        f.Wait(0.5); // the last arrows land
        int arrows = f.Damage<HeartseekerBarrage>().Count;
        double end = f.CompletedAt("Starfall Volley") + 40.0 - (horizon ? 1.5 * arrows : 0.0);

        using var check = new Checks(f);
        check.True(arrows > 0, "the barrage hits");
        f.WaitUntil(end - 0.1);
        check.False(f.IsReady<StarfallVolley>());
        f.WaitUntil(end + 0.1);
        check.True(f.IsReady<StarfallVolley>());
    }

    /// <summary>
    /// When Event Horizon ends in the middle of a Heartseeker Barrage, only the hits that land before its end take 1.5 seconds each off
    /// Starfall Volley's cooldown; the hits after it take nothing off.
    ///
    /// Event Horizon's buff is applied at 0s without Spirit of Heroism, as above, and the barrage starts 0.95 seconds before the buff ends,
    /// so its 2 second channel straddles the end. The end is the time the buff is removed.
    /// </summary>
    [Fact]
    public void Event_Horizon_Ending_During_A_Barrage_Takes_Time_Off_Only_For_The_Hits_Before_Its_End()
    {
        var f = StepFights.Elarion();
        f.Apply(EventHorizon.Buff);

        f.Cast<StarfallVolley>();
        f.WaitUntil(19.05); // the barrage straddles Event Horizon's end
        f.Cast<HeartseekerBarrage>();
        f.Wait(0.5); // the last arrows land
        double horizonEnd = f.AurasRemoved(EventHorizon.Buff).Single().Time.TotalSeconds;
        var hits = f.Damage<HeartseekerBarrage>();
        int hitsDuringHorizon = hits.Count(hit => hit.Time.TotalSeconds < horizonEnd);
        double end = f.CompletedAt("Starfall Volley") + 40.0 - 1.5 * hitsDuringHorizon;

        using var check = new Checks(f);
        check.True(hitsDuringHorizon > 0 && hitsDuringHorizon < hits.Count, "Event Horizon ends during the barrage");
        f.WaitUntil(end - 0.1);
        check.False(f.IsReady<StarfallVolley>());
        f.WaitUntil(end + 0.1);
        check.True(f.IsReady<StarfallVolley>());
    }

    /// <summary>
    /// Piercing Seekers' ricochets take no time off Starfall Volley: during Event Horizon, a Heartseeker Barrage on 2 enemies with Piercing
    /// Seekers leaves Starfall Volley's cooldown where the same barrage without it does, 1.5 seconds off per hit on the target.
    ///
    /// Per the user. Event Horizon's buff is applied without Spirit of Heroism, as above.
    /// </summary>
    [Fact]
    public void Piercing_Seekers_Ricochets_Take_No_Time_Off_Starfall_Volley()
    {
        var plainScenario = StepFights.Elarion(enemies: 2);
        plainScenario.Apply(EventHorizon.Buff);

        plainScenario.Cast<StarfallVolley>();
        plainScenario.Cast<HeartseekerBarrage>();
        plainScenario.Wait(0.5); // the last arrows land
        int targetHits = plainScenario.Damage<HeartseekerBarrage>().Count(hit => hit.Target == plainScenario.Enemy.Name);
        double end = plainScenario.CompletedAt("Starfall Volley") + 40.0 - 1.5 * targetHits;

        var piercingScenario = StepFights.Elarion(enemies: 2, talents: [PiercingSeekers.TalentId]);
        piercingScenario.Apply(EventHorizon.Buff);

        piercingScenario.Cast<StarfallVolley>();
        piercingScenario.Cast<HeartseekerBarrage>();
        piercingScenario.Wait(0.5); // the last arrows land
        int ricochets = piercingScenario.Damage<HeartseekerBarrage>().Count(hit => hit.Target != piercingScenario.Enemy.Name);

        using var check = new Checks();
        check.True(ricochets > 0, "the barrage ricochets with Piercing Seekers");
        foreach (var (scenario, with) in new[] { (plainScenario, "without Piercing Seekers"),
                     (piercingScenario, "with Piercing Seekers") })
        {
            scenario.WaitUntil(end - 0.1);
            check.False(scenario.IsReady<StarfallVolley>(), what: $"{with}, ready 0.1s before {end:F3}s");
            scenario.WaitUntil(end + 0.1);
            check.True(scenario.IsReady<StarfallVolley>(), what: $"{with}, ready 0.1s after {end:F3}s");
        }
    }

    /// <summary>
    /// Check how soon Volley is ready (measured in GCDs) during EH, under various conditions: Blessing of the Commander's cooldown reduction (4% at rank I, Emerald 450; 12% at rank II,
    /// Emerald 1500) lowers each breakpoint, and so does Fusillade's 2.5 second barrage (2 without it).
    ///
    /// The haste is the gear's; Spirit of Heroism's 30% comes on top. Each breakpoint is where the barrage fits one more arrow, which
    /// takes 1.5 seconds more off the volley; the barrage fires floor(channel / tick) arrows, never a partial one (see
    /// ElarionHasteBreakpoints). Readiness is read each time the hero is free. The breakpoints are computed: the engine measured
    /// 23 - arrows extra global cooldowns (22 - arrows with rank I, 20 - arrows with rank II, with or without Fusillade) at every
    /// haste, but it fires a partial arrow, so its own breakpoints are one arrow early.
    /// </summary>
    [Theory]
    [InlineData(false, 0, 0.70, 3)]
    [InlineData(false, 0, 0.80, 2)]
    [InlineData(false, 0, 0.90, 1)]
    [InlineData(false, 0, 1.00, 0)]
    [InlineData(false, 450, 0.60, 3)]
    [InlineData(false, 450, 0.70, 2)]
    [InlineData(false, 450, 0.80, 1)]
    [InlineData(false, 450, 0.90, 0)]
    [InlineData(false, 1500, 0.40, 3)]
    [InlineData(false, 1500, 0.50, 2)]
    [InlineData(false, 1500, 0.60, 1)]
    [InlineData(false, 1500, 0.70, 0)]
    [InlineData(true, 0, 0.30, 3)]
    [InlineData(true, 0, 0.38, 2)]
    [InlineData(true, 0, 0.46, 1)]
    [InlineData(true, 0, 0.54, 0)]
    [InlineData(true, 450, 0.22, 3)]
    [InlineData(true, 450, 0.30, 2)]
    [InlineData(true, 450, 0.38, 1)]
    [InlineData(true, 450, 0.46, 0)]
    [InlineData(true, 1500, 0.06, 3)]
    [InlineData(true, 1500, 0.14, 2)]
    [InlineData(true, 1500, 0.22, 1)]
    [InlineData(true, 1500, 0.30, 0)]
    public void Starfall_Volley_Reset_After_A_Barrage_During_Event_Horizon_Haste_Breakpoints(bool fusillade, double emeraldPower, double haste,
        int extraGcds)
    {
        using var check = new Checks();
        check.Equal(extraGcds, ExtraGcdsUntilTheVolleyIsReady(fusillade, emeraldPower, haste), what: $"extra global cooldowns at {haste:P0} haste");
        check.Equal(extraGcds + 1, ExtraGcdsUntilTheVolleyIsReady(fusillade, emeraldPower, haste - 0.01),
            what: $"extra global cooldowns at {haste - 0.01:P0} haste");
    }

    /// <summary>
    /// Starfall Volley, Event Horizon and Heartseeker Barrage with a Strand of Eternity (and Fusillade), then Celestial Shots until Starfall Volley is
    /// ready: how many it took.
    /// </summary>
    private static int ExtraGcdsUntilTheVolleyIsReady(bool fusillade, double emeraldPower, double haste)
    {
        var f = StepFights.Elarion(haste: haste, legendaries: [StarstrikersAscent.HauberkItemId], talents: fusillade ? [Fusillade.TalentId] : null,
            gemPower: emeraldPower > 0 ? new() { [GemColor.Emerald] = emeraldPower } : null);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Event Horizon

        f.Cast<StarfallVolley>();
        f.Cast<EventHorizon>();
        f.Cast<HeartseekerBarrage>();
        int gcds = 0;
        while (!f.IsReady<StarfallVolley>() && gcds < 20)
        {
            f.Cast<CelestialShot>();
            gcds++;
        }
        return gcds;
    }
}
