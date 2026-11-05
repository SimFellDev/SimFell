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
    /// Totals of fights 1-4 of each hero's default action list, captured again when sims began rolling crits and damage
    /// spread (before, each hit dealt its average and counted as a crit only above a 50% chance); Rime's, Gunde's, Mara's
    /// and Tariq's again once dungeon_event.fight.remains and target.time_to_die read the fight's end. Tariq's again once
    /// Schism had bad luck protection, and again once Bloodline was unhasted, Ride the Lightning snapshotted and Kill Em All
    /// left Heavy Strike out. Rime's, Ardeos's, Mara's and Tariq's again for the engine fixes of the functional tests branch
    /// (cooldown rates, Detonate's global cooldown, Event Horizon, Skylit Grace, Brave Machinations, weapon cooldowns, gems).
    /// Tariq's again for his new default list.
    /// </summary>
    [Theory]
    [InlineData("Rime", 120, 3, new[] { 3103846.9857844426, 3146941.0664216764, 2661851.3885713113, 2817501.47229399 })]
    [InlineData("Rime", 300, 1, new[] { 8470077.969138976, 7314085.758635247, 6457222.233761247, 8102670.212286511 })]
    [InlineData("Ardeos", 120, 3, new[] { 2928926.308143849, 3153655.5730937966, 3210365.792271153, 3186044.199992814 })]
    [InlineData("Ardeos", 300, 1, new[] { 4880401.459850469, 4756376.690589438, 4909095.667185546, 4970616.393990371 })]
    [InlineData("Gunde", 120, 3, new[] { 5234084.4458837481, 4755985.9798798524, 4219977.3005579598, 4397998.891160924 })]
    [InlineData("Gunde", 300, 1, new[] { 7589315.0879662065, 7860490.4554016367, 7145894.9954196177, 7482946.3632446928 })]
    [InlineData("Mara", 120, 3, new[] { 2440956.3761566584, 2523958.1828004178, 2016531.8647401505, 2580733.011348616 })]
    [InlineData("Mara", 300, 1, new[] { 4698165.2098863395, 6431641.410211125, 5304913.496761604, 5296672.2168469215 })]
    [InlineData("Tariq", 120, 3, new[] { 1693530.682890242, 2424288.0936405938, 2202817.0745414351, 1898456.3845911967 })]
    [InlineData("Tariq", 300, 1, new[] { 3583787.245763781, 3830425.3795905053, 3372158.792518341, 3295629.6778827147 })]
    public void Each_Seed_Gives_Its_Pinned_Totals(string hero, int seconds, int targets, double[] totals)
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
