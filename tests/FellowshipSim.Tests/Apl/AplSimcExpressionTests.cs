using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Apl;

/// <summary>
/// The SimulationCraft expressions the other heroes' lists use: dot (a debuff), active_dot (the enemies with a debuff),
/// charges_fractional, and Ardeos's damage over time read as debuffs.
/// </summary>
public class AplSimcExpressionTests
{
    [Fact]
    public void A_Dot_Is_A_Debuff_And_Ticking_Is_Up()
    {
        var (hero, ctx) = Fight("Rime", enemies: 1);
        ctx.Player.CurrentTarget.Auras.Apply(BurstingIce.Debuff, ctx.CurrentTime);
        var expires = ctx.Player.CurrentTarget.Auras.FindActive("bursting_ice", ctx.CurrentTime)!.ExpiresAt;
        AssertReads(1.0, "dot.bursting_ice.ticking", hero, ctx);
        AssertReads(1.0, "debuff.bursting_ice.ticking", hero, ctx);
        AssertReads((expires - ctx.CurrentTime).TotalSeconds, "dot.bursting_ice.remains", hero, ctx);
        AssertReads(0.0, "dot.bursting_ice.refreshable", hero, ctx);
    }

    [Fact]
    public void Active_Dot_Counts_The_Enemies_With_The_Debuff_Until_It_Runs_Out()
    {
        var (hero, ctx) = Fight("Rime", enemies: 3);
        AssertReads(0.0, "active_dot.bursting_ice", hero, ctx);
        ctx.AllEnemies[0].Auras.Apply(BurstingIce.Debuff, ctx.CurrentTime);
        ctx.AllEnemies[2].Auras.Apply(BurstingIce.Debuff, ctx.CurrentTime);
        AssertReads(2.0, "active_dot.bursting_ice", hero, ctx);
        var count = BoundExpression.Bind(AplParser.ParseExpression("active_dot.bursting_ice>=2"), hero);
        Assert.Equal(ctx.AllEnemies[0].Auras.FindActive("bursting_ice", ctx.CurrentTime)!.ExpiresAt, count.NextChange(ctx));
    }

    [Fact]
    public void Charges_Fractional_Counts_The_Part_Of_A_Charge_Recharged()
    {
        var (hero, ctx) = Fight("Rime", enemies: 1);
        var coldSnap = hero.GetAbility<ColdSnap>()!;
        // Game data: Cold Snap (ability 1020) has 2 charges.
        AssertReads(2, "cooldown.cold_snap.charges_fractional", hero, ctx);
        coldSnap.TriggerCooldown(ctx);
        AssertReads(1, "cooldown.cold_snap.charges_fractional", hero, ctx);
        // Half a charge back halfway to the next charge.
        var nextCharge = hero.Cooldowns[coldSnap.Name].NextChargeAt;
        var half = BoundExpression.Bind(AplParser.ParseExpression("cooldown.cold_snap.charges_fractional>=1.5"), hero);
        long halfway = (ctx.CurrentTime.Milliseconds + nextCharge.Milliseconds) / 2;
        Assert.InRange(half.NextChange(ctx).Milliseconds, halfway - 1, halfway + 1);
        Assert.Equal("(cooldown.cold_snap.charges_fractional >= 2)",
            AplParser.ParseActionLine("actions=cold_snap,if=charges_fractional>=2")!.Condition!.ToString());
    }

    [Fact]
    public void Ardeos_Damage_Over_Time_Reads_As_A_Debuff()
    {
        var (hero, ctx) = Fight("Ardeos", enemies: 2);
        var ardeos = (Core.Heroes.Ardeos.Ardeos)hero;
        var searing = ardeos.GetAbility<SearingBlaze>()!.Dot;
        AssertReads(0.0, "dot.searing_blaze.ticking", hero, ctx);
        var dot = ardeos.Dots.Apply(ctx, searing, ctx.Player.CurrentTarget);
        AssertReads(1.0, "dot.searing_blaze.ticking", hero, ctx);
        // The enemy's Searing Blaze debuff runs until the effect ends; the effect's own expiry is read.
        AssertReads(dot.RemainingSeconds(ctx.CurrentTime), "debuff.searing_blaze.remains", hero, ctx);
        AssertReads(0.0, "dot.searing_blaze.refreshable", hero, ctx);
        AssertReads(1.0, "active_dot.searing_blaze", hero, ctx);

        // Engulfing Flames runs as copies side by side: each one is a stack.
        var engulfing = ardeos.GetAbility<EngulfingFlames>()!.Dot;
        ardeos.Dots.Apply(ctx, engulfing, ctx.Player.CurrentTarget);
        ardeos.Dots.Apply(ctx, engulfing, ctx.Player.CurrentTarget);
        AssertReads(2.0, "dot.engulfing_flames.stack", hero, ctx);
    }

    [Theory]
    [InlineData("Elarion", "full_recharge_time<=gcd.max*3&charges>=2&charges_fractional>=1.5")]
    [InlineData("Ardeos", "active_dot.engulfing_flames>=2&dot.searing_blaze.remains<=3&dot.fire_ball.ticking")]
    [InlineData("Mara", "dot.hemorrhage.refreshable&active_dot.seething_poison=0")]
    public void The_Editor_Knows_The_Simc_Names(string hero, string text)
    {
        var check = AplVocabulary.For(hero).Check(text);
        Assert.Null(check.Error);
        Assert.DoesNotContain(check.Tokens, t => t.Kind == AplTokenKind.Unknown);
    }

    [Fact]
    public void The_Editor_Flags_An_Active_Dot_The_Hero_Lacks()
    {
        var check = AplVocabulary.For("Ardeos").Check("active_dot.no_such_burn>=1");
        Assert.Contains(check.Tokens, t => t.Kind == AplTokenKind.Unknown);
    }

    private static void AssertReads(double expected, string text, Hero hero, SimulationContext ctx)
    {
        var expression = AplParser.ParseExpression(text);
        Assert.Equal(expected, expression.EvaluateNumeric(hero, ctx), 6);
        Assert.Equal(expected, BoundExpression.Bind(expression, hero).EvaluateNumeric(ctx), 6);
    }

    private static (Hero Hero, SimulationContext Ctx) Fight(string heroName, int enemies)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = heroName, PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        for (int i = 0; i < enemies; i++) ctx.AddEnemy(100 + i, 1, "Mob", maxHealth: 1000);
        ctx.Player.CurrentTarget = ctx.AllEnemies.First();
        return (hero, ctx);
    }
}
