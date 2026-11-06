using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Amethyst Splinter is a damage over time: its duration ends with a partial tick, and the next crit starts a new rhythm.</summary>
public class AmethystSplintersTests
{
    private const int CritLogId = 999_999;

    /// <summary>The splinter ticks of crits at <paramref name="crits"/> on one enemy, until 20s.</summary>
    private static List<(double Time, double Amount)> Ticks(params double[] crits) => Ticks(diedAt: null, crits);

    private static List<(double Time, double Amount)> Ticks(double? diedAt, params double[] crits)
    {
        var hero = HeroImporter.Import(new CharacterBuild
        {
            Hero = "Mara",
            PrimaryStat = 1000,
            Augmentations = [new BuildEntry(AmethystSplinters.AspectId, 4)],
        });
        var ctx = new SimulationContext(hero);
        var target = ctx.AddEnemy(101, 1, "Training Dummy 1", maxHealth: 100_000_000);
        hero.CurrentTarget = target;
        var ticks = new List<(double Time, double Amount)>();
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.AbilityId == LogIds.Effect(AmethystSplinters.DotEffectId)) ticks.Add((ctx.CurrentTime.TotalSeconds, dmg.Amount));
        };
        foreach (double crit in crits)
            ctx.ScheduleAt(SimTime.FromSeconds(crit), c => c.ApplyDamage(hero, target, 1000.0, CritLogId, "Crit", canCrit: true, 1.0, 1.0, forcedCrit: true));
        if (diedAt is { } died) ctx.ScheduleAt(SimTime.FromSeconds(died), hero.TriggerDied);
        ctx.RunUntil(SimTime.FromSeconds(20));
        return ticks;
    }

    [Fact]
    public void A_Crit_Keeps_The_Rhythm_And_The_Eight_Seconds_End_With_A_Partial_Tick()
    {
        // The crit restarts the duration: ticks on its period, and a part of a period's progress at the end.
        var ticks = Ticks(0.0, 1.0);

        Assert.Equal([2.0, 4.0, 6.0, 8.0, 9.0 + AuraContainer.ExpiryLagSeconds], ticks.Select(t => Math.Round(t.Time, 3)), (a, b) => Math.Abs(a - b) < 0.002);
        // A simulated hit rolls the engine's damage spread.
        Assert.InRange(ticks[4].Amount / ticks[3].Amount, 0.4, 0.62);
    }

    [Fact]
    public void The_Heros_Death_Ends_The_Splinter_With_No_Partial_Tick()
    {
        var ticks = Ticks(diedAt: 5.0, crits: 0.0);

        Assert.Equal([2.0, 4.0], ticks.Select(t => Math.Round(t.Time, 3)), (a, b) => Math.Abs(a - b) < 0.002);
    }

    [Fact]
    public void A_Crit_After_The_Eight_Seconds_Starts_A_New_Rhythm()
    {
        var ticks = Ticks(0.0, 1.0, 9.5);

        // The new rhythm's first tick is one period after the crit.
        Assert.Equal(11.5, ticks[5].Time, 3);
    }
}
