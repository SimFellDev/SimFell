using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's legendary items, each worn on a bare Mara against one enemy: From the Shadows (her cape), Vexira's Venom (her wristbands) and Drenched in Blood (her ring).
/// Any legendary also gives Mote of Creation's +10% main stat, so the damage tests wear the legendary in both fights.
/// </summary>
public class MaraLegendaries
{
    // ---- From the Shadows

    /// <summary>
    /// With From the Shadows, Hemorrhaging Strike's bleed ticks faster, for the same duration: its period is 0.87 of 3 seconds, shortened
    /// by haste, floor(15 / tick) full ticks over the 15 seconds of a 1 Combo Point bleed, plus a partial one for the time left (at least
    /// 0.1 seconds).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void From_The_Shadows_Makes_The_Bleeds_Tick_Period_87_Percent(double haste)
    {
        double tick = 3 * 0.87 / (1 + haste);
        var f = StepFights.Mara(haste: haste, legendaries: [FromTheShadows.CapeItemId]);
        f.ForceProcs(key => key == FromTheShadows.ProcKey ? false : null); // no clone
        f.Set(Mara.ComboPoints, 1); // a 15 second bleed

        f.Cast<HemorrhagingStrike>();
        f.Wait(20); // the bleed is over
        var ticks = f.Damage("Hemorrhage");

        using var check = new Checks(f);
        check.Equal(FullTicks(15, tick) + PartialTick(15, tick, 0.1), ticks.Count);
        check.Seconds(tick, ticks[1].Time.TotalSeconds - ticks[0].Time.TotalSeconds);
    }

    /// <summary>
    /// With From the Shadows, each time you deal damage to an enemy with Hemorrhaging Strike's bleed you have a 15% chance to summon a
    /// shadowy clone: no roll for a Backstab before the bleed, then a roll at 15% for every hit after it.
    /// </summary>
    [Fact]
    public void From_The_Shadows_Has_A_15_Percent_Chance_On_Each_Hit_On_A_Bleeding_Enemy()
    {
        var f = StepFights.Mara(legendaries: [FromTheShadows.CapeItemId]);
        var procs = f.ForceProcs(key => key == FromTheShadows.ProcKey ? false : null); // only to read the chance

        f.Cast<Backstab>();
        int rollsBeforeTheBleed = procs.ChancesOf(FromTheShadows.ProcKey).Count;
        f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike
        f.Cast<HemorrhagingStrike>();
        var bleedStart = f.Damage<HemorrhagingStrike>().Single().Time; // the bleed goes on after the strike's hit
        f.Wait(6); // ticks and auto attacks on the bleeding enemy
        var hitsOnTheBleedingEnemy = f.DamageBetween(after: bleedStart);
        var chances = procs.ChancesOf(FromTheShadows.ProcKey);

        using var check = new Checks(f);
        check.Equal(0, rollsBeforeTheBleed);
        check.True(hitsOnTheBleedingEnemy.Count > 0);
        check.Equal(hitsOnTheBleedingEnemy.Count, chances.Count);
        check.True(chances.All(chance => chance == 0.15));
    }

    /// <summary>
    /// The shadowy clone of From the Shadows assaults the enemy with a Queen's Fang at maximum Combo Points and full power: its hit equals
    /// your own Queen's Fang at 6 Combo Points.
    /// </summary>
    [Fact]
    public void From_The_Shadows_Clone_Strikes_A_Queens_Fang_At_6_Combo_Points()
    {
        var plainScenario = StepFights.Mara(legendaries: [FromTheShadows.CapeItemId]).WithoutDamageSpread();
        plainScenario.ForceProcs(key => key == FromTheShadows.ProcKey ? false : null); // no clone
        plainScenario.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike

        plainScenario.Cast<HemorrhagingStrike>(); // the same bleeding enemy as the clone's
        plainScenario.Set(Mara.ComboPoints, 6);
        plainScenario.Cast<QueensFang>();
        double ownQueensFangDamage = plainScenario.Damage<QueensFang>().Single().Value;

        var cloneScenario = StepFights.Mara(legendaries: [FromTheShadows.CapeItemId]).WithoutDamageSpread();
        bool summoned = false;
        cloneScenario.ForceProcs(key => key == FromTheShadows.ProcKey ? !summoned && (summoned = true) : null); // the first roll procs, no other
        cloneScenario.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike

        cloneScenario.Cast<HemorrhagingStrike>();
        cloneScenario.Wait(4); // a hit on the bleeding enemy summons the clone
        var cloneHits = cloneScenario.Damage("Queen's Fang (shadowy clone)");

        using var check = new Checks(cloneScenario);
        check.Equal(1, cloneHits.Count);
        check.Ratio(1.0, cloneHits[0].Value / ownQueensFangDamage);
    }

