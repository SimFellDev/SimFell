using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Skandi's Decree and Nature's Fury (Branch of the Everliving) in a sim.</summary>
public class RimeGearTests
{
    [Fact]
    public void Skandis_Decree_Makes_Bursting_Ice_Pulse_For_Two_Seconds_More()
    {
        const string apl = "actions=bursting_ice\nactions+=/frost_bolt";
        int without = Hits(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 }, apl, h => h.GetAbility<BurstingIce>()!.DamageLogId);
        var build = new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, LegendaryItemIds = [SkandisDecree.LegsItemId] };
        Assert.Equal(SkandisDecree.BurstingIceBonusSeconds, HeroImporter.Import(build).GetAbility<BurstingIce>()!.BonusDurationSeconds);

        // Pulses every 0.5s: 3s of debuff is 6 pulses, 5s is 10.
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
            .Run(SimTime.FromSeconds(30), 1, 3, degreeOfParallelism: 1);

        // One cast in 30s (cooldown 60s): four hits, 0.3s apart.
        Assert.Equal(NaturesFury.Hits, hits.Count);
        for (int i = 1; i < hits.Count; i++) Assert.Equal(NaturesFury.BounceSeconds, hits[i].Time - hits[i - 1].Time, 3);
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
