using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Al'zerac's Shackle, Sahril's Aegis and Zeraleth's Hunger in a sim, on Rime with each weapon equipped.</summary>
public class TankWeaponTests
{
    [Fact]
    public void Alzeracs_Shackle_Freezes_Then_Explodes_And_Slows()
    {
        var hits = Hits(AlzeracsShackle.AbilityId, "alzeracs_shackle", seconds: 25);
        var freeze = hits.Where(h => h.Id == LogIds.Effect(AlzeracsShackle.FreezeEffectId)).ToList();
        var explosion = hits.Where(h => h.Id == AlzeracsShackle.AbilityId).ToList();
        var slow = hits.Where(h => h.Id == LogIds.Effect(AlzeracsShackle.SlowEffectId)).ToList();

        // One cast in 25s (cooldown 30s) on one enemy: 3s of Freeze ticking every 1.5s, the explosion as it ends, then
        // 15s of Slow ticking every 2s.
        Assert.Equal(2, freeze.Count);
        var explodes = Assert.Single(explosion);
        Assert.Equal(AlzeracsShackle.FreezeLandSeconds + AlzeracsShackle.FreezeSeconds, explodes.Time, 1);
        Assert.True(freeze.All(f => f.Time <= explodes.Time));
        Assert.InRange(slow.Count, 7, 8);
        Assert.True(slow.All(s => s.Time > explodes.Time && s.Time <= explodes.Time + AlzeracsShackle.SlowSeconds + 0.1));
    }

    [Fact]
    public void Sahrils_Aegis_Explodes_When_The_Shield_Expires()
    {
        var hits = Hits(SahrilsAegis.AbilityId, "sahrils_aegis", seconds: 30);
        var explodes = Assert.Single(hits, h => h.Id == SahrilsAegis.AbilityId);
        Assert.Equal(SahrilsAegis.ShieldSeconds, explodes.Time, 1);
        Assert.True(explodes.Amount > 0);
    }

    [Fact]
    public void Zeraleths_Hunger_Channels_Six_Ticks()
    {
        var hits = Hits(ZeralethsHunger.AbilityId, "zeraleths_hunger", seconds: 10);
        var ticks = hits.Where(h => h.Id == ZeralethsHunger.AbilityId).ToList();
        Assert.Equal((int)(ZeralethsHunger.ChannelSeconds / ZeralethsHunger.TickPeriodSeconds), ticks.Count);
        Assert.All(ticks, t => Assert.True(t.Amount > 0));
    }

    private static List<(int Id, double Time, double Amount)> Hits(int weaponAbilityId, string action, double seconds)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, WeaponAbilityId = weaponAbilityId });
        Assert.Equal(weaponAbilityId, hero.WeaponAbility!.Id);
        var hits = new List<(int Id, double Time, double Amount)>();
        hero.OnDamageDealt += (in dmg) => hits.Add((dmg.AbilityId, dmg.Timestamp.TotalSeconds, dmg.Amount));
        new MonteCarloSimulator(() => hero, AplParser.ParseScript($"actions={action}\nactions+=/frost_bolt"))
            .Run(SimTime.FromSeconds(seconds), 1, 1, degreeOfParallelism: 1);
        return hits;
    }
}
