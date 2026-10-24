using FellowshipSim.Core.APL;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Monte Carlo fights: each fight has its own seed, a seed repeats its fight exactly, and a hero that has fought before
/// fights as a fresh one.
/// </summary>
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

    /// <summary>The fixture's weapon is Asha's Chronoshift Spire.</summary>
    private const string WeaponApl = "actions+=/chronoshift\n" + Apl;

    [Fact]
    public void Each_Fight_Has_Its_Own_Seed()
    {
        var result = Run(Apl, iterations: 10, firstSeed: 1);
        Assert.True(result.StandardDeviation > 0);
        Assert.Equal(10, result.IterationDamage.Distinct().Count());
    }

    [Fact]
    public void A_Seed_Repeats_Its_Fight()
    {
        var first = Run(Apl, iterations: 5, firstSeed: 3);
        var again = Run(Apl, iterations: 5, firstSeed: 3);
        Assert.Equal(first.IterationDamage, again.IterationDamage);
    }

    [Theory]
    [InlineData(false, 120, 3)]
    [InlineData(true, 300, 1)]
    public void A_Hero_After_A_Fight_Fights_As_A_Fresh_Hero(bool weapon, int seconds, int targets)
    {
        string apl = weapon ? WeaponApl : Apl;
        // One hero: fights 2-10 each follow another fight.
        var reused = Run(apl, iterations: 10, firstSeed: 1, seconds, targets);
        for (int i = 1; i < 10; i++)
        {
            Assert.Equal(Run(apl, iterations: 1, firstSeed: 1 + i, seconds, targets).IterationDamage[0], reused.IterationDamage[i]);
        }
    }

    /// <summary>Totals of fights 1-4 of each hero's default action list, captured at commit 8fecd72.</summary>
    [Theory]
    [InlineData("Rime", 120, 3, new[] { 3065348.164364021, 2844345.624924346, 2586790.2809684295, 2826773.541334825 })]
    [InlineData("Rime", 300, 1, new[] { 7872715.6917813085, 8752259.817411212, 8604763.571995698, 8861947.45083851 })]
    [InlineData("Ardeos", 120, 3, new[] { 1847837.1115456694, 2054980.736083649, 1936607.7508904785, 1953843.794033505 })]
    [InlineData("Ardeos", 300, 1, new[] { 3623429.388401441, 3545005.846612877, 3385237.950039354, 3227405.2486445424 })]
    [InlineData("Gunde", 120, 3, new[] { 3411873.057643108, 3676642.4235181487, 3663297.493065301, 3894994.09916709 })]
    [InlineData("Gunde", 300, 1, new[] { 6052496.876257931, 6029016.467322267, 5538107.514025447, 6347232.759195004 })]
    [InlineData("Mara", 120, 3, new[] { 1633623.2702913966, 1141078.5666773887, 1392297.1570090842, 1461812.038685851 })]
    [InlineData("Mara", 300, 1, new[] { 3786242.7066847594, 3396084.100169657, 3398590.5212257053, 3391223.1314318073 })]
    [InlineData("Tariq", 120, 3, new[] { 1453563.3278640977, 1552281.144037157, 1504112.8646380526, 1522586.9304606495 })]
    [InlineData("Tariq", 300, 1, new[] { 2582605.552998744, 2626938.8995864824, 2760472.34265179, 3129809.457215084 })]
    public void Each_Seed_Gives_The_Totals_Of_Commit_8fecd72(string hero, int seconds, int targets, double[] totals)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{hero.ToLowerInvariant()}_character.json")))!;
        var result = new MonteCarloSimulator(build, AplParser.ParseScript(DefaultActionLists.Read(hero, DefaultActionLists.DefaultName)!))
            .Run(SimTime.FromSeconds(seconds), totals.Length, targets, firstSeed: 1, degreeOfParallelism: 1, keepFightDamage: true);
        Assert.Equal(totals, result.IterationDamage);
    }

    private static MonteCarloResult Run(string apl, int iterations, int firstSeed, int seconds = 120, int targets = 3)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        // One thread: one hero fights every fight of the run.
        return new MonteCarloSimulator(build, AplParser.ParseScript(apl))
            .Run(SimTime.FromSeconds(seconds), iterations, targets, firstSeed, degreeOfParallelism: 1, keepFightDamage: true);
    }
}