    /// <summary>
    /// With Bloodrush and From the Shadows, the two speed-ups multiply the bleed's period: every 3 x 0.87 / 1.2 seconds shortened by
    /// haste, floor(15 / tick) full ticks over the 15 seconds of a 1 Combo Point bleed, plus a partial one for the time left (at least 0.1
    /// seconds).
    ///
    /// From the game data and a combat log, as PR #61 reads them: each scales the bleed's tick period, From the Shadows by 0.87 and
    /// Bloodrush by 1 / 1.2, and fjrCax7VzGmMZw6h fight 26 has its ticks 0.722 to 0.739 of 3 seconds / haste apart (0.87 / 1.2 = 0.725).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Bloodrush_And_From_The_Shadows_Multiply_The_Bleeds_Tick_Period(double haste)
    {
        double tick = 3 * 0.87 / 1.2 / (1 + haste);
        var f = StepFights.Mara(haste: haste, talents: [Bloodrush.TalentId], legendaries: [FromTheShadows.CapeItemId]);
        f.ForceProcs(key => key == FromTheShadows.ProcKey ? false : null); // no clone
        f.Set(Mara.ComboPoints, 1); // a 15 second bleed

        f.Cast<HemorrhagingStrike>();
        f.Wait(20); // the bleed is over

        var ticks = f.Damage("Hemorrhage");
        using var check = new Checks(f);
        check.Equal(FullTicks(15, tick) + PartialTick(15, tick, 0.1), ticks.Count);
        check.Seconds(tick, ticks[1].Time.TotalSeconds - ticks[0].Time.TotalSeconds);
    }

    /// <summary>
    /// With Gushing Blood, From the Shadows and Bloodrush, a Hemorrhaging Strike during Maiden of Death gives the struck enemy and the
    /// other enemy a bleed whose period has From the Shadows' 0.87 twice: 3 x 0.87 x 0.87 / 1.2 seconds shortened by haste, first tick
    /// included, against 3 x 0.87 / 1.2 outside Maiden of Death.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Gushing_Blood_Copies_Take_From_The_Shadows_Twice(double haste)
    {
        double tick = 3 * 0.87 * 0.87 / 1.2 / (1 + haste);
        var f = StepFights.Mara(haste: haste, enemies: 2,
            talents: [GushingBlood.TalentId, Bloodrush.TalentId], legendaries: [FromTheShadows.CapeItemId]);
        f.ForceProcs(key => key == FromTheShadows.ProcKey ? false : null); // no clone
        var struck = f.Enemies()[0];
        var other = f.Enemies()[1];

        f.Cast<MaidenOfDeath>();
        f.Set(Mara.ComboPoints, 1); // a 15 second bleed
        f.Cast<HemorrhagingStrike>(struck);
        double strike = f.CompletedAt("Hemorrhaging Strike");
        f.Wait(5); // two ticks

        using var check = new Checks(f);
        foreach (var enemy in new[] { struck, other })
        {
            var ticks = f.Damage("Hemorrhage").Where(hit => hit.Target == enemy.Name).ToList();
            check.True(ticks.Count >= 2, what: $"two ticks on {enemy.Name}");
            check.Seconds(strike + tick, ticks[0].Time.TotalSeconds, what: $"first tick on {enemy.Name}");
            check.Seconds(tick, ticks[1].Time.TotalSeconds - ticks[0].Time.TotalSeconds, what: $"tick period on {enemy.Name}");
        }
    }

