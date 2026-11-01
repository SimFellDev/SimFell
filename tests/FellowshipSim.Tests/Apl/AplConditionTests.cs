using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Import;
using Superpower;

namespace FellowshipSim.Tests.Apl;

/// <summary>Decimal numbers, math functions, <c>prev.&lt;ability&gt;</c>, and the oldest and newest application of a debuff.</summary>
public class AplConditionTests
{
    [Theory]
    [InlineData("1.5 > 1.25", 1.0)]
    [InlineData("0.1 + 0.2 = 0.3", 1.0)]
    [InlineData(".5 * 4", 2.0)]
    [InlineData("2.5 >= 2.5", 1.0)]
    [InlineData("2.49 >= 2.5", 0.0)]
    [InlineData("floor(2.9)", 2.0)]
    [InlineData("floor(-2.1)", -3.0)]
    [InlineData("ceil(2.1)", 3.0)]
    [InlineData("round(2.5)", 3.0)]
    [InlineData("round(-2.5)", -3.0)]
    [InlineData("round(2.4)", 2.0)]
    [InlineData("abs(-2.5)", 2.5)]
    [InlineData("min(3, 1, 2)", 1.0)]
    [InlineData("max(1, 4)", 4.0)]
    [InlineData("max(min(1, 2), 0.5)", 1.0)]
    [InlineData("-floor(2.5) * 2", -4.0)]
    [InlineData("!floor(0.5)", 1.0)]
    [InlineData("MAX(1, 2)", 2.0)]
    [InlineData("min(active_enemies, 5) >= 1", 1.0)]
    public void Numbers_And_Functions_Read_The_Same_Bound_And_Interpreted(string text, double expected)
    {
        var (hero, ctx) = Fight();
        var expression = AplParser.ParseExpression(text);
        Assert.Equal(expected, expression.EvaluateNumeric(hero, ctx), 9);
        Assert.Equal(expected, BoundExpression.Bind(expression, hero).EvaluateNumeric(ctx), 9);
    }

    [Theory]
    [InlineData("floor(1, 2)")]
    [InlineData("min(1)")]
    [InlineData("abs()")]
    [InlineData("max(1, 2")]
    public void A_Function_Takes_Its_Number_Of_Arguments(string text)
    {
        Assert.Throws<ParseException>(() => AplParser.ParseExpression(text));
    }

    [Theory]
    [InlineData("floor(buff.ice_blitz.remains) < 3 & max(worb, 2) > 1")]
    [InlineData("!floor(x)")]
    [InlineData("-abs(x - 1)")]
    [InlineData("min(a, b, c) * 2")]
    public void A_Function_Is_Written_Back_As_Read(string text)
    {
        Assert.Equal(text, AplFormatter.Format(AplParser.ParseExpression(text)));
    }

    [Fact]
    public void A_Functions_Arguments_Read_The_Actions_Own_Ability()
    {
        Assert.Equal("max(debuff.bursting_ice.refreshable, 0)",
            AplFormatter.Format(AplParser.ParseActionLine("actions=bursting_ice,if=max(refreshable,0)")!.Condition!));
    }

    [Fact]
    public void A_Function_Is_Allowed_In_Every_Argument_Of_An_Action()
    {
        var action = AplParser.ParseActionLine("actions=frost_bolt,target_if=min:min(target.health,10),if=max(worb,1)>=1")!;
        Assert.Equal(AplTargetSelection.Min, action.TargetIf!.Selection);
        Assert.Equal("min(target.health, 10)", AplFormatter.Format(action.TargetIf.Expression));
        Assert.Equal("max(worb, 1) >= 1", AplFormatter.Format(action.Condition!));
    }

    [Fact]
    public void The_Builder_Has_A_Term_For_A_Function()
    {
        var rows = AplCondition.Rows(AplParser.ParseExpression("max(worb, 2) > 1"));
        var term = Assert.Single(rows).Left![0];
        Assert.Equal("max", term.Function);
        Assert.Equal(2, term.Arguments!.Count);
        Assert.Equal("worb", term.Arguments[0][0].Left![0].Path);
    }

    [Fact]
    public void The_Check_Marks_A_Functions_Name()
    {
        var check = AplVocabulary.For("Rime").Check("floor(worb) >= 1 & prev.frost_bolt & prev.no_such_ability");
        Assert.Null(check.Error);
        Assert.Equal(AplTokenKind.Function, check.Tokens[0].Kind);
        Assert.Equal(AplTokenKind.Cooldown, check.Tokens[^3].Kind);
        Assert.Equal(AplTokenKind.Unknown, check.Tokens[^1].Kind);
    }

