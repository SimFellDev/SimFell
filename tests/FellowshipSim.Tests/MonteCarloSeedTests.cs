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

    /// <summary>Totals of fights 1-4 of each hero's default action list, captured after Rime replay accuracy v1 (#8).</summary>
    [Theory]
    [InlineData("Rime", 120, 3, new[] { 2870678.2440036838, 2843688.0938747828, 2929791.065455927, 2897177.6522508198 })]
    [InlineData("Rime", 300, 1, new[] { 7233920.9662274774, 7623903.1019708812, 7579174.9157335293, 7815783.4910974968 })]
    [InlineData("Ardeos", 120, 3, new[] { 1845987.3390204795, 2042634.8375148706, 1929449.9252678133, 1949523.0494714687 })]
    [InlineData("Ardeos", 300, 1, new[] { 3608322.4011011017, 3532991.629864017, 3372426.210184026, 3220176.1674971473 })]
    [InlineData("Gunde", 120, 3, new[] { 3811424.4034362095, 3560833.2644922696, 3234331.2926150644, 3706322.8886219356 })]
    [InlineData("Gunde", 300, 1, new[] { 5973349.8170667086, 5268126.14582658, 5361702.123513517, 6138935.9951158399 })]
    [InlineData("Mara", 120, 3, new[] { 1516517.1318114088, 1069946.8106850635, 1299322.9615743649, 1367035.5049298343 })]
    [InlineData("Mara", 300, 1, new[] { 3533874.9679868752, 3179579.6722181421, 3178372.0824673641, 3172106.5293867188 })]
    [InlineData("Tariq", 120, 3, new[] { 1414937.2808608001, 1377475.57163405, 1307630.9678825177, 1512618.7135326373 })]
    [InlineData("Tariq", 300, 1, new[] { 2669110.3478315724, 2636357.4835497807, 3006694.6476314552, 2885783.5698467265 })]
    public void Each_Seed_Gives_The_Totals_Of_Rime_Accuracy_V1(string hero, int seconds, int targets, double[] totals)
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
