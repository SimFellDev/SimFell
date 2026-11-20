using FellowshipSim.Core.Dungeons;
using FellowshipSim.UI.Pages;

namespace FellowshipSim.Tests;

public class DungeonEditorTests
{
    private static DungeonPull Pull(string name) => new(name, [new EnemyGroup(1, "Imp", HitPoints: 1000)], 30);

    private static readonly IReadOnlyList<DungeonPull> Pulls =
    [
        Pull("1"),
        Pull("adds") with { ChainWith = "1" },
        Pull("boss") with { Join = new PullJoin("adds") },
    ];

    [Fact]
    public void A_Renamed_Pull_Keeps_The_Options_Naming_It()
    {
        var editor = new DungeonEditor();
        editor.Toggle("adds");
        var renamed = editor.Rename(Pulls, 1, "wave_2");
        Assert.Equal(["1", "wave_2", "boss"], renamed.Select(p => p.Name));
        Assert.Equal("wave_2", renamed[2].Join!.Pull);
        Assert.Equal("1", renamed[1].ChainWith);
        Assert.Contains("wave_2", editor.Open);
    }

    [Fact]
    public void A_New_Pull_Has_The_Lowest_Free_Whole_Number_As_Its_Name()
    {
        var editor = new DungeonEditor();
        var added = editor.Insert(Pulls, 1, Pull("1"));
        Assert.Equal(["1", "2", "adds", "boss"], added.Select(p => p.Name));
        Assert.Contains("2", editor.Open);
    }

    [Fact]
    public void A_Removed_Pull_Takes_The_Options_Naming_It()
    {
        var removed = new DungeonEditor().Remove(Pulls, 1);
        Assert.Equal(["1", "boss"], removed.Select(p => p.Name));
        Assert.Null(removed[1].Join);
    }

    [Fact]
    public void A_Moved_Pull_Keeps_Its_Name()
    {
        var moved = new DungeonEditor().Move(Pulls, 2, 0);
        Assert.Equal(["boss", "1", "adds"], moved.Select(p => p.Name));
        Assert.Equal("adds", moved[0].Join!.Pull);
    }

    [Fact]
    public void A_Renamed_Moved_Or_Duplicated_Pull_Keeps_Its_Groups_First_Seen_And_Dies()
    {
        EnemyGroup[] groups = [new(2, "Imp", HitPoints: 1000, DiesSeconds: 12), new(1, "Ogre", HitPoints: 1000, FirstSeenSeconds: 5.5)];
        IReadOnlyList<DungeonPull> pulls = [Pull("1"), Pull("adds") with { Groups = groups, ChainWith = "1" }];
        var editor = new DungeonEditor();
        Assert.Equal(groups, editor.Rename(pulls, 1, "wave_2")[1].Groups);
        Assert.Equal(groups, editor.Move(pulls, 1, 0)[0].Groups);
        var duplicated = editor.Insert(pulls, 2, pulls[1]);
        Assert.Equal(groups, duplicated[2].Groups);
        Assert.Equal(groups, editor.Remove(duplicated, 0)[0].Groups);
        // The values are in the text of the edited pulls.
        string text = new DungeonProfile("Test", 600, editor.Remove(duplicated, 0)).Format();
        Assert.Contains("pull.adds+=/count=2,name=Imp,hit_points=1000,dies=12", text);
        Assert.Contains("pull.2+=/count=1,name=Ogre,hit_points=1000,first_seen=5.5", text);
    }
}
