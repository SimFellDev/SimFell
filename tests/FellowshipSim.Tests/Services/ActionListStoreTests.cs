using FellowshipSim.Logs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Tests.Services;

/// <summary>
/// Saved action lists: a web visitor saves their own lists and reads the shared ones without changing them; the desktop
/// user's lists are the data folder's <c>apls</c> folder.
/// </summary>
public sealed class ActionListStoreTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), $"simfell-apls-{Guid.NewGuid():N}");
    private readonly ActionListStore _lists;
    private static readonly Visitor A = new("a1b2", OwnsDataFolder: false);
    private static readonly Visitor B = new("c3d4", OwnsDataFolder: false);

    public ActionListStoreTests()
    {
        _lists = new ActionListStore(new ReplayLibrary(_data), new DiskBlobStore(_data));
        Directory.CreateDirectory(Path.Combine(_data, "apls", "Rime"));
        File.WriteAllText(Path.Combine(_data, "apls", "Rime", "default.simc"), "actions+=/frost_bolt");
    }

    [Fact]
    public async Task A_Visitor_Lists_Their_Own_Lists_Then_The_Shared_Ones()
    {
        await _lists.SaveAsync(A, "Rime", "default", "actions+=/cold_snap");

        Assert.Equal([new ActionListEntry("Rime", "default"), new ActionListEntry("Rime", "default", Shared: true)],
            await _lists.ListAsync(A, ["Rime"]));
        Assert.Equal("actions+=/cold_snap", await _lists.ReadAsync(A, new ActionListEntry("Rime", "default")));
        Assert.Equal("actions+=/frost_bolt", await _lists.ReadAsync(A, new ActionListEntry("Rime", "default", Shared: true)));
    }

    [Fact]
    public async Task A_Visitors_Save_Leaves_The_Shared_List_And_Other_Visitors_Alone()
    {
        await _lists.SaveAsync(A, "Rime", "default", "actions+=/cold_snap");

        Assert.Equal("actions+=/frost_bolt", File.ReadAllText(Path.Combine(_data, "apls", "Rime", "default.simc")));
        Assert.Equal([new ActionListEntry("Rime", "default", Shared: true)], await _lists.ListAsync(B, ["Rime"]));
        Assert.Null(await _lists.ReadAsync(B, new ActionListEntry("Rime", "default")));
    }

    [Fact]
    public async Task The_Data_Folders_Owner_Saves_And_Lists_The_Apls_Folder()
    {
        await _lists.SaveAsync(Visitor.DataFolderOwner, "Rime", "aoe", "actions+=/ice_comet");

        Assert.True(File.Exists(_lists.PathOf("Rime", "aoe")));
        Assert.Equal([new ActionListEntry("Rime", "aoe"), new ActionListEntry("Rime", "default")],
            await _lists.ListAsync(Visitor.DataFolderOwner, ["Rime"]));
    }

    [Fact]
    public async Task A_Visitor_Deletes_Their_Own_List_And_Not_The_Shared_One()
    {
        await _lists.SaveAsync(A, "Rime", "default", "actions+=/cold_snap");
        await _lists.DeleteAsync(A, "Rime", "default");

        Assert.Equal([new ActionListEntry("Rime", "default", Shared: true)], await _lists.ListAsync(A, ["Rime"]));
        Assert.True(File.Exists(_lists.PathOf("Rime", "default")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_data)) Directory.Delete(_data, recursive: true);
    }
}
