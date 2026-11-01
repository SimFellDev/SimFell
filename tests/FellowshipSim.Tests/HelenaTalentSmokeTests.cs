using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Helena;
using FellowshipSim.Core.Heroes.Helena.Abilities;
using FellowshipSim.Core.Heroes.Helena.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Helena talent and legendary, checking each is imported and its effect shows up: its aura
/// is gained or its damage is dealt. The character is the one from the 1p7AXmHfwV9CYMxR fight 30 replay (Roots).
/// </summary>
public class HelenaTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions+=/attack
        actions+=/siegebreaker
        actions+=/hold_the_line,if=cooldown.shield_slam.charges=0
        actions+=/shields_up
        actions+=/fated_strike
        actions+=/shockwave
        actions+=/shield_slam
        actions+=/shield_throw
        actions+=/sweeping_strike
        actions+=/power_strike
        actions+=/charge
        actions+=/measured_strike
        """;

    private static readonly int[] BaseTalents =
    [
        MartialCommand.TalentId, GleamingShield.TalentId, FrontLineDefender.TalentId, GreaterShockwave.TalentId,
        HighCommand.TalentId, GuardedVeteran.TalentId,
    ];

    [Theory]
    [InlineData(SwordAndBoard.TalentId, "aura:Empowered Shield Slam")]
    [InlineData(LingeringRush.TalentId, "aura:Sweeping Strike: Blood Rally")]
    [InlineData(Aftershock.TalentId, "damage:Shockwave")]
    [InlineData(SecondWind.TalentId, "aura:Second Wind")]
    [InlineData(Defiance.TalentId, "aura:Defiance")]
    [InlineData(SkullCracker.TalentId, "damage:Shield Throw")]
    [InlineData(SharpenedBlade.TalentId, "damage:Power Strike")]
    [InlineData(TheBestDefense.TalentId, "damage:Shield Throw")]
    [InlineData(PunishingStrikes.TalentId, "damage:Measured Strike")]
    [InlineData(UnscarredSoul.TalentId, "damage:Shield Slam")]
    [InlineData(ShieldMastery.TalentId, "damage:Shield Slam")]
    [InlineData(MasterOfWar.TalentId, "damage:Shield Slam")]
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

    /// <summary>Sharpened Blade: Power Strike deals more to an enemy with Sweeping Strike's bleed than without the talent.</summary>
    [Fact]
    public void Sharpened_Blade_Raises_Power_Strike_Damage()
    {
        var (without, _, _) = Run(BaseTalents, 1);
        var (with, _, _) = Run([.. BaseTalents, SharpenedBlade.TalentId], 1);
        double before = without.AbilityDpsBreakdown.GetValueOrDefault(PowerStrike.AbilityName);
        double after = with.AbilityDpsBreakdown.GetValueOrDefault(PowerStrike.AbilityName);
        output.WriteLine($"Sharpened Blade: Power Strike {before:N0} -> {after:N0} DPS");
        Assert.True(after > before, $"Power Strike {before:N0} -> {after:N0} DPS");
    }

    [Theory]
    [InlineData(RazorCrash.BreastplateItemId, "damage:Razor Shrapnel")]
    [InlineData(IronSentinel.HelmItemId, "aura:Empowered Shield Slam")]
    [InlineData(VanguardsAdvance.GreavesItemId, "damage:Shockwave")]
    public void Legendary_Is_Modeled_And_Takes_Effect(int itemId, string evidence)
    {
        var (without, _, gainedWithout) = Run(BaseTalents, 3, legendaryItemIds: []);
        var (with, hero, gained) = Run(BaseTalents, 3, legendaryItemIds: [itemId]);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Legendary"));
        Assert.Single(hero.Legendaries);
        string[] parts = evidence.Split(':', 2);
        double baseline = parts[0] == "aura" ? gainedWithout.GetValueOrDefault(parts[1]) : without.AbilityDpsBreakdown.GetValueOrDefault(parts[1]);
        double amount = parts[0] == "aura" ? gained.GetValueOrDefault(parts[1]) : with.AbilityDpsBreakdown.GetValueOrDefault(parts[1]);
        output.WriteLine($"Legendary item {itemId}: {with.MeanDps:N0} DPS, {evidence} -> {amount:N0} (without: {baseline:N0})");
        Assert.True(amount > baseline, $"Legendary item {itemId}: no more {evidence}");
    }

    [Fact]
    public void Every_Talent_In_Helenas_Tree_Is_Modeled()
    {
        Assert.Equal(18, Helena.TalentTree.Length);
        foreach (var slot in Helena.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Helena.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
    }

    private static Hero Import(IEnumerable<int> talentIds, int[]? legendaryItemIds = null)
    {
        // The Helena from 1p7AXmHfwV9CYMxR fight 30 (Roots), as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "helena_character.json")))!;
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
