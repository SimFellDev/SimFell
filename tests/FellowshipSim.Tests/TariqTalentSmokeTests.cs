using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Tariq talent and legendary, checking it is imported and its effect shows up: its
/// aura is gained (on Tariq or an enemy he hits) or its damage is dealt. The character is the one from the
/// fXyzwGcQkTrLD34R fight 4 replay. The talent sims have Executioner's Grin: a sim's enemies never fall to 30% health, and
/// Culling Strike needs it otherwise.
/// </summary>
public class TariqTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions+=/thunder_call
        actions+=/raging_tempest
        actions+=/focused_wrath
        actions+=/leap_smash
        actions+=/culling_strike
        actions+=/chain_lightning
        actions+=/hammer_storm,if=active_enemies>=3
        actions+=/skull_crusher
        actions+=/heavy_strike
        actions+=/face_breaker
        actions+=/wild_swing
        """;

    /// <summary>The talents of the fight 4 Tariq, less the row 5 and row 6 picks the tests swap in.</summary>
    private static readonly int[] BaseTalents =
    [
        HighRoad.TalentId, SquareHammer.TalentId, Sledgehammer.TalentId, MouthForWar.TalentId, Bloodline.TalentId,
        RideTheLightning.TalentId, FarBeyondDriven.TalentId,
    ];

    [Theory]
    [InlineData(LeftHandPath.TalentId, "damage:Face Breaker")]
    [InlineData(RideTheLightning.TalentId, "aura:Ride the Lightning")]
    [InlineData(SquareHammer.TalentId, "aura:Square Hammer: Active")]
    [InlineData(BloodAndThunder.TalentId, "damage:Chain Lightning")]
    [InlineData(TheMotherload.TalentId, "damage:Chain Lightning")]
    [InlineData(MouthForWar.TalentId, "aura:Focused Wrath")]
    [InlineData(Thunderstruck.TalentId, "damage:Chain Lightning")]
    [InlineData(Pneuma.TalentId, "damage:Culling Strike")]
    [InlineData(Sledgehammer.TalentId, "damage:Sledgehammer", 2)]
    [InlineData(FarBeyondDriven.TalentId, "aura:Far Beyond Driven")]
    [InlineData(KillEmAll.TalentId, "aura:Kill Em All")]
    [InlineData(AceOfSpades.TalentId, "damage:Chain Lightning")]
    [InlineData(Schism.TalentId, "aura:Schism - Hammer Storm")]
    [InlineData(ThemBones.TalentId, "damage:Skull Crusher")]
    [InlineData(CrackTheSky.TalentId, "damage:Thunder Call: Skull Crusher")]
    [InlineData(KillingInTheName.TalentId, "damage:Culling Strike")]
    public void Talent_Is_Modeled_And_Takes_Effect(int talentId, string evidence, int minTargets = 1)
    {
        foreach (int targets in new[] { 1, 8 }.Where(t => t >= minTargets))
        {
            var (result, hero, gained) = Run([.. BaseTalents, talentId], targets, legendaryItemId: ExecutionersGrin.WrapsItemId);
            Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
            Assert.Contains(hero.Talents, t => t.Id == talentId);
            Assert.True(result.MeanDps > 0);
            Assert.True(Seen(result, gained, evidence, out string seen), $"Talent {talentId}: no {evidence} on {targets} target(s)");
            output.WriteLine($"Talent {talentId} on {targets} target(s): {result.MeanDps:N0} DPS, {evidence} -> {seen}");
        }
    }

    [Theory]
    [InlineData(SlayersMosh.HelmItemId, "debuff:Slayer's Mosh")]
    [InlineData(ExecutionersGrin.WrapsItemId, "aura:Executioner's Grin")]
    [InlineData(ThunderingVortex.HarnessItemId, "aura:Thundering Vortex")]
    public void Legendary_Is_Modeled_And_Takes_Effect(int itemId, string evidence)
    {
        var (result, hero, gained) = Run(BaseTalents, 3, legendaryItemId: itemId);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Legendary"));
        Assert.Single(hero.Legendaries);
        Assert.True(Seen(result, gained, evidence, out string seen), $"Legendary item {itemId}: no {evidence}");
        output.WriteLine($"Legendary item {itemId}: {result.MeanDps:N0} DPS, {evidence} -> {seen}");
    }

    [Fact]
    public void Every_Talent_In_Tariqs_Tree_Is_Modeled()
    {
        Assert.Equal(18, Tariq.TalentTree.Length);
        foreach (var slot in Tariq.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Tariq.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
    }

    private static bool Seen(MonteCarloResult result, Dictionary<string, int> gained, string evidence, out string seen)
    {
        string[] parts = evidence.Split(':', 2);
        if (parts[0] is "aura" or "debuff")
        {
            string key = parts[0] == "debuff" ? evidence : parts[1];
            seen = $"{gained.GetValueOrDefault(key)} gains";
            return gained.GetValueOrDefault(key) > 0;
        }
        seen = $"{result.AbilityDpsBreakdown.GetValueOrDefault(parts[1]):N0} DPS";
        return result.AbilityDpsBreakdown.GetValueOrDefault(parts[1]) > 0;
    }

    private static (MonteCarloResult Result, Hero Hero, Dictionary<string, int> Gained) Run(IEnumerable<int> talentIds, int targets,
        int iterations = 10, int? legendaryItemId = null)
    {
        // The Tariq from fXyzwGcQkTrLD34R fight 4 (Stormwatch), as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tariq_character.json")))!;
        build.Talents = talentIds.ToList();
        build.LegendaryItemIds = legendaryItemId is { } id ? [id] : [];
        var hero = HeroImporter.Import(build);
        var gained = new Dictionary<string, int>();
        hero.Auras.OnGained += (aura, _, _) => gained[aura.Def.Name] = gained.GetValueOrDefault(aura.Def.Name) + 1;
        // An enemy's debuffs count as "debuff:<name>" for each hit that lands on it while they hold.
        hero.OnDamageDealt += (in hit) =>
        {
            if (hit.Target == null) return;
            foreach (var aura in hit.Target.Auras.Active(hit.Timestamp)) gained[$"debuff:{aura.Def.Name}"] = gained.GetValueOrDefault($"debuff:{aura.Def.Name}") + 1;
        };
        // One thread: the simulator builds one hero, the one above.
        var result = new MonteCarloSimulator(() => hero, AplParser.ParseScript(Apl))
            .Run(SimTime.FromSeconds(300), iterations, targets, degreeOfParallelism: 1);
        return (result, hero, gained);
    }
}
