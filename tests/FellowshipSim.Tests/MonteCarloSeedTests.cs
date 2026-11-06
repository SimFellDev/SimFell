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
    [InlineData("Rime", 120, 3, new[] { 2907912.8285365724, 3077995.7808620073, 2639474.0534616355, 3692629.478085001 })]
    [InlineData("Rime", 300, 1, new[] { 7808773.518253535, 8580918.500406025, 6484534.03766708, 7908492.7942486135 })]
    [InlineData("Ardeos", 120, 3, new[] { 3345832.948582943, 3320652.327085807, 3045402.7701891046, 3364899.0574173066 })]
    [InlineData("Ardeos", 300, 1, new[] { 4861741.223070237, 4963248.446390114, 5005333.543539385, 5275683.404074567 })]
    [InlineData("Gunde", 120, 3, new[] { 4750556.164876443, 4292964.45035569, 4326519.37782646, 4517457.10624198 })]
    [InlineData("Gunde", 300, 1, new[] { 7830907.803116159, 8218577.12439234, 7712523.117932505, 7129098.934661397 })]
    [InlineData("Mara", 120, 3, new[] { 2330058.4626759314, 2417646.543699906, 2084912.5279980167, 2737120.527902944 })]
    [InlineData("Mara", 300, 1, new[] { 5714356.674606941, 6466807.200661406, 5173169.937469561, 5200053.606742187 })]
    [InlineData("Tariq", 120, 3, new[] { 1683642.4543730475, 1894807.7444384687, 2138104.880529615, 2150306.6377791297 })]
    [InlineData("Tariq", 300, 1, new[] { 3430804.3659526613, 3393330.185518483, 3561628.9533571065, 4194916.886603879 })]
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
