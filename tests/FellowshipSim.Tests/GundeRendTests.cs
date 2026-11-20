using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Editing;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Dungeons.Affixes;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using FellowshipSim.Core.Heroes.Gunde.Talents;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Rend and the mechanics that read it, from the Blueprint bytecode of build 26320967: the debuff's stacks, Massacre's
/// stacks, Bloodbound Spirit's share, Sundered Flesh and Owed in Blood's saved damage. The character is the one from the
/// Y4wpb2f3gc8LqJDX fight 25 replay.
/// </summary>
public class GundeRendTests
{
    private const double Health = 1_000_000_000;

    [Theory]
    [InlineData(2.51, 3)]
    [InlineData(2.49, 2)]
    [InlineData(7.0, 7)]
    [InlineData(0.1, 1)]
    public void Rend_Stacks_Are_The_Raw_Total_In_Strength_Rounded(double strengths, int stacks)
    {
        foreach (bool deepRend in new[] { false, true })
        {
            var (hero, ctx, enemy) = Fight(deepRend ? [DeepRend.TalentId] : []);
            double strength = hero.PrimaryAttribute(ctx.CurrentTime);
            hero.Rend.Apply(ctx, enemy, strengths * strength);

            Assert.Equal(stacks, enemy.Auras.GetStacks(Rend.Debuff, ctx.CurrentTime));
            Assert.Equal(strengths * strength, hero.Rend.RawTotal(enemy), 6);
            // Game data: tuning ReducedTickSpeed.PeriodFrequencyIncrease of Deep Rend (talent 751) is 1.1.
            double tickRate = deepRend ? 1.1 : 1.0;
            Assert.Equal(strengths * strength * tickRate, hero.Rend.Remaining(enemy), 6);
        }
    }

    [Fact]
    public void An_Action_List_Reads_Rend_Stacks_On_The_Target()
    {
        var vocabulary = AplVocabulary.For("Gunde");
        Assert.Contains(vocabulary.Auras, a => a.Slug == "rend" && a.Name == Rend.Debuff.Name);
        foreach (string condition in new[] { "debuff.rend.stack>=200", "dot.rend.stack>=200", "active_dot.rend>=3" })
        {
            var check = vocabulary.Check(condition);
            Assert.Null(check.Error);
            Assert.DoesNotContain(check.Tokens, t => t.Problem != null);
        }
        Assert.Contains(Completions.For(vocabulary, "debuff.re"), c => c.Text == "rend");

        var (hero, ctx, enemy) = Fight([]);
        var stacks = AplParser.ParseExpression("debuff.rend.stack");
        var bound = BoundExpression.Bind(stacks, hero);
        Assert.Equal(0, stacks.EvaluateNumeric(hero, ctx));
        Assert.Equal(0, bound.EvaluateNumeric(ctx));

        hero.Rend.Apply(ctx, enemy, 7 * hero.PrimaryAttribute(ctx.CurrentTime));
        Assert.Equal(enemy.Auras.GetStacks(Rend.Debuff, ctx.CurrentTime), stacks.EvaluateNumeric(hero, ctx));
        Assert.Equal(7, stacks.EvaluateNumeric(hero, ctx));
        Assert.Equal(7, bound.EvaluateNumeric(ctx));

        // Slaughter takes every stack.
        hero.GetAbility<Slaughter>()!.Execute(ctx, enemy);
        Assert.Equal(0, stacks.EvaluateNumeric(hero, ctx));
        Assert.Equal(0, bound.EvaluateNumeric(ctx));
    }

