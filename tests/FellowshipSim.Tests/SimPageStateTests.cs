using FellowshipSim.Core.APL;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;
using FellowshipSim.UI.Build;
using FellowshipSim.UI.Pages;
using FellowshipSim.UI.Shared;

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

    // A page with every option off its default, the build unsaved and the action list its own.
    private static SimPageState Edited(bool dungeon)
    {
        var build = BuildEditor.NewBuild("Ardeos");
        build.Name = "Unsaved";
        var state = new SimPageState
        {
            Iterations = 321,
            Threads = 1,
            FirstSeed = 9,
            DurationSeconds = 123,
            TrainingDummies = 4,
            VaryLength = true,
            LengthVariancePercent = 12.5,
            VaryHeroShare = false,
            Boss = false,
            StartWithSpirit = true,
            StartingSpirit = 55,
            ReactionDelays = false,
            HealthFollowsTime = false,
        };
        state.Editor = new BuildEditor(build);
        state.ActionListText = "actions=custom_list_line" + Environment.NewLine;
        if (dungeon)
        {
            state.Mode = SimMode.Dungeon;
            state.Dungeon = DefaultDungeons.Default with { Difficulty = 12, Name = "Edited" };
        }
        return state;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_Again_Loads_An_Unsaved_Sims_Whole_Profile_In_The_Import_Dialog(bool dungeon)
    {
        var edited = Edited(dungeon);
        string profile = edited.SimProfileText(edited.Build, edited.ActionListText!);
        var state = new SimPageState { Tab = SimTab.Advanced, Extra = SimExtra.Compare };

        state.RunAgain(profile);

        Assert.True(state.ImportOpen);
        Assert.Equal(SimExtra.None, state.Extra);
        Assert.Equal(profile, state.ProfileText);
        Assert.Equal("Unsaved", state.Build.Name);
        Assert.Equal("", state.ActionListValue);
        Assert.Equal(dungeon ? SimMode.Dungeon : SimMode.TrainingDummies, state.Mode);
        Assert.Equal(profile, state.SimProfileText(state.Build, state.ActionListText!));
    }

    [Fact]
    public void Character_Profile_Leaves_The_Sim_Settings()
    {
        var state = Edited(dungeon: false);
        string before = state.SimProfileText(state.Build, state.ActionListText!);
        state.ProfileText = new SimcProfile(state.Build, state.ActionListText!).Format();

        state.ImportProfile();

        Assert.Equal(MessageKind.Plain, state.ImportStatus.Kind);
        Assert.Equal(before, state.SimProfileText(state.Build, state.ActionListText!));
    }
}