using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using Superpower;

namespace FellowshipSim.Tests;

public class AplParserTests
{
    [Theory]
    [InlineData("a&b|c", "((a & b) | c)")]
    [InlineData("a|b&c", "(a | (b & c))")]
    [InlineData("!a&b", "(!(a) & b)")]
    [InlineData("!(a|b)", "!((a | b))")]
    [InlineData("x<=2&y>-1.5", "((x <= 2) & (y > -(1.5)))")]
    [InlineData("-x<-(y)", "(-(x) < -(y))")]
    [InlineData("!-x", "!(-(x))")]
    [InlineData("--1", "-(-(1))")]
    [InlineData("x==1|x=2|x!=3", "(((x = 1) | (x = 2)) | (x != 3))")]
    [InlineData("buff.glacial_assault.stack>=3", "(buff.glacial_assault.stack >= 3)")]
    public void Expression_Precedence_And_Operators(string text, string expected) =>
        Assert.Equal(expected, AplParser.ParseExpression(text).ToString());

    [Fact]
    public void Expression_Builds_Typed_Operators()
    {
        var expr = Assert.IsType<BinaryOpExpression>(AplParser.ParseExpression("!a&b<1"));
        Assert.Equal(BinaryOperator.And, expr.Operator);
        Assert.Equal(UnaryOperator.Not, Assert.IsType<UnaryOpExpression>(expr.Left).Operator);
        Assert.Equal(BinaryOperator.Less, Assert.IsType<BinaryOpExpression>(expr.Right).Operator);
        Assert.Equal(1.0, Assert.IsType<ConstantExpression>(((BinaryOpExpression)expr.Right).Right).Value);
    }

    [Theory]
    [InlineData("a+b*c", "(a + (b * c))")]
    [InlineData("a-b-c", "((a - b) - c)")]
    [InlineData("a%b%%c", "((a % b) %% c)")]
    [InlineData("-a*b", "(-(a) * b)")]
    [InlineData("a--b", "(a - -(b))")]
    [InlineData("a+1>b*2&c", "(((a + 1) > (b * 2)) & c)")]
    [InlineData("(a+b)*c", "((a + b) * c)")]
    public void Expression_Arithmetic_Precedence(string text, string expected) =>
        Assert.Equal(expected, AplParser.ParseExpression(text).ToString());

    [Theory]
    [InlineData("1+2*3", 7.0)]
    [InlineData("10-4-3", 3.0)]
    [InlineData("7%2", 3.5)]
    [InlineData("7%%3", 1.0)]
    [InlineData("-2*3", -6.0)]
    [InlineData("5%0", 0.0)]
    [InlineData("5%%0", 0.0)]
    [InlineData("2+2=4", 1.0)]
    public void Expression_Arithmetic_Evaluates(string text, double expected) =>
        // Constant-only expressions never read the hero or the simulation.
        Assert.Equal(expected, AplParser.ParseExpression(text).EvaluateNumeric(null!, null!));

    [Theory]
    [InlineData("1-1", false)]
    [InlineData("3%%3", false)]
    [InlineData("1+1", true)]
    public void Expression_Arithmetic_As_Condition(string text, bool expected) =>
        Assert.Equal(expected, AplParser.ParseExpression(text).Evaluate(null!, null!));

    [Fact]
    public void Expression_Builds_Negate()
    {
        var expr = Assert.IsType<UnaryOpExpression>(AplParser.ParseExpression("-2.5"));
        Assert.Equal(UnaryOperator.Negate, expr.Operator);
        Assert.Equal(2.5, Assert.IsType<ConstantExpression>(expr.Operand).Value);
    }

    [Theory]
    [InlineData("x-")]
    [InlineData("a*")]
    [InlineData("a+*b")]
    [InlineData("a/b")]
    [InlineData("a&b c")]
    [InlineData("(a&b")]
    [InlineData("a)")]
    [InlineData("a&")]
    [InlineData("")]
    [InlineData("a@b")]
    public void Expression_Malformed_Throws(string text) =>
        Assert.Throws<ParseException>(() => AplParser.ParseExpression(text));

    [Theory]
    [InlineData("actions+=/frost_bolt", "frost_bolt")]
    [InlineData("actions=frost_bolt", "frost_bolt")]
    [InlineData("actions.aoe+=/frost_bolt", "frost_bolt")]
    [InlineData("frost_bolt,if=buff.x.down", "frost_bolt,if=buff.x.down")]
    [InlineData("actions+=/cold_snap,if=x=1", "cold_snap,if=(x = 1)")]
    [InlineData("actions+=/cold_snap,line_cd=5,if=a", "cold_snap,if=a")]
    [InlineData("actions+=/run_action_list,name=aoe,if=a", "run_action_list,name=aoe,if=a")]
    public void ActionLine_Forms(string line, string expected) =>
        Assert.Equal(expected, AplParser.ParseActionLine(line)!.ToString());

    [Theory]
    [InlineData("snapshot_stats")]
    [InlineData("actions.precombat+=/snapshot_stats")]
    public void ActionLine_Snapshot_Stats_Is_Skipped(string line) =>
        Assert.Null(AplParser.ParseActionLine(line));

    [Fact]
    public void Script_Skips_Comments_And_Blank_Lines()
    {
        var actions = AplParser.ParseScript("""
            # opener
            actions=ice_comet

            actions+=/frost_bolt
            """);
        Assert.Equal(["ice_comet", "frost_bolt"], actions.Actions.Select(a => a.AbilityName));
    }

    [Fact]
    public void Script_Error_Names_The_Line()
    {
        var e = Assert.Throws<ParseException>(() => AplParser.ParseScript("actions=a\n# c\nactions+=/b,if=(x"));
        Assert.StartsWith("APL line 3:", e.Message);
    }
}
