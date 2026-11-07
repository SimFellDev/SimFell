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
    /// spread (before, each hit dealt its average and counted as a crit only above a chance threshold); Rime's, Gunde's, Mara's
    /// and Tariq's again once dungeon_event.fight.remains and target.time_to_die read the fight's end. Tariq's again once
    /// Schism had bad luck protection, and again once Bloodline was unhasted, Ride the Lightning snapshotted and Kill Em All
    /// left Heavy Strike out. Rime's, Ardeos's, Mara's and Tariq's again for the engine fixes of the functional tests branch
    /// (cooldown rates, Detonate's global cooldown, Event Horizon, Skylit Grace, Brave Machinations, weapon cooldowns, gems).
    /// Tariq's again for his new default list. Mara's again once an Arachnid Assault clone kept the cast's numbers. The rows
    /// with a Spirit Refund past the duration of Rising Spirit again once that duration had a cap. Mara's 3-target row again once
    /// Hemorrhaging Strike had no cooldown. Mara's again once her default list read Deadly Scheme: Active. Every row with a
    /// proc-per-minute roll again once RealPpm had the game's drought factor and first-proc floor. Ardeos's again once Searing
    /// Blaze kept part of its time left on a recast, Slow Burn gave its full half second and a cap, Intensifying Inferno
    /// counted each effect and Devouring Flame multiplied per Engulfing Flames. Tariq's again once health came from BaseHealth, Stamina and the max health auras (Ruby Storm).
    /// </summary>
    [Theory]
    [InlineData("Rime", 120, 3, new[] { 2929444.08752369, 3077995.7808620073, 2829134.0571837318, 3705848.5280579049 })]
    [InlineData("Rime", 300, 1, new[] { 7807802.8056133138, 8531419.3838742785, 8084639.1539794672, 7908492.7942486135 })]
    [InlineData("Ardeos", 120, 3, new[] { 3416176.3452053526, 3513888.2134329369, 3159070.0166639746, 3238265.2998513975 })]
    [InlineData("Ardeos", 300, 1, new[] { 4969978.3232260561, 4912988.5024501747, 4687323.675213783, 4858429.3177604256 })]
    [InlineData("Gunde", 120, 3, new[] { 4748197.3158851741, 4295319.6169056566, 4262391.2107238909, 4470118.3590960605 })]
    [InlineData("Gunde", 300, 1, new[] { 7249581.1534622693, 7814233.1795717087, 7659215.0033817766, 7371523.7896765638 })]
    [InlineData("Mara", 120, 3, new[] { 2560953.2346197013, 2353016.7117702793, 2897776.7017053822, 2866602.873777526 })]
    [InlineData("Mara", 300, 1, new[] { 7895879.749682934, 6001428.5453476505, 7463940.1662859339, 7588414.1623460148 })]
    [InlineData("Tariq", 120, 3, new[] { 1975056.465988361, 1770988.7557543414, 2311390.7997524641, 2173045.6661721431 })]
    [InlineData("Tariq", 300, 1, new[] { 3437695.7313061529, 3708298.4520335575, 3263678.2554253116, 3721164.205084838 })]
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
