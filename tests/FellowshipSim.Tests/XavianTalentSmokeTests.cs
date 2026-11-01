using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Xavian;
using FellowshipSim.Core.Heroes.Xavian.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Xavian talent and legendary, checking it is imported and its effect shows up: its
/// aura is gained (on Xavian or an enemy he hits), its damage is dealt, or the damage it raises grows. The character is
/// the one from the XyGpcHjNaAzgk7mW fight 20 replay.
/// </summary>
public class XavianTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions=attack
        actions+=/aura_of_solace,if=!buff.aura_of_solace.up
        actions+=/decree_of_the_sun
        actions+=/omega_reprieval
        actions+=/solar_shield
        actions+=/shining_halo
        actions+=/solar_blades
        actions+=/omnistrike
        actions+=/sky_crash
        actions+=/ruptured_dawn
        actions+=/blinding_slash
        actions+=/brilliant_flash
        actions+=/sun_strike
        actions+=/brilliant_flare
        """;

    /// <summary>The talents of the fight 20 Xavian, less the picks the tests swap in.</summary>
    private static readonly int[] BaseTalents = [MagicWard.TalentId, TemperedDescent.TalentId];

    [Theory]
    [InlineData(SunsTouch.TalentId, "damage:Sun's Touch")]
    [InlineData(GrandHalo.TalentId, "more:damage:Shining Halo")]
    [InlineData(SolarBurn.TalentId, "damage:Sunburn")]
    [InlineData(TemperedDescent.TalentId, "aura:Tempered Descent")]
    [InlineData(LightsOath.TalentId, "aura:Light's Oath")]
    [InlineData(RisingSun.TalentId, "more:damage:Solar Blades")]
    [InlineData(ImposingPresence.TalentId, "dps")]
    [InlineData(CelestialFavor.TalentId, "more:damage:Brilliant Flash")]
    [InlineData(GleamingStrikes.TalentId, "more:damage:Omnistrike")]
    [InlineData(SunsVerdict.TalentId, "damage:Sun Beams")]
    [InlineData(MagicWard.TalentId, "aura:Magic Ward")]
    [InlineData(HallowedShield.TalentId, "aura:Hallowed Shield")]
    [InlineData(VanguardOfVengeance.TalentId, "aura:Vanguard of Vengeance")]
    [InlineData(Invictus.TalentId, "damage:Invictus")]
    [InlineData(GoldenHour.TalentId, "aura:Golden Hour")]
    [InlineData(ZenithTechnique.TalentId, "more:damage:Shining Halo")]
    [InlineData(HealingHalo.TalentId, "dps")]
    [InlineData(ShiftingSpectrum.TalentId, "dps")]
    public void Talent_Is_Modeled_And_Takes_Effect(int talentId, string evidence)
    {
        foreach (int targets in new[] { 1, 8 })
        {
            // The base leaves the talent out, so only the talent can show the evidence.
            int[] without = BaseTalents.Where(t => t != talentId).ToArray();
            var (result, hero, gained) = Run([.. without, talentId], targets);
            Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
            Assert.Contains(hero.Talents, t => t.Id == talentId);
            Assert.True(result.MeanDps > 0);
            if (evidence == "dps") continue;
            // "more:" evidence is there without the talent too: the talent must add to it.
            bool more = evidence.StartsWith("more:");
            string shown = more ? evidence["more:".Length..] : evidence;
            double amount = Amount(result, gained, shown);
            double baseline = 0.0;
            if (more)
            {
                var (baseResult, _, baseGained) = Run(without, targets);
                baseline = Amount(baseResult, baseGained, shown);
            }
            output.WriteLine($"Talent {talentId} on {targets} target(s): {result.MeanDps:N0} DPS, {shown} -> {amount:N0} (without: {baseline:N0})");
            Assert.True(amount > baseline, $"Talent {talentId}: no {evidence} on {targets} target(s)");
        }
    }

    [Theory]
    [InlineData(SolarGlare.BreastplateItemId, "debuff:Blinding Horizon")]
    [InlineData(GrosslyIncandescent.HelmItemId, "aura:Grossly Incandescent")]
    [InlineData(FortressInTheSands.GreavesItemId, "dps")]
    public void Legendary_Is_Modeled_And_Takes_Effect(int itemId, string evidence)
    {
        var (result, hero, gained) = Run(BaseTalents, 3, legendaryItemId: itemId);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Legendary"));
        Assert.Single(hero.Legendaries);
        Assert.True(result.MeanDps > 0);
        if (evidence == "dps") return;
        double amount = Amount(result, gained, evidence);
        output.WriteLine($"Legendary item {itemId}: {result.MeanDps:N0} DPS, {evidence} -> {amount:N0}");
        Assert.True(amount > 0, $"Legendary item {itemId}: no {evidence}");
    }

    [Fact]
    public void Every_Talent_In_Xavians_Tree_Is_Modeled()
    {
        Assert.Equal(18, Xavian.TalentTree.Length);
        foreach (var slot in Xavian.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Xavian.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
    }

    /// <summary>Gains of an "aura:" or "debuff:" evidence, or the DPS of a "damage:" one.</summary>
    private static double Amount(MonteCarloResult result, Dictionary<string, int> gained, string evidence)
    {
        string[] parts = evidence.Split(':', 2);
        return parts[0] switch
        {
            "aura" => gained.GetValueOrDefault(parts[1]),
            "debuff" => gained.GetValueOrDefault(evidence),
            _ => result.AbilityDpsBreakdown.GetValueOrDefault(parts[1]),
        };
    }

    private static (MonteCarloResult Result, Hero Hero, Dictionary<string, int> Gained) Run(IEnumerable<int> talentIds, int targets,
        int iterations = 10, int? legendaryItemId = null)
    {
        // The Xavian from XyGpcHjNaAzgk7mW fight 20 (Urrak Markets), as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "xavian_character.json")))!;
        build.Talents = talentIds.ToList();
        build.LegendaryItemIds = legendaryItemId is { } id ? [id] : [];
        var hero = HeroImporter.Import(build);
        var gained = new Dictionary<string, int>();
        hero.Auras.OnGained += (aura, _, _) => gained[aura.Def.Name] = gained.GetValueOrDefault(aura.Def.Name) + 1;
        // An enemy's debuffs count as "debuff:<name>" for each hit on it while they hold.
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