    /// <summary>
    /// With Gushing Blood and From the Shadows, the bleed a Hemorrhaging Strike during Maiden of Death puts on a second enemy has From the
    /// Shadows' 0.87 twice (the engine's way of showing the game's bug): its ticks come every 3 x 0.87 x 0.87 seconds shortened by haste,
    /// against 3 x 0.87 on the struck enemy.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void From_The_Shadows_Applies_Twice_To_The_Bleed_Gushing_Blood_Copies(double haste)
    {
        var f = StepFights.Mara(haste: haste, enemies: 2, talents: [GushingBlood.TalentId], legendaries: [FromTheShadows.CapeItemId]);
        f.ForceProcs(key => key == FromTheShadows.ProcKey ? false : null); // no clone
        var struck = f.Enemies()[0];
        var other = f.Enemies()[1];

        f.Cast<MaidenOfDeath>();
        f.Set(Mara.ComboPoints, 1);
        f.Cast<HemorrhagingStrike>(struck);
        f.Wait(5); // two ticks

        double Period(EnemyTarget enemy)
        {
            var ticks = f.Damage("Hemorrhage").Where(hit => hit.Target == enemy.Name).ToList();
            return (ticks[1].Time - ticks[0].Time).TotalSeconds;
        }

        using var check = new Checks(f);
        check.Seconds(3 * 0.87 / (1 + haste), Period(struck), what: "the struck enemy's tick period");
        check.Seconds(3 * 0.87 * 0.87 / (1 + haste), Period(other), what: "the copy's tick period");
    }

    // ---- Vexira's Venom

    /// <summary>
    /// With Vexira's Venom, your Queen's Fang and Arachnid Assault critical strikes apply Vexira's Venom to the enemy; the same finishers
    /// without a crit, and other abilities' crits, do not.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang", true, true)]
    [InlineData("Queen's Fang", false, false)]
    [InlineData("Arachnid Assault", true, true)]
    [InlineData("Arachnid Assault", false, false)]
    [InlineData("Hemorrhaging Strike", true, false)]
    [InlineData("Backstab", true, false)]
    public void Vexiras_Venom_Comes_From_Queens_Fang_And_Arachnid_Assault_Crits(string ability, bool crit, bool venom)
    {
        var f = StepFights.Mara(legendaries: [VexirasVenom.WristItemId]);
        f.Set(Mara.ComboPoints, 3); // enough to cast a finisher

        f.Cast(ability, crit: crit);

        Assert.Equal(venom, f.HasDebuff("Vexira's Venom", on: f.Enemy));
    }

    /// <summary>
    /// Vexira's Venom deals 28% of the crit's damage as poison over its 6 seconds, in 3 ticks' worth: each tick is 0.28 x crit / 3.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang")]
    [InlineData("Arachnid Assault")]
    public void Vexiras_Venom_Ticks_Deal_28_Percent_Of_The_Crit_Over_3(string ability)
    {
        var f = StepFights.Mara(legendaries: [VexirasVenom.WristItemId]).WithoutDamageSpread();
        f.Set(Mara.ComboPoints, 3); // enough to cast a finisher

        f.Cast(ability, crit: true);
        f.Wait(3); // the first tick
        double critDamage = f.Damage(ability).Single().Value;
        var ticks = f.Ticks(VexirasVenom.EffectId);

        AssertRatio(0.28 / 3, ticks[0].Value / critDamage);
    }

    /// <summary>
    /// Vexira's Venom ticks every 2 seconds shortened by haste, for 6 seconds that haste does not shorten: floor(6 / tick) full ticks plus
    /// a partial one for the time left (at least 0.1 seconds), whose damage is that fraction of a tick, so all the ticks add up to 6 x (1 +
    /// haste) / 2 full ticks; the first tick comes one tick after the crit.
    /// </summary>
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void Vexiras_Venom_Ticks_Every_Hasted_2_Seconds_For_6_Seconds_With_A_Partial(double haste)
    {
        double tick = 2 / (1 + haste);
        var f = StepFights.Mara(haste: haste, legendaries: [VexirasVenom.WristItemId]).WithoutDamageSpread();
        f.Set(Mara.ComboPoints, 3); // enough to cast Queen's Fang

        f.Cast<QueensFang>(crit: true);
        f.Wait(8); // the venom is over
        double critTime = f.Damage<QueensFang>().Single().Time.TotalSeconds;
        var ticks = f.Ticks(VexirasVenom.EffectId);

        using var check = new Checks(f);
        check.Equal(FullTicks(6, tick) + PartialTick(6, tick, 0.1), ticks.Count);
        check.Equal(6 * (1 + haste) / 2, ticks.Sum(t => t.Value) / ticks[0].Value, 0.03);
        check.Seconds(critTime + tick, ticks[0].Time.TotalSeconds);
    }

