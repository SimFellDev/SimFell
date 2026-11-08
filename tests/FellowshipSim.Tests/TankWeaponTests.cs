using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Aspects;
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
        Assert.Equal(AlzeracsShackle.FreezeLandSeconds + AlzeracsShackle.Duration, explodes.Time, 1);
        Assert.True(freeze.All(f => f.Time <= explodes.Time));
        Assert.InRange(slow.Count, 7, 8);
        Assert.True(slow.All(s => s.Time > explodes.Time && s.Time <= explodes.Time + AlzeracsShackle.SlowSeconds + 0.1));
    }

    [Fact]
    public void Brave_Machinations_Raises_The_Crit_Chance_Of_Shackles_Ticks()
    {
        double SlowCritRate(List<BuildEntry> augmentations)
        {
            var hero = HeroImporter.Import(new CharacterBuild
            {
                Hero = "Rime", PrimaryStat = 1000, WeaponAbilityId = AlzeracsShackle.AbilityId, Augmentations = augmentations,
            });
            int slowId = LogIds.Effect(AlzeracsShackle.SlowEffectId), ticks = 0, crits = 0;
            hero.OnDamageDealt += (in dmg) =>
            {
                if (dmg.AbilityId != slowId) return;
                ticks++;
                if (dmg.IsCrit) crits++;
            };
            new MonteCarloSimulator(() => hero, AplParser.ParseScript("actions=alzeracs_shackle\nactions+=/frost_bolt"))
                .Run(SimTime.FromSeconds(25), 400, 1, degreeOfParallelism: 1);
            return (double)crits / ticks;
        }

        double without = SlowCritRate([]);
        double with = SlowCritRate([new BuildEntry(BraveMachinations.AspectId, 4)]);
        Assert.InRange(with - without, BraveMachinations.BuffAddedWeaponCritChanceByRank[3] - 0.05, BraveMachinations.BuffAddedWeaponCritChanceByRank[3] + 0.05);
    }

    [Fact]
    public void Sahrils_Aegis_Explodes_When_The_Shield_Expires()
    {
        var hits = Hits(SahrilsAegis.AbilityId, "sahrils_aegis", seconds: 30);
        var explodes = Assert.Single(hits, h => h.Id == SahrilsAegis.AbilityId);
        Assert.Equal(SahrilsAegis.Duration, explodes.Time, 1);
        Assert.True(explodes.Amount > 0);
    }

    [Fact]
    public void Zeraleths_Hunger_Channels_Six_Ticks()
    {
        var hits = Hits(ZeralethsHunger.AbilityId, "zeraleths_hunger", seconds: 10);
        var ticks = hits.Where(h => h.Id == ZeralethsHunger.AbilityId).ToList();
        Assert.Equal((int)(ZeralethsHunger.AbilityChannelTime / ZeralethsHunger.AbilityChannelTick), ticks.Count);
        Assert.All(ticks, t => Assert.True(t.Amount > 0));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Shackles_Slow_Ticks_On_An_Enemy_That_Dies_And_Stops_On_One_That_Leaves_Combat_Alive(bool dies)
    {
        // The enemy leaves combat 5s into its Slow.
        double left = AlzeracsShackle.FreezeLandSeconds + AlzeracsShackle.Duration + 5.0;
        var ticks = ShackleTicks(AlzeracsShackle.SlowEffectId, left, dies);

        Assert.Contains(ticks, t => t < left);
        if (dies)
        {
            Assert.Contains(ticks, t => t > left + 5.0);
            Assert.InRange(ticks.Count, 7, 8);
        }
        else Assert.DoesNotContain(ticks, t => t > left);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Shackles_Freeze_Ticks_On_An_Enemy_That_Dies_And_Stops_On_One_That_Leaves_Combat_Alive(bool dies)
    {
        // The enemy leaves combat 0.5s into its Freeze, before the first tick.
        double left = AlzeracsShackle.FreezeLandSeconds + 0.5;
        var ticks = ShackleTicks(AlzeracsShackle.FreezeEffectId, left, dies);

        if (dies) Assert.Contains(ticks, t => t > left);
        else Assert.Empty(ticks);
    }

    [Fact]
    public void A_Proc_Off_A_Shackle_Tick_On_A_Dead_Enemy_Misses()
    {
        int slowId = LogIds.Effect(AlzeracsShackle.SlowEffectId);
        int beamId = LogIds.Effect(DiamondStrike.DamageEffectId);
        // The enemy dies 1s into its Slow.
        double died = AlzeracsShackle.FreezeLandSeconds + AlzeracsShackle.Duration + 1.0;
        int ticksAfterDeath = 0, beamsBeforeDeath = 0, beamsAfterDeath = 0;
        for (int seed = 1; seed <= 100; seed++)
        {
            var hero = HeroImporter.Import(new CharacterBuild
            {
                Hero = "Rime", PrimaryStat = 1000, WeaponAbilityId = AlzeracsShackle.AbilityId,
                Augmentations = [new BuildEntry(DiamondStrike.AspectId, 4)],
            });
            var ctx = new SimulationContext(hero);
            ctx.Rng.Seed(seed);
            hero.ResetForIteration();
            hero.BeginFight(ctx);
            var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e9);
            hero.CurrentTarget = enemy;
            hero.OnDamageDealt += (in dmg) =>
            {
                bool after = dmg.Timestamp.TotalSeconds > died;
                if (dmg.AbilityId == slowId && after) ticksAfterDeath++;
                if (dmg.AbilityId == beamId && dmg.Amount > 0)
                {
                    if (after) beamsAfterDeath++;
                    else beamsBeforeDeath++;
                }
            };
            Assert.True(ctx.BeginCast(hero, hero.WeaponAbility!, enemy).Succeeded);
            ctx.Schedule(SimTime.FromSeconds(died), c => c.RemoveEnemy(enemy, defeated: true, died: true), "death");
            ctx.RunUntil(SimTime.FromSeconds(25));
        }

        Assert.True(ticksAfterDeath > 0);
        Assert.True(beamsBeforeDeath > 0);
        Assert.Equal(0, beamsAfterDeath);
    }

    /// <summary>Times of the Shackle's <paramref name="effectId"/> ticks with damage, the enemy leaving combat at <paramref name="left"/>.</summary>
    private static List<double> ShackleTicks(int effectId, double left, bool dies)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, WeaponAbilityId = AlzeracsShackle.AbilityId });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e9);
        hero.CurrentTarget = enemy;
        int id = LogIds.Effect(effectId);
        var ticks = new List<double>();
        hero.OnDamageDealt += (in dmg) => { if (dmg.AbilityId == id && dmg.Amount > 0) ticks.Add(dmg.Timestamp.TotalSeconds); };
        Assert.True(ctx.BeginCast(hero, hero.WeaponAbility!, enemy).Succeeded);
        ctx.Schedule(SimTime.FromSeconds(left), c => c.RemoveEnemy(enemy, defeated: dies, died: dies), "leave");
        ctx.RunUntil(SimTime.FromSeconds(25));
        return ticks;
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
