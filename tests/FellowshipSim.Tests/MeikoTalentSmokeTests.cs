using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Meiko;
using FellowshipSim.Core.Heroes.Meiko.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Meiko talent and legendary, checking it is imported and its effect shows up: its aura
/// is gained (on Meiko or an enemy she hits) or its damage is dealt. The character is the one from the 7Zmfx9gq6HjvThzc
/// fight 2 replay. The tree test covers Resonance of Earth, Stone Guard, Debilitating Vortex, Slipstream, Earthbourne,
/// Harmonic Residual, Shattering Obelisk and Peacefield. The list builds Earthfall and Stormfall in turn (Rising Earth
/// while Shatter Earth's cooldown has more than 22 seconds left), so both paths run on any number of enemies.
/// </summary>
public class MeikoTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions=attack
        actions+=/twin_souls_army_of_one
        actions+=/stone_shield
        actions+=/shatter_earth
        actions+=/serenity,if=!buff.everlasting_spirit.up
        actions+=/double_palm_strike
        actions+=/spirited_vortex
        actions+=/earthfist_barrage
        actions+=/lashing_stormkick
        actions+=/rising_storm
        actions+=/rising_earth
        actions+=/earth_fist,if=buff.kick_technique.up&buff.stormfall.up
        actions+=/spirit_palm,if=buff.kick_technique.up
        actions+=/wind_kick,if=buff.fist_technique.up&buff.earthfall.up
        actions+=/spirit_palm,if=buff.fist_technique.up
        actions+=/earth_fist,if=buff.palm_technique.up&buff.spirited_vortex.remains<4
        actions+=/wind_kick,if=buff.palm_technique.up
        actions+=/spirit_palm,if=buff.spirited_vortex.remains<4|buff.warden_of_the_temple.stack=3|talent.forbidden_technique&debuff.forbidden_technique.remains<6
        actions+=/earth_fist,if=buff.earthfall.up
        actions+=/wind_kick,if=buff.stormfall.up
        actions+=/earth_fist,if=cooldown.shatter_earth.remains>22
        actions+=/wind_kick
        """;

    /// <summary>A talent with no part in the other talents' evidence; its own test leaves it out.</summary>
    private static readonly int[] BaseTalents = [PerfectStorm.TalentId];

    [Theory]
    [InlineData(Earthwell.TalentId, "more:aura:Serenity Stacks")]
    [InlineData(PerfectStorm.TalentId, "damage:Perfect Storm")]
    [InlineData(WillOfStone.TalentId, "more:damage:Spirited Vortex")]
    [InlineData(WhippingApex.TalentId, "more:aura:Serenity Stacks")]
    [InlineData(GuardiansFluidity.TalentId, "more:damage:Stone Shield")]
    [InlineData(HarshWinds.TalentId, "more:damage:Spirited Vortex")]
    [InlineData(WardenOfTheTemple.TalentId, "aura:Warden of the Temple")]
    [InlineData(ForbiddenTechnique.TalentId, "damage:Forbidden Technique")]
    [InlineData(RumblingStone.TalentId, "more:damage:Earth Fist")]
    [InlineData(ConclusiveStrikes.TalentId, "more:damage:Lashing Stormkick")]
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
    [InlineData(TectonicImpact.TunicItemId, "more:damage:Earthfist Barrage")]
    [InlineData(EarthenSkin.HelmItemId, "more:damage:Wind Kick")]
    [InlineData(EverlastingSpirit.LeggingsItemId, "damage:Everlasting Spirit")]
    public void Legendary_Is_Modeled_And_Takes_Effect(int itemId, string evidence)
    {
        var (result, hero, gained) = Run(BaseTalents, 3, legendaryItemId: itemId);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Legendary"));
        Assert.Single(hero.Legendaries);
        bool more = evidence.StartsWith("more:");
        string shown = more ? evidence["more:".Length..] : evidence;
        double amount = Amount(result, gained, shown);
        double baseline = more ? Amount(Run(BaseTalents, 3).Result, [], shown) : 0.0;
        output.WriteLine($"Legendary item {itemId}: {result.MeanDps:N0} DPS, {shown} -> {amount:N0} (without: {baseline:N0})");
        Assert.True(amount > baseline, $"Legendary item {itemId}: no {evidence}");
    }

    [Fact]
    public void Every_Talent_In_Meikos_Tree_Is_Modeled()
    {
        Assert.Equal(18, Meiko.TalentTree.Length);
        foreach (var slot in Meiko.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Meiko.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
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
        // The Meiko from 7Zmfx9gq6HjvThzc fight 2 (Empyrean Sands), as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "meiko_character.json")))!;
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