    /// <summary>Vexira's Venom lasts 6 seconds from the crit that applied it.</summary>
    [Fact]
    public void Vexiras_Venom_Lasts_6_Seconds()
    {
        var f = StepFights.Mara(legendaries: [VexirasVenom.WristItemId]);
        f.Set(Mara.ComboPoints, 3); // enough to cast Queen's Fang

        f.Cast<QueensFang>(crit: true);
        double end = f.Damage<QueensFang>().Single().Time.TotalSeconds + 6.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Vexira's Venom", on: f.Enemy));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Vexira's Venom", on: f.Enemy));
    }

    /// <summary>
    /// A new crit on an enemy with Vexira's Venom restarts its 6 seconds: after a second Queen's Fang crit 3 seconds into the venom, it
    /// lasts 6 seconds from the second crit.
    /// </summary>
    [Fact]
    public void Vexiras_Venom_Restarts_Its_6_Seconds_On_A_New_Crit()
    {
        var f = StepFights.Mara(legendaries: [VexirasVenom.WristItemId]);
        f.Set(Mara.ComboPoints, 3); // enough to cast Queen's Fang

        f.Cast<QueensFang>(crit: true);
        f.WaitUntil(f.Damage<QueensFang>().Single().Time.TotalSeconds + 3.0);
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>(crit: true);
        double end = f.Damage<QueensFang>().Last().Time.TotalSeconds + 6.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Vexira's Venom", on: f.Enemy));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Vexira's Venom", on: f.Enemy));
    }

    /// <summary>
    /// A new crit on an enemy with Vexira's Venom keeps the tick timing: with a second Queen's Fang crit 2.5 seconds into the venom, the
    /// next tick still comes 4 seconds after the first crit, not 2 seconds after the second.
    /// </summary>
    [Fact]
    public void Vexiras_Venom_Keeps_Its_Tick_Timing_On_A_New_Crit()
    {
        var f = StepFights.Mara(legendaries: [VexirasVenom.WristItemId]);
        f.Set(Mara.ComboPoints, 3); // enough to cast Queen's Fang

        f.Cast<QueensFang>(crit: true);
        double firstCrit = f.Damage<QueensFang>().Single().Time.TotalSeconds;
        f.WaitUntil(firstCrit + 2.5);
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>(crit: true);
        double secondCrit = f.Damage<QueensFang>().Last().Time.TotalSeconds;
        f.Wait(3); // the next tick
        var nextTick = f.Ticks(VexirasVenom.EffectId).First(t => t.Time.TotalSeconds > secondCrit);

        AssertSeconds(firstCrit + 4.0, nextTick.Time.TotalSeconds);
    }

    /// <summary>
    /// The shadowy clones' crits add no Vexira's Venom: during Matriarch Macabre, a Queen's Fang crit whose clones crit too gives the same
    /// venom ticks, against the crit, as one whose clones do not crit.
    /// </summary>
    [Fact]
    public void Vexiras_Venom_Gets_Nothing_From_The_Shadowy_Clones_Crits()
    {
        (double TickOverCrit, bool ClonesCrit) Venom(double crit)
        {
            var f = StepFights.Mara(crit: crit, legendaries: [VexirasVenom.WristItemId]).WithoutDamageSpread();
            f.Set(Hero.SpiritPoints, 100); // enough to cast Matriarch Macabre

            f.Cast<MatriarchMacabre>();
            f.Set(Mara.ComboPoints, 3);
            f.Cast<QueensFang>(crit: true);
            f.Wait(3); // the clones strike, then the first tick
            double critDamage = f.Damage<QueensFang>().Single().Value;
            var clones = f.Damage("Queen's Fang (shadowy clone)");

            return (f.Ticks(VexirasVenom.EffectId)[0].Value / critDamage, clones.Count == 2 && clones.All(hit => hit.IsCrit));
        }

        var critClonesScenario = Venom(1.0);
        var plainClonesScenario = Venom(0.0);

        using var check = new Checks();
        check.True(critClonesScenario.ClonesCrit);
        check.False(plainClonesScenario.ClonesCrit);
        check.Ratio(1.0, critClonesScenario.TickOverCrit / plainClonesScenario.TickOverCrit);
    }

