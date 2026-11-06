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
    /// Tariq's again for his new default list. Mara's again once an Arachnid Assault clone kept the cast's numbers. The rows
    /// with a Spirit Refund past 20s of Rising Spirit again once its duration stopped at 20s. Mara's 3-target row again once
    /// Hemorrhaging Strike had no cooldown. Mara's again once her default list read Deadly Scheme: Active. Every row with a
    /// proc-per-minute roll again once RealPpm had the game's drought factor and first-proc floor.
    /// </summary>
    [Theory]
    [InlineData("Rime", 120, 3, new[] { 2929444.08752369, 3077995.7808620073, 2829134.0571837318, 3705848.5280579049 })]
    [InlineData("Rime", 300, 1, new[] { 7807802.8056133138, 8531419.3838742785, 8084639.1539794672, 7908492.7942486135 })]
    [InlineData("Ardeos", 120, 3, new[] { 3345832.9485829431, 3320652.327085807, 2970622.7621510844, 3343792.2069075285 })]
    [InlineData("Ardeos", 300, 1, new[] { 4861741.2230702369, 4925568.8819087893, 4902383.9729880206, 5274164.3018721882 })]
    [InlineData("Gunde", 120, 3, new[] { 4748197.3158851741, 4295319.6169056566, 4262391.2107238909, 4470118.3590960605 })]
    [InlineData("Gunde", 300, 1, new[] { 7249581.1534622693, 7814233.1795717087, 7659215.0033817766, 7371523.7896765638 })]
    [InlineData("Mara", 120, 3, new[] { 2560953.2346197013, 2353016.7117702793, 2897776.7017053822, 2866602.873777526 })]
    [InlineData("Mara", 300, 1, new[] { 7895879.749682934, 6001428.5453476505, 7463940.1662859339, 7588414.1623460148 })]
    [InlineData("Tariq", 120, 3, new[] { 1975057.5546042551, 1770988.9151165667, 2311391.1960613201, 2173046.1471902938 })]
    [InlineData("Tariq", 300, 1, new[] { 3437696.0836679186, 3708298.5408707494, 3263678.3343260279, 3737354.5175946755 })]
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