    [Fact]
    public void Massacre_Gains_A_Stack_Per_Rend_Stack_Slaughter_Consumes()
    {
        var (hero, ctx, first) = Fight([Massacre.TalentId]);
        var second = ctx.AddEnemy(101, 2, "Enemy", maxHealth: Health);
        double strength = hero.PrimaryAttribute(ctx.CurrentTime);
        hero.Rend.Apply(ctx, first, 3 * strength);
        hero.Rend.Apply(ctx, second, 5 * strength);
        var slaughter = hero.GetAbility<Slaughter>()!;

        slaughter.Execute(ctx, first);
        Assert.Equal(8, hero.Auras.GetStacks(Massacre.Buff, ctx.CurrentTime));

        // A second Slaughter adds its stacks, up to the maximum.
        // Game data: tuning StacksToHaste.MaxStacks of Massacre (talent 772) is 100.
        const int maxStacks = 100;
        hero.Rend.Apply(ctx, first, 2 * maxStacks * strength);
        slaughter.Execute(ctx, first);
        Assert.Equal(maxStacks, hero.Auras.GetStacks(Massacre.Buff, ctx.CurrentTime));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bloodbound_Spirit_Scales_The_Whole_Share(bool reignInBlood)
    {
        var (hero, ctx, enemy) = Fight([]);
        if (reignInBlood) hero.Auras.Apply(ReignInBlood.Buff, ctx.CurrentTime);
        double dealt = 0.0;
        hero.OnDamageDealt += (in DamageContext hit) =>
        {
            if (hit.AbilityName == BloodboundSpirit.AbilityName) dealt += hit.Amount;
        };

        hero.GetAbility<BloodboundSpirit>()!.Execute(ctx, enemy);
        ctx.RunUntil(SimTime.FromSeconds(BloodboundSpirit.FirstPulseSeconds));

        // Game data: tunings Gunde.PerTargetAccumulatedBleed.DamageToDotScaler (0.5) and DotScaler (2) of Bloodbound Spirit
        // (ability 2296), and BonusDotDamage (0.3) of Reign in Blood (ability 2295).
        double share = (0.5 + (reignInBlood ? 0.3 : 0.0)) * 2;
        Assert.True(dealt > 0.0);
        Assert.Equal(dealt * share, hero.Rend.RawTotal(enemy), 6);
    }

    [Fact]
    public void Slaughter_Ticks_Take_The_Targets_Damage_Taken_And_No_Target_Multiplier_Of_Gunde()
    {
        var (hero, ctx, enemy) = Fight([]);
        hero.TargetDamageMultipliers.Add((_, _) => 2.0);
        enemy.Auras.Apply(AnomalousOrbs.Instability, ctx.CurrentTime);
        var ticks = new List<double>();
        hero.OnDamageDealt += (in DamageContext hit) =>
        {
            if (hit.AbilityName == Slaughter.AbilityName) ticks.Add(hit.Amount);
        };
        hero.Rend.Apply(ctx, enemy, 10 * hero.PrimaryAttribute(ctx.CurrentTime));
        double consumed = hero.Rend.Remaining(enemy);

        hero.GetAbility<Slaughter>()!.Execute(ctx, enemy);
        ctx.RunUntil(ctx.CurrentTime + SimTime.FromSeconds(Slaughter.Constants.DotPeriod));

        Assert.Single(ticks);
        double expected = consumed * 1.6 / 10 * 4;
        Assert.InRange(ticks[0], expected * (1.0 - RandomOutcomeSource.DamageSpread), expected * (1.0 + RandomOutcomeSource.DamageSpread));
    }

    [Fact]
    public void Sundered_Flesh_Never_Fires_On_An_Application()
    {
        var (hero, ctx, enemy) = Fight([SunderedFlesh.TalentId], health: 1_000);
        hero.Rend.Apply(ctx, enemy, 50_000);
        Assert.Equal(1_000, enemy.CurrentHealth);
        Assert.True(hero.Rend.IsOn(enemy));
    }

    [Fact]
    public void Sundered_Flesh_Deals_Health_Plus_One_After_A_Tick_And_Leaves_Rend()
    {
        const double rend = 50_000, health = 10_000;
        var (hero, ctx, enemy) = Fight([SunderedFlesh.TalentId], health: health);
        // Each Rend hit with the enemy's health before it (the fixture's gear procs deal damage of their own on a hit).
        var hits = new List<(double Amount, double HealthBefore)>();
        double healthBefore = health;
        hero.OnDamageDealt += (in DamageContext hit) =>
        {
            if (hit.AbilityId == Rend.AbilityId) hits.Add((hit.Amount, healthBefore));
            healthBefore = enemy.CurrentHealth;
        };
        hero.Rend.Apply(ctx, enemy, rend);
        ctx.RunUntil(SimTime.FromSeconds(Rend.TickSeconds));

        // The tick deals its bucket; the enemy then holds less health than the Rend left and takes its health plus one.
        Assert.Equal(2, hits.Count);
        Assert.Equal(rend / Rend.TicksPerApplication, hits[0].Amount, 6);
        Assert.Equal(hits[1].HealthBefore + 1.0, hits[1].Amount, 6);
        Assert.Equal(0.0, enemy.CurrentHealth);
        Assert.True(hero.Rend.IsOn(enemy));
    }

    [Fact]
    public void Sundered_Flesh_Skips_Training_Dummies()
    {
        const double rend = 50_000;
        var (hero, ctx, enemy) = Fight([SunderedFlesh.TalentId], health: 10_000);
        enemy.IsTrainingDummy = true;
        var hits = RendHits(hero);
        hero.Rend.Apply(ctx, enemy, rend);
        ctx.RunUntil(SimTime.FromSeconds(Rend.TickSeconds));
        Assert.Single(hits);
        Assert.True(enemy.CurrentHealth > 0.0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Owed_In_Blood_Adds_A_Strength_Per_Stack_As_Raw_Rend(bool deepRend)
    {
        var (hero, ctx, enemy) = Fight(deepRend ? [DeepRend.TalentId] : []);
        double strength = hero.PrimaryAttribute(ctx.CurrentTime);
        // The fight starts with stacks picked up before it; their removal empties the saved damage.
        hero.Auras.Remove(OwedInBlood.Buff, ctx.CurrentTime);
        for (int i = 0; i < 3; i++) hero.Auras.Apply(OwedInBlood.Buff, ctx.CurrentTime);

        hero.GetAbility<OwedInBlood>()!.Execute(ctx, enemy);

        Assert.Equal(3 * strength, hero.Rend.RawTotal(enemy), 6);
        Assert.Equal(3, enemy.Auras.GetStacks(Rend.Debuff, ctx.CurrentTime));
        Assert.Equal(0, hero.Auras.GetStacks(OwedInBlood.Buff, ctx.CurrentTime));
    }

    [Fact]
    public void Owed_In_Blood_Saves_At_Most_Its_Maximum_Stacks_Of_Strength()
    {
        const double strength = 1_000;
        // Game data: tuning MaxStacks of Owed in Blood (ability 2298) is 150.
        const int maxStacks = 150;
        Assert.Equal(strength, OwedInBlood.SavedAfterPickup(0.0, 1, strength));
        Assert.Equal(maxStacks * strength, OwedInBlood.SavedAfterPickup((maxStacks - 1) * strength, 5, strength));
        // A pickup at a higher Strength raises the cap with it.
        Assert.Equal((maxStacks + 2) * strength, OwedInBlood.SavedAfterPickup(maxStacks * strength, 1, 2 * strength), 6);
    }

    private static List<double> RendHits(Gunde hero)
    {
        var hits = new List<double>();
        hero.OnDamageDealt += (in DamageContext hit) =>
        {
            if (hit.AbilityId == Rend.AbilityId) hits.Add(hit.Amount);
        };
        return hits;
    }

    private static (Gunde Hero, SimulationContext Ctx, EnemyTarget Enemy) Fight(int[] talentIds, double health = Health)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gunde_character.json")))!;
        build.Talents = [.. talentIds];
        var hero = (Gunde)HeroImporter.Import(build);
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        var enemy = ctx.AddEnemy(100, 1, "Enemy", maxHealth: health);
        hero.CurrentTarget = enemy;
        return (hero, ctx, enemy);
    }
}
