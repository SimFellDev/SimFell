using FellowshipSim.Core.Dungeons;
using FellowshipSim.UI.Pages;

namespace FellowshipSim.Tests;

public class DungeonEditorTests
{
    private static DungeonPull Pull(string name) => new(name, [new EnemyGroup(1, "Imp", HitPoints: 1000)]);

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
}
