using FellowshipSim.Core.APL;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>A sim's report: damage over time, each damage source, and each buff and debuff.</summary>
public class SimReportTests
{
    private const int Seconds = 60;
    private const int Fights = 50;

    private static MonteCarloResult Run(int targets = 1) =>
        new MonteCarloSimulator(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000, CritRating = 400 },
                AplParser.ParseScript("actions=bursting_ice\nactions+=/frost_bolt"))
            .Run(SimTime.FromSeconds(Seconds), Fights, targets, degreeOfParallelism: 2);

    [Fact]
    public void The_Timeline_Ends_At_The_Mean_Damage_Inside_Its_Band()
    {
        var result = Run();
        var timeline = result.Timeline!;
        Assert.Equal(Seconds, timeline.Damage.Count);
        Assert.Equal(result.TotalMeanDamage, timeline.Damage[^1], result.TotalMeanDamage * 1e-9);
        // The band holds the middle 90% of fights (early on most have dealt nothing yet, so the mean can lie above it).
        for (int s = 0; s < Seconds; s++)
        {
            Assert.True(timeline.DamageLow[s] <= timeline.DamageHigh[s]);
            Assert.True(timeline.DpsLow[s] <= timeline.DpsHigh[s]);
        }
        // Fights in a sim differ little (each hit deals its expected damage), and the band is kept in single precision.
        double slack = timeline.Damage[^1] * 0.001;
        Assert.InRange(timeline.Damage[^1], timeline.DamageLow[^1] - slack, timeline.DamageHigh[^1] + slack);
        // The last 10 s's rolling DPS is that window's damage over 10 s.
        Assert.Equal((timeline.Damage[^1] - timeline.Damage[^11]) / timeline.RollingSeconds, timeline.Dps[^1], 6);
    }

    [Fact]
    public void Each_Source_Adds_Up_To_The_Fights_Damage()
    {
        var result = Run();
        Assert.Equal(result.TotalMeanDamage, result.Abilities.Sum(a => a.Damage), result.TotalMeanDamage * 1e-9);
        var bolt = result.Abilities.Single(a => a.Name == "Frost Bolt");
        Assert.True(bolt.Casts > 0 && bolt.Hits >= bolt.Casts - 1);
        Assert.InRange(bolt.CritChance, 0.05, 1.0);
        Assert.Equal(bolt.Damage / bolt.Hits, bolt.AverageHit, 6);
        Assert.Equal(bolt.Damage / Seconds, bolt.Dps, 6);
    }

    [Fact]
    public void Buffs_And_Debuffs_Report_Their_Uptime_And_Applications()
    {
        var result = Run();
        var embrace = result.Buffs.Single(b => b.Name == BurstingIce.WintersEmbrace.Name && !b.OnTarget);
        var debuff = result.Buffs.Single(b => b.Name == BurstingIce.Debuff.Name && b.OnTarget);
        // Bursting Ice every 10 s, its debuff (and Winter's Embrace) 3 s at a time.
        Assert.InRange(debuff.Uptime, 0.2, 0.45);
        Assert.InRange(debuff.Applications, 4, 7);
        Assert.InRange(debuff.AverageDuration, 2.5, 3.5);
        Assert.InRange(embrace.Uptime, 0.2, 0.45);
        Assert.True(debuff.AverageGap > 5);
        // The typical fight's timeline: ordered spans inside the fight, covering about its uptime.
        var spans = debuff.Spans!;
        Assert.True(spans.Count >= 2 && spans.Count % 2 == 0);
        for (int i = 1; i < spans.Count; i++) Assert.True(spans[i] >= spans[i - 1]);
        Assert.InRange(spans[^1], 0, Seconds);
        double covered = Enumerable.Range(0, spans.Count / 2).Sum(i => spans[2 * i + 1] - spans[2 * i]);
        Assert.InRange(covered / Seconds, debuff.Uptime - 0.1, debuff.Uptime + 0.1);
        // Buffs come first.
        Assert.True(result.Buffs.TakeWhile(b => !b.OnTarget).Count() + result.Buffs.SkipWhile(b => !b.OnTarget).Count() == result.Buffs.Count);
        Assert.DoesNotContain(result.Buffs.SkipWhile(b => !b.OnTarget), b => !b.OnTarget);
    }
}
