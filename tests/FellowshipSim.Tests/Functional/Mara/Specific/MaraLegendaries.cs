using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's legendary items, each worn on a bare Mara against one enemy: From the Shadows (her cape) and Vexira's Venom (her wristbands).
/// Any legendary also gives Mote of Creation's +10% main stat, so the damage tests wear the legendary in both fights.
/// </summary>
public class MaraLegendaries
{
    // ---- From the Shadows

    /// <summary>
    /// With From the Shadows, Hemorrhaging Strike's bleed ticks 15% faster, for the same duration: every 3 / 1.15 seconds shortened by
    /// haste, floor(15 / tick) full ticks over the 15 seconds of a 1 Combo Point bleed, plus a partial one for the time left (at least 0.1
    /// seconds).
    ///
    /// From the engine (FromTheShadows.cs, the tooltip's "ticks faster" with 15% from the game data), marked UNVERIFIED: no log has this
    /// legendary. TODO: confirm the 15% in a log with From the Shadows.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void From_The_Shadows_Makes_The_Bleed_Tick_15_Percent_Faster(double haste)
    {
        double tick = 3 / 1.15 / (1 + haste);
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
    ///
    /// From the engine (FromTheShadows.cs, game data), marked UNVERIFIED: no log has this legendary. TODO: confirm the 15% in a log.
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
    ///
    /// From the engine (FromTheShadows.cs, the tooltip), marked UNVERIFIED: no log has this legendary, and the clone's strike is taken to
    /// come at once. TODO: confirm how a log shows the clone's Queen's Fang.
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
    ///
    /// Source: game data (28%, 6 seconds, 2 second ticks); combat logs (NOTES.md, Vexira's Venom: the first crit gives ticks of 28% of it
    /// / 3).
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
    ///
    /// Source: game data and combat logs (NOTES.md, Vexira's Venom: ticks every 2s shortened by Haste, with a partial tick as it runs
    /// out, which reads its progress on the server frame it fires, up to 1/30 second after the 6 seconds). The sum of the ticks over a
    /// full tick is checked within 0.03, the most that frame adds at these hastes.
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
    ///
    /// Source: combat logs (NOTES.md, Vexira's Venom: a new crit restarts its 6 seconds).
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
    ///
    /// Source: combat logs (NOTES.md, Vexira's Venom: a new crit restarts its 6 seconds and keeps the tick timing).
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
    ///
    /// Source: game data (NOTES.md, Vexira's Venom: the venom's ability rejects the clones' damage). The clones crit with a hero at 100%
    /// crit, and not with a hero at 0% whose own Queen's Fang is forced to crit.
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
    ///
    /// Source: game data (effect 2364's preset Empty, NOTES.md).
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
}
