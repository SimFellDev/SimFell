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
    /// heroes' again with their lists from FellowSimc's SimC lists. Re-pinned for the engine fixes of the functional tests branch (cooldown rates, the 31/30 and 29/30 global cooldown of Detonate,
    /// Detonate and Cinders, Event Horizon, Skylit Grace, Brave Machinations and Nature's Fury, weapon cooldowns, gems, Amethyst Splinters).
    /// </summary>
    [Theory]
    [InlineData("Rime", 120, 3, new[] { 3072594.4512320277, 2891268.419831695, 3064480.4096245281, 2959378.6551024965 })]
    [InlineData("Rime", 300, 1, new[] { 7932641.1848282842, 8212400.8833039831, 7497932.6960335923, 8972097.1966243163 })]
    [InlineData("Ardeos", 120, 3, new[] { 3178854.3626358607, 3015413.6128822458, 3185682.0778475194, 3177669.8038768875 })]
    [InlineData("Ardeos", 300, 1, new[] { 4936513.4543811502, 4833849.5613626326, 4455490.0538597442, 4831046.3185050916 })]
    [InlineData("Gunde", 120, 3, new[] { 4249690.0869354261, 4420362.5168073159, 4321510.3556000302, 4574701.5092196791 })]
    [InlineData("Gunde", 300, 1, new[] { 6861027.1777992975, 6757379.5672820788, 7042020.9264418846, 7284342.3034589784 })]
    [InlineData("Mara", 120, 3, new[] { 2310180.6123624993, 2117118.5577164143, 2362270.4404721106, 2636468.8278212529 })]
    [InlineData("Mara", 300, 1, new[] { 4896400.2174366862, 5123015.2697632769, 5710790.6310998807, 5730826.6926729493 })]
    [InlineData("Tariq", 120, 3, new[] { 2052950.4947410417, 1734670.2676573573, 1910001.0717739458, 1597080.5579660365 })]
    [InlineData("Tariq", 300, 1, new[] { 3535217.5392981456, 3230726.0350537631, 3256625.6694165505, 3547927.9140148927 })]
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
