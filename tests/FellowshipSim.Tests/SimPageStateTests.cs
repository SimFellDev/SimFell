using FellowshipSim.Core.APL;
using FellowshipSim.UI.Build;
using FellowshipSim.UI.Pages;

namespace FellowshipSim.Tests;

/// <summary>A build of another hero in the Sim page puts in that hero's action list.</summary>
public class SimPageStateTests
{
    [Fact]
    public void Another_Heros_Build_Takes_Its_Last_Picked_Or_Default_List()
    {
        var state = new SimPageState
        {
            ActionListValue = "own/Rime/Mine",
            ActionListName = "Mine",
            ActionListText = "actions=frost_bolt",
        };

        state.Editor = new BuildEditor(BuildEditor.NewBuild("Ardeos"));
        Assert.Equal($"builtin/Ardeos/{DefaultActionLists.DefaultName}", state.ActionListValue);
        Assert.Equal(DefaultActionLists.DefaultName, state.ActionListName);
        Assert.Null(state.ActionListText);

        // Back to Rime: the list last picked for Rime.
        state.Editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        Assert.Equal("own/Rime/Mine", state.ActionListValue);
        Assert.Equal("actions=frost_bolt", state.ActionListText);

        // Another build of the same hero keeps the list.
        state.Editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        Assert.Equal("own/Rime/Mine", state.ActionListValue);
    }
}
