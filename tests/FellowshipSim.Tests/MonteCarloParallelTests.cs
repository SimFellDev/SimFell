using FellowshipSim.Core.APL;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Monte Carlo fights on several threads: fight i has the same total at every degree of parallelism, a worker's reused
/// hero fights as a fresh hero, and the merged totals equal the totals of the fights.
/// </summary>
public class MonteCarloParallelTests
{
    private const string Apl = """
        actions+=/chronoshift
        actions+=/flight_of_the_navir
        actions+=/ice_blitz
        actions+=/winters_blessing
        actions+=/ice_comet
        actions+=/glacial_blast
        actions+=/freezing_torrent
        actions+=/bursting_ice
        actions+=/cold_snap
        actions+=/frost_bolt
        """;

    private const int Seconds = 120;
    private const int Targets = 3;

    [Fact]
    public void Fight_I_Has_The_Same_Total_At_Every_Degree_Of_Parallelism()
    {
        const int fights = 96;
        var (single, _) = Run(fights, degreeOfParallelism: 1);
        int mostHeroes = 0;
        foreach (int threads in new[] { 2, 4, 8 })
        {
            var (result, heroes) = Run(fights, threads);
            mostHeroes = Math.Max(mostHeroes, heroes);
            Assert.Equal(single.IterationDamage, result.IterationDamage);
        }
        // A run may use fewer threads than it allows: on a busy machine the first can fight every chunk before another
        // starts. One of the runs used several, so the comparison covers fights spread over threads.
        Assert.True(mostHeroes > 1, $"Every run built {mostHeroes} hero");
    }

    [Fact]
    public void A_Workers_Reused_Hero_Fights_As_A_Fresh_Hero()
    {
        const int fights = 32;
        var (result, _) = Run(fights, degreeOfParallelism: 4);
        for (int i = 0; i < fights; i++)
        {
            var (fresh, _) = Run(1, degreeOfParallelism: 1, firstSeed: 1 + i);
            Assert.Equal(fresh.IterationDamage[0], result.IterationDamage[i]);
        }
    }

    [Fact]
    public void Merged_Totals_Equal_The_Totals_Of_The_Fights()
    {
        const int fights = 96;
        var (result, _) = Run(fights, degreeOfParallelism: 8);
        var dps = result.IterationDamage.Select(d => d / Seconds).ToList();
        double mean = dps.Average();
        double deviation = Math.Sqrt(dps.Sum(d => (d - mean) * (d - mean)) / fights);
        Assert.Equal(mean, result.MeanDps, mean * 1e-12);
        Assert.Equal(deviation, result.StandardDeviation, deviation * 1e-9);
        Assert.Equal(dps.Min(), result.MinDps);
        Assert.Equal(dps.Max(), result.MaxDps);
        Assert.Equal(mean, result.AbilityDpsBreakdown.Values.Sum(), mean * 1e-12);
    }

    private static (MonteCarloResult Result, int Heroes) Run(int fights, int degreeOfParallelism, int firstSeed = 1)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        int heroes = 0;
        var simulator = new MonteCarloSimulator(() =>
        {
            Interlocked.Increment(ref heroes);
            return HeroImporter.Import(build);
        }, AplParser.ParseScript(Apl));
        var result = simulator.Run(SimTime.FromSeconds(Seconds), fights, Targets, firstSeed, degreeOfParallelism, keepFightDamage: true);
        return (result, heroes);
    }
}
