using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Helena;
using FellowshipSim.Core.Heroes.Helena.Abilities;
using FellowshipSim.Core.Heroes.Helena.Talents;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;
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
    [InlineData(Aftershock.TalentId, "damage:Shockwave")]
    [InlineData(Defiance.TalentId, "aura:Defiance")]
    [InlineData(SkullCracker.TalentId, "damage:Shield Throw")]
    [InlineData(SharpenedBlade.TalentId, "damage:Power Strike")]
    public void Talent_Is_Modeled_And_Takes_Effect(int talentId, string evidence)
    {
        foreach (int targets in new[] { 1, 8 })
        {
            var (without, _, gainedWithout) = Run(BaseTalents, targets);
            var (result, hero, gained) = Run([.. BaseTalents, talentId], targets);
            Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Talent"));
            Assert.True(result.MeanDps > 0);
            string[] parts = evidence.Split(':', 2);
            double baseline = parts[0] == "aura" ? gainedWithout.GetValueOrDefault(parts[1]) : without.AbilityDpsBreakdown.GetValueOrDefault(parts[1]);
            double amount = parts[0] == "aura" ? gained.GetValueOrDefault(parts[1]) : result.AbilityDpsBreakdown.GetValueOrDefault(parts[1]);
            output.WriteLine($"Talent {talentId} on {targets} target(s): {result.MeanDps:N0} DPS, {evidence} -> {amount:N0} (without: {baseline:N0})");
            Assert.True(amount > baseline, $"Talent {talentId}: no more {evidence} on {targets} target(s)");
        }
    }

    /// <summary>A defensive proc's talent changes nothing in a sim: its aura is never gained, its hit never dealt.</summary>
    [Theory]
    [InlineData(TheBestDefense.TalentId, "aura:The Best Defense")]
    [InlineData(PunishingStrikes.TalentId, "aura:Punishing Strikes")]
    [InlineData(SecondWind.TalentId, "aura:Second Wind")]
    [InlineData(UnscarredSoul.TalentId, "damage:Unscarred Soul")]
    public void A_Defensive_Proc_Talent_Does_Nothing_In_A_Sim(int talentId, string evidence)
    {
        var (result, hero, gained) = Run([.. BaseTalents, talentId], 3);
        Assert.Single(hero.DefensiveProcs);
        string[] parts = evidence.Split(':', 2);
        Assert.Equal(0.0, parts[0] == "aura" ? gained.GetValueOrDefault(parts[1]) : result.AbilityDpsBreakdown.GetValueOrDefault(parts[1]));
    }

    /// <summary>
    /// A replay fires each defensive proc talent at its proof in the log: The Best Defense resets Shield Throw and gives its
    /// buff, Punishing Strikes gives 2 stacks, Second Wind its buff, and Unscarred Soul deals the logged reflect hit.
    /// </summary>
    [Theory]
    [InlineData(TheBestDefense.TalentId)]
    [InlineData(PunishingStrikes.TalentId)]
    [InlineData(SecondWind.TalentId)]
    [InlineData(UnscarredSoul.TalentId)]
    public void A_Replay_Fires_A_Defensive_Proc_Talent_At_Its_Proof(int talentId)
    {
        var character = Character();
        character.Talents = [.. BaseTalents, talentId];
        var enemy = new UnitRef(100, 1);
        var proof = talentId switch
        {
            TheBestDefense.TalentId => TheBestDefense.Buff.LogId,
            PunishingStrikes.TalentId => PunishingStrikes.Buff.LogId,
            SecondWind.TalentId => SecondWind.Buff.LogId,
            _ => LogIds.Effect(UnscarredSoul.ReflectEffectId),
        };
        var script = new ReplayScript
        {
            Schema = ReplayScript.SchemaVersion,
            ReportCode = "test",
            FightId = 1,
            FightName = "Test",
            DurationSeconds = 10,
            Player = new ReplayPlayer { Id = 1, Name = "Tester", Hero = "Helena" },
            Character = character,
            Commands = [new ReplayCommand { Time = 0.5, Kind = CommandKind.EnemyArise, Enemy = enemy, Name = "Dummy", MaxHp = 1e9, Hp = 1e9 }],
            Observations = new ReplayObservations
            {
                Auras = talentId == UnscarredSoul.TalentId ? [] :
                [
                    new LogAura { Time = 5.0, Type = "applybuff", AbilityId = proof, Source = new UnitRef(1, 1), Target = new UnitRef(1, 1),
                        Stack = talentId == PunishingStrikes.TalentId ? PunishingStrikes.StacksPerProc : null },
                ],
                Damage = talentId == UnscarredSoul.TalentId
                    ? [new LogDamage { Time = 5.0, AbilityId = proof, AbilityName = UnscarredSoul.TalentName, Target = enemy, Amount = 1000 }]
                    : [],
            },
        };
        var hero = ReplayDriver.CreateHero(script);
        var gains = new List<(string Name, int Stacks)>();
        hero.Auras.OnGained += (aura, _, now) => gains.Add((aura.Def.Name, aura.Stacks));
        var result = new ReplayDriver(hero, script, new ReplayOptions { StopOnFirstFailure = false }).Run();
        switch (talentId)
        {
            case TheBestDefense.TalentId:
                Assert.Contains(gains, g => g.Name == TheBestDefense.TalentName);
                break;
            case PunishingStrikes.TalentId:
                Assert.Contains(gains, g => g.Name == PunishingStrikes.TalentName && g.Stacks == PunishingStrikes.StacksPerProc);
                break;
            case SecondWind.TalentId:
                Assert.Contains(gains, g => g.Name == SecondWind.TalentName);
                break;
            default:
                Assert.Contains(result.AllHits, h => h.AbilityId == proof && h.Log != null);
                break;
        }
    }

    /// <summary>Lingering Rush: Blood Rally lasts 6 seconds more.</summary>
    [Fact]
    public void Lingering_Rush_Lengthens_Blood_Rally()
    {
        var without = Import(BaseTalents).GetAbility<SweepingStrike>()!;
        var with = Import([.. BaseTalents, LingeringRush.TalentId]).GetAbility<SweepingStrike>()!;
        Assert.Equal(SweepingStrike.BloodRallySeconds, without.BloodRallyBuff.Duration.TotalSeconds);
        Assert.Equal(SweepingStrike.BloodRallySeconds + LingeringRush.BloodRallyBonusSeconds, with.BloodRallyBuff.Duration.TotalSeconds);
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

    /// <summary>The Helena from 1p7AXmHfwV9CYMxR fight 30 (Roots), as the replay imported it.</summary>
    private static CharacterBuild Character() => System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "helena_character.json")))!;

    private static Hero Import(IEnumerable<int> talentIds, int[]? legendaryItemIds = null)
    {
        var build = Character();
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
