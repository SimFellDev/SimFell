using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// <see cref="PeriodicEffect.Refresh"/> restarts a hasted effect's duration and keeps its period timer: the tick after the
/// refresh comes when it was due.
/// </summary>
public class PeriodicEffectRefreshTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Refresh_Keeps_The_Period_Timer(bool damageOverTime)
    {
        var hero = HeroImporter.Import(System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!);
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e9);
        hero.CurrentTarget = enemy;
        double period = 1.0 / (1.0 + hero.GetHaste(SimTime.Zero));

        var ticks = new List<double>();
        double? ended = null;
        var effect = PeriodicEffect.BeginHasted(ctx, hero, "Test Dot", 2.6 * period, 1.0, tickOnStart: false, partialFinalTick: false,
            onTick: (c, _) => ticks.Add(c.CurrentTime.TotalSeconds), onEnd: (c, _) => ended = c.CurrentTime.TotalSeconds,
            damageLogId: 1_999_003, target: enemy, damageOverTime: damageOverTime);
        // Halfway between the first and second ticks, the duration restarts at 2.6 periods: it now runs out at 4.1 periods.
        bool refreshed = false;
        ctx.Schedule(SimTime.FromSeconds(1.5 * period), c => refreshed = effect.Refresh(c, 2.6 * period), "refresh");

        ctx.RunUntil(SimTime.FromSeconds(10));

        Assert.True(refreshed);
        Assert.Equal(4, ticks.Count);
        for (int i = 0; i < ticks.Count; i++) Assert.Equal((i + 1) * period, ticks[i], 2);
        Assert.Equal(4.1 * period + AuraContainer.ExpiryLagSeconds, ended!.Value, 2);
        Assert.False(effect.Refresh(ctx, 1.0));
    }
}
