using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Talents;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Runs normal (APL) sims with each Ardeos talent that no recorded combat log uses, checking the talent is imported and
/// its effect shows up: its aura is gained or its damage is dealt. The character is the one from the fight 7 replay.
/// </summary>
public class ArdeosTalentSmokeTests(ITestOutputHelper output)
{
    private const string Apl = """
        actions+=/incinerate
        actions+=/wildfire
        actions+=/pyromania
        actions+=/apocalypse
        actions+=/fire_frogs
        actions+=/fire_ball
        actions+=/searing_blaze,if=!debuff.searing_blaze.up
        actions+=/engulfing_flames
        actions+=/detonate
        actions+=/infernal_wave
        """;

    /// <summary>The talents of the Ardeos in yMAZVJL9hTpFjqbC fight 7.</summary>
    private static readonly int[] BaseTalents =
    [
        SlowBurn.TalentId, FrogSquad.TalentId, ApocalypticSurge.TalentId, UndyingFlame.TalentId, RollingFlames.TalentId,
        PyrophibianFrenzy.TalentId, BurningInitiative.TalentId,
    ];

    [Theory]
    [InlineData(GreatBallsOfFire.TalentId, "damage:Fire Ball")]
    [InlineData(Backdraft.TalentId, "damage:Searing Blaze")]
    [InlineData(FlareUp.TalentId, "damage:Flare Up")]
    [InlineData(AgonizingBlaze.TalentId, "damage:Searing Blaze")]
    [InlineData(Firestarter.TalentId, "damage:Fire Frogs")]
    [InlineData(CascadingInferno.TalentId, "aura:Cascading Inferno")]
    [InlineData(CrashAndBurn.TalentId, "damage:Fire Ball")]
    [InlineData(CracklingInferno.TalentId, "damage:Crackling Inferno")]
    [InlineData(ReignOfFire.TalentId, "aura:Reign of Fire")]
    [InlineData(IntensifyingInferno.TalentId, "damage:Infernal Wave")]
    [InlineData(SpontaneousCombustion.TalentId, "damage:Engulfing Flames")]
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
    public void Every_Talent_In_Ardeos_Tree_Is_Modeled()
    {
        Assert.Equal(18, Ardeos.TalentTree.Length);
        foreach (var slot in Ardeos.TalentTree) Assert.NotNull(Catalog.CreateTalent(slot.TalentId));
        var (_, hero, _) = Run(Ardeos.TalentTree.Select(s => s.TalentId), 1, iterations: 1);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
    }

    [Fact]
    public void The_Logged_Build_Spends_Burning_Embers_On_Detonate()
    {
        var (result, _, gained) = Run(BaseTalents, 3);
        output.WriteLine(string.Join(", ", result.AbilityDpsBreakdown.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:N0}")));
        Assert.True(result.AbilityDpsBreakdown.GetValueOrDefault("Detonate") > 0);
        Assert.True(result.AbilityDpsBreakdown.GetValueOrDefault("Fire Frogs") > 0);
        Assert.True(gained.GetValueOrDefault("Apocalyptic Surge") > 0);
    }

    /// <summary>
    /// Fire Toad (Pyromancer Helm) turns frogs into toads: Fire Frogs deal more, on one target and on many, than with
    /// Explosivo (Pyromancer Robe), which leaves frogs alone; both legendaries bring the same legendary stats.
    /// </summary>
    [Fact]
    public void Fire_Toad_Raises_Fire_Frogs_Damage()
    {
        foreach (int targets in new[] { 1, 8 })
        {
            var (without, _, _) = Run(BaseTalents, targets, legendaryItemIds: [Explosivo.ChestItemId]);
            var (with, hero, _) = Run(BaseTalents, targets, legendaryItemIds: [FireToad.HeadItemId]);
            Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Legendary"));
            double before = without.AbilityDpsBreakdown.GetValueOrDefault("Fire Frogs");
            double after = with.AbilityDpsBreakdown.GetValueOrDefault("Fire Frogs");
            output.WriteLine($"Fire Toad on {targets} target(s): Fire Frogs {before:N0} -> {after:N0} DPS, total {without.MeanDps:N0} -> {with.MeanDps:N0}");
            Assert.True(after > before * 1.2, $"Fire Frogs {before:N0} -> {after:N0} DPS on {targets} target(s)");
        }
    }

    private static (MonteCarloResult Result, Hero Hero, Dictionary<string, int> Gained) Run(IEnumerable<int> talentIds, int targets, int iterations = 10,
        int[]? legendaryItemIds = null)
    {
        // The Ardeos from yMAZVJL9hTpFjqbC fight 7, as the replay imported it.
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ardeos_character.json")))!;
        build.Talents = talentIds.ToList();
        // The fixture's legendary unless others are given.
        if (legendaryItemIds != null) build.LegendaryItemIds = [.. legendaryItemIds];
        var hero = HeroImporter.Import(build);
        var gained = new Dictionary<string, int>();
        hero.Auras.OnGained += (aura, _, _) => gained[aura.Def.Name] = gained.GetValueOrDefault(aura.Def.Name) + 1;
        // One thread: the simulator builds one hero, the one above.
        var result = new MonteCarloSimulator(() => hero, AplParser.ParseScript(Apl))
            .Run(SimTime.FromSeconds(300), iterations, targets, degreeOfParallelism: 1);
        return (result, hero, gained);
    }
}
