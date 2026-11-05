using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.ProtectiveEnchants;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Mara talent and legendary, checking it is imported and its effect shows up: its
/// aura is gained or its damage is dealt. The character is the one from the Fc8v6yWxD14GQMnV fight 19 replay.
/// </summary>
public class MaraTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions+=/attack
        actions+=/matriarch_macabre
        actions+=/macabre_stratagem
        actions+=/final_stratagem
        actions+=/maiden_of_death
        actions+=/sahrils_wrath
        actions+=/brooding_shadows,if=combo_points=0
        actions+=/stealth_skittering_blades,if=active_enemies>=3
        actions+=/widows_bite,if=buff.brooding_shadows.up
        actions+=/stealth_backstab
        actions+=/hemorrhaging_strike,if=combo_points>=6&(active_enemies<3|buff.maiden_of_death.up)
        actions+=/arachnid_assault,if=combo_points>=6&active_enemies>=3
        actions+=/queens_fang,if=combo_points>=6
        actions+=/widows_bite,if=energy<170
        actions+=/skittering_blades,if=active_enemies>=3
        actions+=/backstab
        """;

    /// <summary>The talents of the fight 19 Mara.</summary>
    private static readonly int[] BaseTalents =
    [
        SinnersPride.TalentId, AssassinsGuile.TalentId, FeedTheQueen.TalentId, Malevolence.TalentId, DeadlyScheme.TalentId,
        VenomousDelight.TalentId, MacabreStratagemTalent.TalentId,
    ];

    [Theory]
    // Red Ledger's few % of crit chance is lost in the crit rolls of 10 fights.
    [InlineData(RedLedger.TalentId, "more:dps", 1, 1, 50)]
    [InlineData(CorrosiveSpill.TalentId, "damage:Corrosive Spill")]
    [InlineData(AssassinsGuile.TalentId, "aura:Assassin's Guile")]
    [InlineData(Bloodrush.TalentId, "more:damage:Hemorrhage")]
    [InlineData(EfficientKiller.TalentId, "more:energy")]
    [InlineData(GushingBlood.TalentId, "more:damage:Hemorrhage")]
    [InlineData(FeedTheQueen.TalentId, "aura:Feed the Queen", 3)]
    [InlineData(DeadlyScheme.TalentId, "aura:Deadly Scheme: Active")]
    [InlineData(MacabreStratagemTalent.TalentId, "more:aura:Matriarch Macabre")]
    [InlineData(SeethingBurst.TalentId, "damage:Seething Burst", 1, 1)]
    [InlineData(Hemotoxin.TalentId, "damage:Hemotoxin")]
    [InlineData(SinnersPride.TalentId, "more:aura:Maiden of Death")]
    [InlineData(Malevolence.TalentId, "aura:Malevolence: Arachnid Assault", 1, 1)]
    [InlineData(Malevolence.TalentId, "aura:Malevolence: Queen's Fang", 8)]
    [InlineData(ArachnidOnslaught.TalentId, "more:damage:Arachnid Assault", 8)]
    [InlineData(CausticWounds.TalentId, "more:damage:Caustic Poison", 1, 1)]
    [InlineData(Puncture.TalentId, "more:damage:Widow's Bite")]
    public void Talent_Is_Modeled_And_Takes_Effect(int talentId, string evidence, int minTargets = 1, int maxTargets = 8,
        int iterations = 10)
    {
        foreach (int targets in new[] { 1, 8 }.Where(t => t >= minTargets && t <= maxTargets))
        {
            // The base leaves the talent out, so only the talent can show the evidence.
            int[] without = BaseTalents.Where(t => t != talentId).ToArray();
            var (result, hero, gained) = Run([.. without, talentId], targets, iterations);
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
                var (baseResult, _, baseGained) = Run(without, targets, iterations);
                baseline = Amount(baseResult, baseGained, shown);
            }
            output.WriteLine($"Talent {talentId} on {targets} target(s): {result.MeanDps:N0} DPS, {shown} -> {amount:N0} (without: {baseline:N0})");
            Assert.True(amount > baseline, $"Talent {talentId}: no {evidence} on {targets} target(s)");
        }
    }

    /// <summary>
    /// A list that starves Mara of Energy: Brooding Shadows, then Backstab (its Stealth cast while Brooding Shadows is up), as often as they
    /// come up. Energy then runs out, so the talent's Energy is never lost to the cap and the rotation does not change with it. (With the full
    /// list the talent's extra Energy changes what is cast and the total Energy gained can fall.)
    /// Plain Backstab cannot be used in Stealth, so a list of Brooding Shadows and Backstab alone casts nothing after the first Brooding Shadows.
    /// </summary>
    private const string StarvingApl = """
        actions+=/brooding_shadows
        actions+=/stealth_backstab
        actions+=/backstab
        """;

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Venomous_Delight_Gains_Energy_When_Starved(int targets)
    {
        int[] without = BaseTalents.Where(t => t != VenomousDelight.TalentId).ToArray();
        var (result, hero, gained) = Run([.. without, VenomousDelight.TalentId], targets, apl: StarvingApl);
        var (_, _, baseGained) = Run(without, targets, apl: StarvingApl);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
        double with = gained.GetValueOrDefault(EnergyGained), baseline = baseGained.GetValueOrDefault(EnergyGained);
        output.WriteLine($"Venomous Delight on {targets} target(s): {result.MeanDps:N0} DPS, energy -> {with:N0} (without: {baseline:N0})");
        Assert.True(with > baseline, $"Venomous Delight: no more Energy on {targets} target(s) ({with:N0} against {baseline:N0})");
    }

    /// <summary>A sim's enemies never fall to 30% health: Maiden's Doom is checked on an enemy at 20%.</summary>
    [Fact]
    public void Maidens_Doom_Raises_Maiden_Of_Death_Against_Low_Enemies()
    {
        var (_, hero, _) = Run([.. BaseTalents, MaidensDoom.TalentId], 1, iterations: 1);
        var maiden = hero.GetAbility<MaidenOfDeath>()!;
        var low = new EnemyTarget(1) { MaxHealth = 100, CurrentHealth = 20 };
        var healthy = new EnemyTarget(2) { MaxHealth = 100, CurrentHealth = 90 };
        Assert.Equal(MaidensDoom.MaidenDamage, maiden.DamageAgainst(low));
        Assert.Equal(MaidenOfDeath.BuffDamage, maiden.DamageAgainst(healthy));
    }

    [Theory]
    [InlineData(VexirasVenom.LegsItemId, "damage:Vexira's Venom")]
    [InlineData(DrenchedInBlood.ChestItemId, "aura:Drenched in Blood")]
    [InlineData(FromTheShadows.HelmItemId, "damage:Queen's Fang (shadowy clone)")]
    public void Legendary_Is_Modeled_And_Takes_Effect(int itemId, string evidence)
    {
        var (result, hero, gained) = Run(BaseTalents, 3, legendaryItemId: itemId);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Legendary"));
        Assert.Single(hero.Legendaries);
        double amount = Amount(result, gained, evidence);
        output.WriteLine($"Legendary item {itemId}: {result.MeanDps:N0} DPS, {evidence} -> {amount:N0}");
        Assert.True(amount > 0, $"Legendary item {itemId}: no {evidence}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Base_Build_Deals_Damage_With_Every_Ability_It_Uses(int targets)
    {
        var (result, _, gained) = Run(BaseTalents, targets);
        output.WriteLine($"{targets} target(s): {result.MeanDps:N0} DPS");
        foreach (var (name, dps) in result.AbilityDpsBreakdown.OrderByDescending(kv => kv.Value)) output.WriteLine($"  {name,-36} {dps,10:N0}");
        foreach (var (name, count) in gained.OrderByDescending(kv => kv.Value)) output.WriteLine($"  aura {name,-31} {count,10:N0}");
        Assert.True(result.AbilityDpsBreakdown.GetValueOrDefault(targets == 1 ? QueensFang.AbilityName : ArachnidAssault.AbilityName) > 0);
        Assert.True(result.AbilityDpsBreakdown.GetValueOrDefault(Attack.AbilityName) > 0);
    }

    [Fact]
    public void Every_Talent_In_Maras_Tree_Is_Modeled()
    {
        Assert.Equal(18, Mara.TalentTree.Length);
        foreach (var slot in Mara.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Mara.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
    }

    [Theory]
    [InlineData(false, ShadowWard.BuffSeconds)]
    [InlineData(true, ShadowWard.BuffSeconds + TheTower.ShadowWardBonusSeconds)]
    public void Shadow_Ward_Lasts_Its_Duration_And_Waits_For_Its_Cooldown(bool theTower, double seconds)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mara_character.json")))!;
        if (theTower) build.Augmentations.Add(new BuildEntry(TheTower.ProtectiveEnchantId));
        var hero = HeroImporter.Import(build);
        var gains = new List<double>();
        var ends = new List<double>();
        hero.Auras.OnGained += (aura, _, now) => { if (aura.Def.Key == ShadowWard.Buff.Key) gains.Add(now.TotalSeconds); };
        hero.Auras.OnRemoved += (aura, now) => { if (aura.Def.Key == ShadowWard.Buff.Key) ends.Add(now.TotalSeconds); };

        new MonteCarloSimulator(() => hero, AplParser.ParseScript("actions+=/shadow_ward\nactions+=/backstab"))
            .Run(SimTime.FromSeconds(100), 1, 1, degreeOfParallelism: 1);

        Assert.True(gains.Count >= 2, $"Shadow Ward was gained {gains.Count} times");
        Assert.Equal(seconds + AuraContainer.ExpiryLagSeconds, ends[0] - gains[0], 2);
        double cooldown = ShadowWard.CooldownSeconds * (1.0 - hero.Stats.CooldownReductionPct);
        Assert.InRange(gains[1] - gains[0], cooldown / hero.CooldownRate(hero.GetAbility<ShadowWard>()!, SimTime.Zero) - 0.01, cooldown + 1.5);
    }

    /// <summary>Gains of an "aura:" evidence, the DPS of a "damage:" one, the Energy gained ("energy") or the DPS ("dps").</summary>
    private static double Amount(MonteCarloResult result, Dictionary<string, int> gained, string evidence)
    {
        string[] parts = evidence.Split(':', 2);
        return parts[0] switch
        {
            "aura" => gained.GetValueOrDefault(parts[1]),
            "energy" => gained.GetValueOrDefault(EnergyGained),
            "dps" => result.MeanDps,
            _ => result.AbilityDpsBreakdown.GetValueOrDefault(parts[1]),
        };
    }

    /// <summary>The key under which <see cref="Run"/> counts the Energy gained (not regenerated), past the cap included.</summary>
    private const string EnergyGained = "energy gained";

    private static (MonteCarloResult Result, Hero Hero, Dictionary<string, int> Gained) Run(IEnumerable<int> talentIds, int targets,
        int iterations = 10, int? legendaryItemId = null, string apl = Apl)
    {
        // The Mara from Fc8v6yWxD14GQMnV fight 19 (Culex), as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mara_character.json")))!;
        build.Talents = talentIds.ToList();
        build.LegendaryItemIds = legendaryItemId is { } id ? [id] : [];
        var hero = HeroImporter.Import(build);
        var gained = new Dictionary<string, int>();
        hero.Auras.OnGained += (aura, _, _) => gained[aura.Def.Name] = gained.GetValueOrDefault(aura.Def.Name) + 1;
        hero.OnResourceGained += (resource, amount, _, _) =>
        {
            if (resource == Mara.Energy) gained[EnergyGained] = gained.GetValueOrDefault(EnergyGained) + (int)amount;
        };
        // Energy past the cap counts too: near the cap, a talent's Energy pushes other gains over it.
        hero.OnResourceOverflow += (resource, amount, _) =>
        {
            if (resource == Mara.Energy) gained[EnergyGained] = gained.GetValueOrDefault(EnergyGained) + (int)amount;
        };
        // One thread: the simulator builds one hero, the one above.
        var result = new MonteCarloSimulator(() => hero, AplParser.ParseScript(apl))
            .Run(SimTime.FromSeconds(300), iterations, targets, degreeOfParallelism: 1);
        return (result, hero, gained);
    }
}