    /// <summary>
    /// Vexira's Venom ticks never crit: with 100% crit chance, none of its ticks is a critical strike.
    /// </summary>
    [Fact]
    public void Vexiras_Venom_Ticks_Never_Crit()
    {
        var f = StepFights.Mara(crit: 1.0, legendaries: [VexirasVenom.WristItemId]);
        f.Set(Mara.ComboPoints, 3); // enough to cast Queen's Fang

        f.Cast<QueensFang>();
        f.Wait(8); // the venom is over
        var ticks = f.Ticks(VexirasVenom.EffectId);

        using var check = new Checks(f);
        check.True(ticks.Count > 0);
        foreach (var tick in ticks) check.False(tick.IsCrit, what: "a venom tick is a crit");
    }

    // ---- Drenched in Blood

    /// <summary>A bare Mara with Drenched in Blood whose Queen's Fang always gets a Spirit Refund; a Queen's Fang is cast with
    /// <see cref="CastRefundingQueensFang"/>.</summary>
    private static StepDriver DrenchedInBloodFight()
    {
        var f = StepFights.Mara(spirit: 0.2, legendaries: [DrenchedInBlood.RingItemId]);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        return f;
    }

    /// <summary>Casts a Queen's Fang with a Spirit Refund; returns the second the refund came.</summary>
    private static double CastRefundingQueensFang(StepDriver f)
    {
        f.Set(Mara.ComboPoints, 6); // enough to cast it
        f.Set(Mara.Energy, 100);
        f.Cast<QueensFang>();
        f.Wait(0.5); // the refund lands
        return f.Gains("Spirit").Last().Time.TotalSeconds;
    }

    /// <summary>A Spirit Refund grants Drenched in Blood.</summary>
    [Fact]
    public void A_Spirit_Refund_Grants_Drenched_In_Blood()
    {
        var f = DrenchedInBloodFight();
        Assert.False(f.Has(DrenchedInBlood.Buff));

        CastRefundingQueensFang(f);

        Assert.True(f.Has(DrenchedInBlood.Buff));
    }

    /// <summary>Drenched in Blood gives 24% Expertise.</summary>
    [Fact]
    public void Drenched_In_Blood_Gives_24_Percent_Expertise()
    {
        var f = DrenchedInBloodFight();
        double before = f.Expertise();

        CastRefundingQueensFang(f);

        AssertRatio(0.24, f.Expertise() - before);
    }

    /// <summary>Drenched in Blood lasts 8 seconds from the Spirit Refund.</summary>
    [Fact]
    public void Drenched_In_Blood_Lasts_8_Seconds()
    {
        var f = DrenchedInBloodFight();

        double refund = CastRefundingQueensFang(f);

        f.WaitUntil(refund + 8 - 0.1);
        Assert.True(f.Has(DrenchedInBlood.Buff));
        f.WaitUntil(refund + 8 + 0.1);
        Assert.False(f.Has(DrenchedInBlood.Buff));
    }

    /// <summary>
    /// A second Spirit Refund restarts Drenched in Blood's 8 seconds and does not stack its Expertise: 4 seconds after the first, the second
    /// leaves it 8 seconds from itself, at the same 24%.
    /// </summary>
    [Fact]
    public void A_Second_Refund_Restarts_Drenched_In_Blood_Without_Stacking_It()
    {
        var f = DrenchedInBloodFight();
        double before = f.Expertise();

        double first = CastRefundingQueensFang(f);
        f.WaitUntil(first + 4);
        double second = CastRefundingQueensFang(f);

        using var check = new Checks(f);
        check.Ratio(0.24, f.Expertise() - before);
        f.WaitUntil(first + 8 + 0.1); // the first one's end
        check.True(f.Has(DrenchedInBlood.Buff), "still up after the first refund's 8 seconds");
        f.WaitUntil(second + 8 - 0.1);
        check.True(f.Has(DrenchedInBlood.Buff), "up until 8 seconds after the second");
        f.WaitUntil(second + 8 + 0.1);
        check.False(f.Has(DrenchedInBlood.Buff), "gone 8 seconds after the second");
    }
}
