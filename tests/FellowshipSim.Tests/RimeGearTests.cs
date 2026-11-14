using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Skandi's Decree, Nature's Fury (Branch of the Everliving) and Voidbringer's Touch in a sim.</summary>
public class RimeGearTests
{
    [Fact]
    public void Skandis_Decree_Makes_Bursting_Ice_Pulse_For_Two_Seconds_More()
    {
        const string apl = "actions=bursting_ice\nactions+=/frost_bolt";
        int without = Hits(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 }, apl, h => h.GetAbility<BurstingIce>()!.DamageLogId);
        var build = new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, LegendaryItemIds = [SkandisDecree.LegsItemId] };
        // Game data: Skandi's Decree adds 2 s.
        Assert.Equal(2, HeroImporter.Import(build).GetAbility<BurstingIce>()!.BonusDurationSeconds);

        // Pulses on a fixed period, so a longer debuff has proportionally more pulses.
        int with = Hits(build, apl, h => h.GetAbility<BurstingIce>()!.DamageLogId);
        Assert.InRange((double)with / without, 1.5, 1.8);
    }

    [Fact]
    public void Natures_Fury_Bounces_Four_Times()
    {
        var build = new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, WeaponAbilityId = NaturesFury.AbilityId };
        var hero = HeroImporter.Import(build);
        Assert.IsType<NaturesFury>(hero.WeaponAbility);

        var hits = new List<(double Time, double Amount, bool Crit)>();
        hero.OnDamageDealt += (in dmg) => { if (dmg.AbilityId == NaturesFury.AbilityId) hits.Add((dmg.Timestamp.TotalSeconds, dmg.Amount, dmg.IsCrit)); };
        new MonteCarloSimulator(() => hero, AplParser.ParseScript("actions=natures_fury\nactions+=/frost_bolt"))
            .Run(SimTime.FromSeconds(30), 1, NaturesFury.Hits, degreeOfParallelism: 1);

        // One cast in the run, since the cooldown outlasts it: one hit on each enemy, a bounce period apart.
        Assert.Equal(NaturesFury.Hits, hits.Count);
        for (int i = 1; i < hits.Count; i++) Assert.Equal(NaturesFury.BounceSeconds, hits[i].Time - hits[i - 1].Time, 3);
    }

    [Fact]
    public void Voidbringers_Touch_Is_A_Debuff_On_Its_Target_Until_It_Erupts()
    {
        var vocabulary = AplVocabulary.For("Rime");
        var touched = AplParser.ParseExpression("debuff.voidbringers_touch.up");
        Assert.Equal(AplTokenKind.Debuff, vocabulary.Classify((PropertyExpression)touched).Kind);

        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, WeaponAbilityId = VoidbringersTouch.AbilityId });
        int frostBolt = hero.GetAbility<FrostBolt>()!.DamageLogId;
        var bolts = new List<(double Time, int Stacks)>();
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.AbilityId == frostBolt) bolts.Add((dmg.Timestamp.TotalSeconds, dmg.Target!.Auras.GetStacks(VoidbringersTouch.Debuff, dmg.Timestamp)));
        };
        new MonteCarloSimulator(() => hero, AplParser.ParseScript("actions=voidbringers_touch,if=!debuff.voidbringers_touch.up\nactions+=/frost_bolt"))
            .Run(SimTime.FromSeconds(40), 1, 1, degreeOfParallelism: 1);

        // Frost Bolts store far short of the cap, so the touch erupts when its duration runs out.
        // Game data: Voidbringer's Touch lasts 15 s.
        Assert.Contains(bolts, b => b.Time < 15);
        Assert.Contains(bolts, b => b.Time > 15);
        Assert.All(bolts, b => Assert.Equal(b.Time < 15 ? 1 : 0, b.Stacks));
    }

    private static int Hits(CharacterBuild build, string apl, Func<Hero, int> damageId)
    {
        var hero = HeroImporter.Import(build);
        int id = damageId(hero), hits = 0;
        hero.OnDamageDealt += (in dmg) => { if (dmg.AbilityId == id) hits++; };
        new MonteCarloSimulator(() => hero, AplParser.ParseScript(apl)).Run(SimTime.FromSeconds(120), 1, 1, degreeOfParallelism: 1);
        return hits;
    }
}
