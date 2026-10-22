using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Rime talent that no recorded combat log uses, checking the talent is imported and
/// its effect shows up: its aura is gained or its damage is dealt. The character is the one from the fight 7 replay.
/// </summary>
public class RimeTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions+=/flight_of_the_navir
        actions+=/ice_blitz
        actions+=/winters_blessing
        actions+=/talon_strike
        actions+=/rising_talons
        actions+=/ice_comet
        actions+=/glacial_blast
        actions+=/freezing_torrent
        actions+=/bursting_ice
        actions+=/cold_snap
        actions+=/frost_bolt
        """;

    private static readonly int[] BaseTalents =
    [
        GlacialAssault.TalentId, Burstbolter.TalentId, CascadingBlitz.TalentId, BurstingSwallows.TalentId,
        NavirsKeeper.TalentId, WisdomOfTheNorth.TalentId,
    ];

    [Theory]
    [InlineData(HarrowingIce.TalentId, "aura:Harrowing Ice")]
    [InlineData(SupremeTorrent.TalentId, "damage:Freezing Torrent")]
    [InlineData(CoalescingFrost.TalentId, "damage:Coalescing Frost")]
    [InlineData(ColdShower.TalentId, "damage:Ice Comet")]
    [InlineData(Avalanche.TalentId, "damage:Ice Comet")]
    [InlineData(IcyFlow.TalentId, "aura:Icy Flow")]
    [InlineData(FrostweaversWrath.TalentId, "aura:Frostweaver's Wrath")]
    [InlineData(SoulfrostTorrent.TalentId, "aura:Soulfrost Torrent")]
    public void Talent_Is_Modeled_And_Takes_Effect(int talentId, string evidence)
    {
        foreach (int targets in new[] { 1, 8 })
        {
            var (result, hero, gained) = Run([.. BaseTalents, talentId], targets);
            Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
            Assert.True(result.MeanDps > 0);
            string[] parts = evidence.Split(':', 2);
            bool seen = parts[0] == "aura"
                ? gained.GetValueOrDefault(parts[1]) > 0
                : result.AbilityDpsBreakdown.GetValueOrDefault(parts[1]) > 0;
            output.WriteLine($"Talent {talentId} on {targets} target(s): {result.MeanDps:N0} DPS, {evidence} -> " +
                             (parts[0] == "aura" ? $"{gained.GetValueOrDefault(parts[1])} gains" : $"{result.AbilityDpsBreakdown.GetValueOrDefault(parts[1]):N0} DPS"));
            Assert.True(seen, $"Talent {talentId}: no {evidence} on {targets} target(s)");
        }
    }

    [Fact]
    public void Every_Talent_In_Rimes_Tree_Is_Modeled()
    {
        Assert.Equal(18, Rime.TalentTree.Length);
        foreach (var slot in Rime.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Rime.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
    }

    private static (MonteCarloResult Result, Hero Hero, Dictionary<string, int> Gained) Run(IEnumerable<int> talentIds, int targets, int iterations = 10)
    {
        // The Rime from yMAZVJL9hTpFjqbC fight 7 (sww), as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        build.Talents = talentIds.ToList();
        var hero = HeroImporter.Import(build);
        var gained = new Dictionary<string, int>();
        hero.Auras.OnGained += (aura, _, _) => gained[aura.Def.Name] = gained.GetValueOrDefault(aura.Def.Name) + 1;
        // One thread: the simulator builds one hero, the one above.
        var result = new MonteCarloSimulator(() => hero, AplParser.ParseScript(Apl))
            .Run(SimTime.FromSeconds(300), iterations, targets, degreeOfParallelism: 1);
        return (result, hero, gained);
    }
}
