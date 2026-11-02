using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Editing;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>The completion list of the action-list text at its caret (<see cref="ListEdits"/>).</summary>
public class ListEditsTests
{
    private readonly AplVocabulary _v = EditorVocabulary.Create();

    private const string Lists = "actions+=/frost_bolt\nactions.aoe+=/ice_comet\nactions+=/variable,name=orbs,value=1\n";

    private IReadOnlyList<string>? Items(string before, bool always = false)
    {
        string text = Lists + before;
        return ListEdits.Open(_v, text, text.Length, always)?.Items.Select(i => i.Text).ToList();
    }

    [Fact]
    public void After_The_Prefix_The_List_Has_The_Actions()
    {
        Assert.Equal(["frost_bolt"], Items("actions+=/fr"));
        Assert.Equal([.. _v.Abilities.Select(a => a.Slug), .. _v.ListActions, AplVariable.ActionName], Items("actions.aoe+=/", always: true));
        Assert.Null(Items("actions+=/"));
    }

    [Fact]
    public void A_Prefix_Completes_To_Actions_And_The_Lists()
    {
        Assert.Equal(["actions", "run_action_list", "call_action_list"], Items("act"));
        Assert.Equal(["aoe"], Items("actions.a"));
    }

    [Fact]
    public void After_A_Comma_The_List_Has_The_Actions_Keys()
    {
        Assert.Equal([AplActionLine.ConditionKey, AplActionLine.TargetIfKey], Items("actions+=/frost_bolt,", always: true));
        Assert.Equal([AplActionLine.ListNameKey], Items("actions+=/run_action_list,na"));
        Assert.Equal([AplVariable.ValueKey], Items("actions+=/variable,name=orbs,va"));
    }

    [Fact]
    public void A_Value_Has_The_Lists_Variables_Ops_Selections_Or_Paths()
    {
        Assert.Equal(["aoe"], Items("actions+=/run_action_list,name=a"));
        Assert.Equal(["orbs"], Items("actions+=/variable,name=o"));
        Assert.Equal(["reset"], Items("actions+=/variable,name=orbs,op=res"));
        Assert.Contains("min", Items("actions+=/ice_comet,target_if=mi")!);
        Assert.Equal(["chill", "glacial_assault"], Items("actions+=/frost_bolt,if=buff.", always: true));
        Assert.Equal(["chill"], Items("actions+=/frost_bolt,if=max(1,buff.ch"));
        Assert.Equal(["chill"], Items("actions+=/frost_bolt,target_if=min:debuff.ch"));
    }

    [Fact]
    public void A_Comment_Or_A_Number_Has_No_List()
    {
        Assert.Null(Items("# buff.ch", always: true));
        Assert.Null(Items("actions+=/frost_bolt,if=3", always: true));
    }
}
