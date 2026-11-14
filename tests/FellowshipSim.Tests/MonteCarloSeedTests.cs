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
    /// proc-per-minute roll again once RealPpm had the game's drought factor and first-proc floor. Ardeos's again once Devouring
    /// Flame's bonus per Engulfing Flames came from the game data's damage multiplier less one. Gunde's again once Rend,
    /// Bloodbound Spirit's share, Owed in Blood, Massacre and Sundered Flesh followed the Blueprints of build 26320967.
    /// Rime's, Ardeos's, Gunde's and Tariq's again for the merge of the main branch, PR #53 and PR #61 into the functional tests
    /// branch, and its fixes (Hunter's Focus and Ruby Storm on targeted offensive abilities, Ruby Storm's health scaling, The
    /// Vehement once per cast, Starfall Volley's snapshot haste).
    /// </summary>
    [Theory]
    [InlineData("Rime", 120, 3, new[] { 2929425.0346213374, 3074465.9013403784, 2816687.8423429285, 3677946.9078206043 })]
    [InlineData("Rime", 300, 1, new[] { 7807948.16236004, 8172859.749842195, 7977446.590525352, 7887182.388843213 })]
    [InlineData("Ardeos", 120, 3, new[] { 2951443.4238222046, 3511746.304512846, 3110946.868820314, 3601806.8775200658 })]
    [InlineData("Ardeos", 300, 1, new[] { 5142273.698325525, 5072464.996164897, 5114126.681789436, 5093831.268507719 })]
    [InlineData("Gunde", 120, 3, new[] { 4551064.058743968, 4579418.726403046, 4651195.865758049, 4784575.6063602315 })]
    [InlineData("Gunde", 300, 1, new[] { 7170839.080529111, 8224316.561811523, 7672849.796340762, 9168364.207050571 })]
    [InlineData("Mara", 120, 3, new[] { 3013945.0748772146, 2649045.3937545605, 2878172.6517641111, 2862110.1611054321 })]
    [InlineData("Mara", 300, 1, new[] { 7022510.0781305619, 6709173.7301557567, 6977017.0757674472, 7662633.1300925035 })]
    [InlineData("Tariq", 120, 3, new[] { 1758424.9294189936, 2294493.2434868263, 1879263.041238824, 1998275.1455706037 })]
    [InlineData("Tariq", 300, 1, new[] { 3868035.95173216, 3690831.2724539717, 3487819.7587115434, 3334168.0372929694 })]
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
