using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Elarion talent and legendary, checking it is imported and its effect shows up: its
/// aura is gained (on Elarion or an enemy he hits) or its damage is dealt, or it adds to what the build without it shows
/// ("more:"). The character is the one from the pWmcV42K39GHTLQX fight 23 replay.
/// </summary>
public class ElarionTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions+=/shoot
        actions+=/event_horizon
        actions+=/skystriders_grace
        actions+=/lunarlight_mark
        actions+=/skystriders_supremacy
        actions+=/starfall_volley
        actions+=/heartseeker_barrage
        actions+=/celestial_shot,if=buff.celestial_impetus.up
        actions+=/highwind_arrow,if=buff.resurgent_winds.up
        actions+=/multishot,if=buff.skystriders_supremacy.up|buff.focused_expanse.up
        actions+=/highwind_arrow
        actions+=/multishot,if=active_enemies>=3
        actions+=/celestial_shot,if=focus>=60
        actions+=/focused_shot
        """;

    /// <summary>The talents of the fight 23 Elarion.</summary>
    private static readonly int[] BaseTalents =
    [
        RepeatingStars.TalentId, LunarlightAffinity.TalentId, FerventSupremacy.TalentId, LastLights.TalentId, LunarFury.TalentId,
        PiercingSeekers.TalentId, SkylitGrace.TalentId, Fusillade.TalentId, RisingMoon.TalentId,
    ];

    [Theory]
    [InlineData(FocusedExpanse.TalentId, "aura:Focused Expanse")]
    [InlineData(PiercingSeekers.TalentId, "more:damage:Heartseeker Barrage", 2)]
    [InlineData(ResurgentWinds.TalentId, "aura:Resurgent Winds")]
    [InlineData(DeadlyFocus.TalentId, "more:damage:Focused Shot")]
    [InlineData(Fusillade.TalentId, "more:damage:Heartseeker Barrage")]
    [InlineData(SkywardMunitions.TalentId, "more:damage:Highwind Arrow")]
    [InlineData(RepeatingStars.TalentId, "more:damage:Starfall Volley")]
    [InlineData(LunarFury.TalentId, "more:damage:Lunarlight Salvo")]
    [InlineData(LethalShots.TalentId, "more:damage:Highwind Arrow")]
    [InlineData(RisingMoon.TalentId, "more:aura:Resurgent Winds", 1, ResurgentWinds.TalentId)]
    [InlineData(LunarlightAffinity.TalentId, "more:damage:Lunarlight Salvo")]
    [InlineData(StrikersAim.TalentId, "aura:Striker's Aim", 1, 0, 1)]
    [InlineData(FerventSupremacy.TalentId, "more:damage:Multishot")]
    [InlineData(ImpendingHeartseeker.TalentId, "aura:Impending Heartseeker")]
    [InlineData(FinalCrescendo.TalentId, "aura:Final Cresendo")]
    [InlineData(SwiftReload.TalentId, "more:damage:Highwind Arrow")]
    [InlineData(SkylitGrace.TalentId, "more:aura:Skystrider's Grace")]
    public void Talent_Is_Modeled_And_Takes_Effect(int talentId, string evidence, int minTargets = 1, int withTalent = 0, int maxTargets = 8)
    {
        foreach (int targets in new[] { 1, 8 }.Where(t => t >= minTargets && t <= maxTargets))
        {
            // The base leaves the talent out, so only the talent can show the evidence.
            int[] without = BaseTalents.Where(t => t != talentId).Concat(withTalent > 0 ? [withTalent] : []).Distinct().ToArray();
            var (result, hero, gained) = Run([.. without, talentId], targets);
            Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
            Assert.Contains(hero.Talents, t => t.Id == talentId);
            Assert.True(result.MeanDps > 0);
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

    /// <summary>A sim's enemies never fall below Last Lights' health threshold: Last Lights is checked on its crit chance bonus.</summary>
    [Fact]
    public void Last_Lights_Adds_Crit_Chance_Below_30_Percent_Health()
    {
        var (_, hero, _) = Run(BaseTalents, 1, iterations: 1);
        var low = new EnemyTarget(1) { MaxHealth = 100, CurrentHealth = 29 };
        var high = new EnemyTarget(2) { MaxHealth = 100, CurrentHealth = 31 };
        // Game data: Last Lights adds 20% crit chance.
        Assert.Equal(hero.CritChance(SimTime.Zero) + 0.2, hero.CritChanceAgainst(low, SimTime.Zero), 6);
        Assert.Equal(hero.CritChance(SimTime.Zero), hero.CritChanceAgainst(high, SimTime.Zero), 6);
    }

    [Theory]
    [InlineData(Shimmer.HeadItemId, "debuff:Shimmer")]
    [InlineData(StarstrikersAscent.ChestItemId, "aura:Impending Heartseeker")]
    [InlineData(AstronomersHail.LegsItemId, "more:damage:Starfall Volley")]
    public void Legendary_Is_Modeled_And_Takes_Effect(int itemId, string evidence)
    {
        var (result, hero, gained) = Run(BaseTalents, 3, legendaryItemId: itemId);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Legendary"));
        Assert.Single(hero.Legendaries);
        bool more = evidence.StartsWith("more:");
        string shown = more ? evidence["more:".Length..] : evidence;
        double amount = Amount(result, gained, shown);
        double baseline = 0.0;
        if (more)
        {
            var (baseResult, _, baseGained) = Run(BaseTalents, 3);
            baseline = Amount(baseResult, baseGained, shown);
        }
        output.WriteLine($"Legendary item {itemId}: {result.MeanDps:N0} DPS, {shown} -> {amount:N0} (without: {baseline:N0})");
        Assert.True(amount > baseline, $"Legendary item {itemId}: no {evidence}");
    }

    [Fact]
    public void Every_Talent_In_Elarions_Tree_Is_Modeled()
    {
        Assert.Equal(18, Elarion.TalentTree.Length);
        foreach (var slot in Elarion.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Elarion.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
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
        // The Elarion from pWmcV42K39GHTLQX fight 23 (Wraithtide Vault), as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "elarion_character.json")))!;
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
