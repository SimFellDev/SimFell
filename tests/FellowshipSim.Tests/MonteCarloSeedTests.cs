using FellowshipSim.Core.APL;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Monte Carlo fights: each fight has its own seed, and a seed repeats its fight exactly.</summary>
public class MonteCarloSeedTests
{
    private const string Apl = """
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

    [Fact]
    public void Each_Fight_Has_Its_Own_Seed()
    {
        var result = Run(iterations: 10, firstSeed: 1);
        Assert.True(result.StandardDeviation > 0);
        Assert.Equal(10, result.IterationDamage.Distinct().Count());
    }

    [Fact]
    public void A_Seed_Repeats_Its_Fight()
    {
        var first = Run(iterations: 5, firstSeed: 3);
        var again = Run(iterations: 5, firstSeed: 3);
        Assert.Equal(first.IterationDamage, again.IterationDamage);
    }

    private static MonteCarloResult Run(int iterations, int firstSeed)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        var hero = HeroImporter.Import(build);
        return new MonteCarloSimulator(hero, AplParser.ParseScript(Apl)).Run(SimTime.FromSeconds(120), iterations, targetCount: 3, firstSeed);
    }
}
