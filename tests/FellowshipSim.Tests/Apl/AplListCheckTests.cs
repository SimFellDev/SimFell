using FellowshipSim.Core.APL;

namespace FellowshipSim.Tests.Apl;

/// <summary>The check of a whole action list's text (<see cref="AplListCheck"/>).</summary>
public class AplListCheckTests
{
    private static readonly AplVocabulary Rime = AplVocabulary.For("Rime");

    private static (string Text, AplTokenKind Kind, string? Problem)[] Tokens(string text, AplExpressionCheck check) =>
        check.Tokens.Select(t => (text.Substring(t.Start, t.Length), t.Kind, t.Problem)).ToArray();

    [Fact]
    public void Each_Part_Of_A_Line_Has_Its_Kind()
    {
        const string text = "# opener\nactions+=/ice_comet,target_if=min:debuff.bursting_ice.remains,if=worb>=3\r\nactions.aoe+=/frost_bolt\nactions+=/run_action_list,name=aoe\n";
        var check = Rime.CheckList(text);

        Assert.Null(check.Error);
        Assert.Equal(
            [
                ("# opener", AplTokenKind.Comment, null),
                ("actions", AplTokenKind.List, null),
                ("ice_comet", AplTokenKind.Cooldown, null),
                ("target_if", AplTokenKind.Keyword, null),
                ("min", AplTokenKind.Keyword, null),
                ("debuff.bursting_ice.remains", AplTokenKind.Debuff, null),
                ("if", AplTokenKind.Keyword, null),
                ("worb", AplTokenKind.Resource, null),
                (">=", AplTokenKind.Operator, null),
                ("3", AplTokenKind.Number, null),
                ("actions.aoe", AplTokenKind.List, null),
                ("frost_bolt", AplTokenKind.Cooldown, null),
                ("actions", AplTokenKind.List, null),
                ("run_action_list", AplTokenKind.Keyword, null),
                ("name", AplTokenKind.Keyword, null),
                ("aoe", AplTokenKind.List, null),
            ],
            Tokens(text, check));
    }

    [Fact]
    public void A_Name_The_Hero_Or_The_Text_Does_Not_Have_Is_A_Problem_On_Its_Line()
    {
        const string text = "actions+=/no_such_ability\nactions+=/call_action_list,name=st,if=prev_gcd.nothing";
        var tokens = Tokens(text, Rime.CheckList(text));

        Assert.Contains(("no_such_ability", AplTokenKind.Unknown, "Line 1: Rime has no ability named `no_such_ability`."), tokens);
        Assert.Contains(("st", AplTokenKind.Unknown, "Line 2: No action list named `st`."), tokens);
        Assert.Contains(("prev_gcd.nothing", AplTokenKind.Unknown, "Line 2: Rime has no ability named `nothing`."), tokens);
    }

    [Fact]
    public void A_Line_Written_Twice_Has_Its_Own_Place_And_Line_Number_Each_Time()
    {
        const string line = "actions+=/frost_bolt,if=buff.nothing.up";
        string text = $"{line}\n# between\n{line}";
        var unknown = Rime.CheckList(text).Tokens.Where(t => t.Kind == AplTokenKind.Unknown).ToList();

        Assert.Equal([line.IndexOf("buff."), line.Length + "\n# between\n".Length + line.IndexOf("buff.")], unknown.Select(t => t.Start));
        Assert.Equal(["Line 1: Rime has no aura named `nothing`.", "Line 3: Rime has no aura named `nothing`."], unknown.Select(t => t.Problem));
    }

    [Fact]
    public void The_First_Line_Outside_The_Grammar_Is_The_Error()
    {
        const string text = "actions+=/frost_bolt\n  actions+=/ice_comet,if=worb>=\nactions+=/frost_bolt,if=(";
        var check = Rime.CheckList(text);

        Assert.StartsWith("Line 2: ", check.Error);
        Assert.Equal(text.IndexOf('\n') + 1 + "  actions+=/ice_comet,if=worb>=".Length, check.ErrorStart);
    }
}
