using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>A bad luck protected proc: its step from the tooltip's average, and a count of misses for each proc key.</summary>
public class BadLuckProtectionTests
{
    [Fact]
    public void The_Step_Of_A_35_Percent_Proc_Is_15_8_Percent()
    {
        Assert.Equal(0.158, BadLuckProtection.StepFor(0.35), 3);
        Assert.Equal(0.35, BadLuckProtection.AverageChance(BadLuckProtection.StepFor(0.35)), 6);
        Assert.Equal(1.0, BadLuckProtection.AverageChance(1.0), 6);
    }

    [Fact]
    public void Each_Key_Procs_At_Its_Average_And_Never_Misses_Past_A_Full_Chance()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Tariq", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        ctx.Rng.Seed(1);
        hero.ResetForIteration();
        double step = BadLuckProtection.StepFor(0.35);
        var procs = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 };
        var misses = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 };
        const int Rolls = 20000;
        for (int i = 0; i < Rolls; i++)
        {
            // "b" rolls twice for each roll of "a": a shared count would give them the same rate.
            foreach (string key in i % 3 == 0 ? new[] { "a" } : new[] { "b" })
            {
                if (hero.RollBadLuckProc(ctx, key, step))
                {
                    procs[key]++;
                    misses[key] = 0;
                }
                else
                {
                    Assert.True(++misses[key] < 7, $"{key} missed {misses[key]} times in a row");
                }
            }
        }
        Assert.InRange(procs["a"] / (Rolls / 3.0), 0.33, 0.37);
        Assert.InRange(procs["b"] / (Rolls * 2 / 3.0), 0.33, 0.37);
    }
}
