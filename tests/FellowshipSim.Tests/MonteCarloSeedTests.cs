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
    public void A_Run_Gives_The_Same_Totals_On_Any_Number_Of_Threads()
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        MonteCarloResult RunOn(int threads) => new MonteCarloSimulator(build, AplParser.ParseScript(Apl))
            .Run(SimTime.FromSeconds(60), iterations: 250, targetCount: 2, firstSeed: 1, degreeOfParallelism: threads);

        var one = RunOn(1);
        var four = RunOn(4);
        Assert.Equal(one.MeanDps, four.MeanDps);
        Assert.Equal(one.StandardDeviation, four.StandardDeviation);
        Assert.Equal(one.TotalMeanDamage, four.TotalMeanDamage);
        Assert.Equal(one.AbilityDpsBreakdown, four.AbilityDpsBreakdown);
        Assert.Equal(one.PrimaryTargetDps, four.PrimaryTargetDps);
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

    /// <summary>
    /// Totals of fights 1-4 of each hero's default action list, captured after Rime replay accuracy v1 (#8); the other
    /// heroes' again with their lists from FellowSimc's SimC lists. Re-pinned for the engine fixes of the functional tests branch (cooldown rates,
    /// Detonate and Cinders, Event Horizon, Skylit Grace, Brave Machinations and Nature's Fury, weapon cooldowns, gems, Amethyst Splinters).
    /// </summary>
    [Theory]
    [InlineData("Rime", 120, 3, new[] { 2870678.244003684, 2843688.093874783, 2866593.293901274, 2897177.65225082 })]
    [InlineData("Rime", 300, 1, new[] { 7233920.966227477, 7623903.101970881, 6747224.146484256, 7815783.491097497 })]
    [InlineData("Ardeos", 120, 3, new[] { 2532939.3624233976, 2717047.786400743, 2552893.591250891, 2798633.4128232026 })]
    [InlineData("Ardeos", 300, 1, new[] { 4128606.6696789484, 3854122.2776898607, 4160908.463497333, 4075445.228085431 })]
    [InlineData("Gunde", 120, 3, new[] { 3663885.3829768337, 3631628.931993659, 3717554.9174220143, 3582247.9549547625 })]
    [InlineData("Gunde", 300, 1, new[] { 6479658.102081677, 6331566.565216315, 6551150.214686898, 6376591.190708393 })]
    [InlineData("Mara", 120, 3, new[] { 1885379.5598271624, 1832325.8112658088, 1819565.372964225, 1850822.1432009935 })]
    [InlineData("Mara", 300, 1, new[] { 3773708.2623223015, 3731841.6368531897, 4002344.7956146672, 4918318.978244564 })]
    [InlineData("Tariq", 120, 3, new[] { 1594076.1782188087, 1487084.1576567756, 1617138.8601178003, 1353107.3927278493 })]
    [InlineData("Tariq", 300, 1, new[] { 2907712.6095890063, 2723692.8120396896, 2658707.9397020717, 3238910.6953767887 })]
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