    [Theory]
    [InlineData("floor(debuff.bursting_ice.remains) < 2", 2000, -1)]
    [InlineData("ceil(debuff.bursting_ice.remains) <= 2", 2000, 0)]
    [InlineData("round(debuff.bursting_ice.remains) < 2", 1500, -1)]
    [InlineData("min(debuff.bursting_ice.remains, 5) < 2", 2000, -1)]
    [InlineData("abs(debuff.bursting_ice.remains - 2) < 0.5", 2500, -1)]
    public void A_Function_Of_A_Remaining_Time_Changes_At_Its_Millisecond(string text, int msBeforeExpiry, int offset)
    {
        var (hero, ctx) = Fight();
        ctx.Player.CurrentTarget!.Auras.Apply(Core.Heroes.Rime.Abilities.BurstingIce.Debuff, ctx.CurrentTime);
        var expires = ctx.Player.CurrentTarget.Auras.FindActive("bursting_ice", ctx.CurrentTime)!.ExpiresAt;
        var bound = BoundExpression.Bind(AplParser.ParseExpression(text), hero);
        Assert.False(bound.Evaluate(ctx));

        // A step function breaks at each whole number its argument passes: a decision at each break, none past the change.
        var expected = SimTime.FromMilliseconds(expires.Milliseconds - msBeforeExpiry - offset);
        for (int decisions = 0; !bound.Evaluate(ctx); decisions++)
        {
            Assert.True(decisions < 5, $"{decisions} decisions before the change");
            var next = bound.NextChange(ctx);
            Assert.True(next <= expected, $"the next change at {next} is past {expected}");
            ctx.RunUntil(next);
        }
        Assert.Equal(expected, ctx.CurrentTime);
    }

    [Fact]
    public void Prev_Reads_The_Last_Cast_Started()
    {
        var (hero, ctx) = Fight();
        var lists = AplParser.ParseScript("actions=frost_bolt");
        foreach (var text in new[] { "prev.frost_bolt", "prev.cold_snap", "!prev.frost_bolt" })
        {
            var expression = AplParser.ParseExpression(text);
            Assert.Equal(text == "!prev.frost_bolt" ? 1.0 : 0.0, expression.EvaluateNumeric(hero, ctx));
        }

        var program = lists.Bind(hero);
        var prev = BoundExpression.Bind(AplParser.ParseExpression("prev.frost_bolt"), hero);
        Assert.True(program.TryAct(ctx));
        Assert.True(prev.Evaluate(ctx));
        Assert.Equal(1.0, AplParser.ParseExpression("prev.frost_bolt").EvaluateNumeric(hero, ctx));
        Assert.Equal(0.0, AplParser.ParseExpression("prev.cold_snap").EvaluateNumeric(hero, ctx));

        ctx.Reset();
        Assert.False(prev.Evaluate(ctx));
    }

    [Fact]
    public void Oldest_And_Newest_Read_One_Application_Of_Engulfing_Flames()
    {
        var (hero, ctx) = Fight("Ardeos");
        var ardeos = (Ardeos)hero;
        var dot = hero.Abilities.OfType<EngulfingFlames>().Single().Dot;
        var target = ctx.Player.CurrentTarget!;
        ardeos.Dots.Apply(ctx, dot, target);
        ctx.RunUntil(SimTime.FromSeconds(3));
        ardeos.Dots.Apply(ctx, dot, target);

        double first = ardeos.Dots.On(target).Min(d => d.ExpiresAt.Milliseconds) / 1000.0 - 3.0;
        double last = ardeos.Dots.On(target).Max(d => d.ExpiresAt.Milliseconds) / 1000.0 - 3.0;
        foreach (var (text, expected) in new[]
                 {
                     ("debuff.engulfing_flames.oldest.remains", first),
                     ("dot.engulfing_flames.oldest.remains", first),
                     ("debuff.engulfing_flames.newest.remains", last),
                     ("debuff.engulfing_flames.remains", last),
                     ("debuff.engulfing_flames.stack", 2.0),
                     ("debuff.engulfing_flames.oldest.refreshable", 0.0),
                     ("debuff.searing_blaze.oldest.remains", 0.0),
                 })
        {
            var expression = AplParser.ParseExpression(text);
            Assert.Equal(expected, expression.EvaluateNumeric(hero, ctx), 6);
            Assert.Equal(expected, BoundExpression.Bind(expression, hero).EvaluateNumeric(ctx), 6);
        }

        // The oldest application runs out first: the newest is the oldest from then on.
        var oldest = BoundExpression.Bind(AplParser.ParseExpression("debuff.engulfing_flames.oldest.remains > 4"), hero);
        Assert.True(oldest.Evaluate(ctx));
        Assert.Equal(SimTime.FromMilliseconds((long)Math.Round((first - 4.0 + 3.0) * 1000)), oldest.NextChange(ctx));
    }

    [Fact]
    public void Oldest_And_Newest_Are_Debuff_Properties_With_A_Doc()
    {
        Assert.Contains("oldest.remains", PropertyExpression.Scopes[PropertyExpression.DebuffScope]);
        Assert.Contains("newest.refreshable", PropertyExpression.Scopes[PropertyExpression.DotScope]);
        Assert.DoesNotContain("oldest.remains", PropertyExpression.Scopes[PropertyExpression.BuffScope]);
        Assert.NotNull(PropertyExpression.PropertyDoc(PropertyExpression.DebuffScope, "oldest.remains"));
        Assert.Equal((AplTokenKind.Debuff, (string?)null),
            AplVocabulary.For("Ardeos").Classify(new PropertyExpression("debuff.engulfing_flames.newest.remains")));
    }

    private static (Hero Hero, SimulationContext Ctx) Fight(string heroName = "Rime")
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = heroName, PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        ctx.AddEnemy(100, 1, "Mob", maxHealth: 100_000);
        ctx.Player.CurrentTarget = ctx.AllEnemies.First();
        return (hero, ctx);
    }
}
