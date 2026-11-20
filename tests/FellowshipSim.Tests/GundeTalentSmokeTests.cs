using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Gunde talent that no recorded combat log uses, checking the talent is imported and
/// its effect shows up: its aura is gained or its damage is dealt. The character is the one from the Y4wpb2f3gc8LqJDX
/// fight 25 replay.
/// </summary>
public class GundeTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions+=/attack
        actions+=/bloodbound_spirit
        actions+=/fated_strike
        actions+=/reign_in_blood
        actions+=/rupture
        actions+=/slaughter
        actions+=/owed_in_blood
        actions+=/grim_carve
        actions+=/heart_splitter
        actions+=/blood_arc
        actions+=/reavers_edge
        actions+=/double_strike
        """;

    private static readonly int[] BaseTalents =
    [
        FrenziedReign.TalentId, DeepRend.TalentId, Oathshatter.TalentId, Carnage.TalentId, SuperiorSerration.TalentId,
        Bloodbath.TalentId, HarvestersToll.TalentId, CrimsonStrikes.TalentId, DarkeningHearts.TalentId,
    ];

    [Theory]
    [InlineData(AncestralInstinct.TalentId, "aura:Ancestral Instinct")]
    [InlineData(MurderOfCrows.TalentId, "aura:Murder of Crows")]
    [InlineData(RavensPrecision.TalentId, "damage:Raven's Precision")]
    [InlineData(Bloodcraze.TalentId, "damage:Bloodcraze")]
    [InlineData(Massacre.TalentId, "aura:Massacre")]
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
    public void Every_Talent_In_Gundes_Tree_Is_Modeled()
    {
        Assert.Equal(18, Gunde.TalentTree.Length);
        foreach (var slot in Gunde.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Gunde.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
    }

    /// <summary>
    /// Carrion Onslaught (Northlander's Wraps): Owed in Blood's casts grant it and Blood Arc deals more than with Bloody
    /// Mess (Northlander's Helm), which leaves Blood Arc alone; both bring the same legendary stats.
    /// </summary>
    [Fact]
    public void Carrion_Onslaught_Raises_Blood_Arc_Damage()
    {
        var (without, _, _) = Run(BaseTalents, 1, legendaryItemIds: [BloodyMess.HeadItemId]);
        var (with, hero, gained) = Run(BaseTalents, 1, legendaryItemIds: [CarrionOnslaught.WrapsItemId]);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Legendary"));
        double before = without.AbilityDpsBreakdown.GetValueOrDefault("Blood Arc");
        double after = with.AbilityDpsBreakdown.GetValueOrDefault("Blood Arc");
        output.WriteLine($"Carrion Onslaught: {gained.GetValueOrDefault(CarrionOnslaught.EffectName)} gains, Blood Arc {before:N0} -> {after:N0} DPS, total {without.MeanDps:N0} -> {with.MeanDps:N0}");
        Assert.True(gained.GetValueOrDefault(CarrionOnslaught.EffectName) > 0);
        Assert.True(after > before, $"Blood Arc {before:N0} -> {after:N0} DPS");
    }

    private static Hero Import(IEnumerable<int> talentIds, int[]? legendaryItemIds = null)
    {
        // The Gunde from Y4wpb2f3gc8LqJDX fight 25 (Culex), as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gunde_character.json")))!;
        build.Talents = talentIds.ToList();
        // The fixture's legendary unless others are given.
        if (legendaryItemIds != null) build.LegendaryItemIds = [.. legendaryItemIds];
        return HeroImporter.Import(build);
    }

    private static (MonteCarloResult Result, Hero Hero, Dictionary<string, int> Gained) Run(IEnumerable<int> talentIds, int targets, int iterations = 10,
        int[]? legendaryItemIds = null)
    {
        var hero = Import(talentIds, legendaryItemIds);
        var gained = new Dictionary<string, int>();
        hero.Auras.OnGained += (aura, _, _) => gained[aura.Def.Name] = gained.GetValueOrDefault(aura.Def.Name) + 1;
        // One thread: the simulator builds one hero, the one above.
        var result = new MonteCarloSimulator(() => hero, AplParser.ParseScript(Apl))
            .Run(SimTime.FromSeconds(300), iterations, targets, degreeOfParallelism: 1);
        return (result, hero, gained);
    }
}
