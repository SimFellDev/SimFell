using FellowshipSim.Engine;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Tests.Services;

/// <summary>Finished sims in the blob store: read back by ID, listed newest first, and each visitor's apart from the others'.</summary>
public sealed class SimResultLibraryTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), $"simfell-results-{Guid.NewGuid():N}");
    private readonly DiskBlobStore _store;
    private readonly SimResultLibrary _results;
    private static readonly Visitor A = new("a1b2", OwnsDataFolder: false);
    private static readonly Visitor B = new("c3d4", OwnsDataFolder: false);

    public SimResultLibraryTests()
    {
        _store = new DiskBlobStore(_data);
        _results = new SimResultLibrary(_store);
    }

    private static SavedSim Sim(string id, DateTime createdAt) => new(id, $"sim {id}", "Rime", "sww", "rime=\"sww\"", 1,
        new SimResult(5000, 4000, 6000, 300, new Dictionary<string, double> { ["Frost Bolt"] = 600 }, 100, 300), createdAt);

    [Fact]
    public async Task A_Saved_Sim_Reads_Back_By_Its_Id()
    {
        var sim = Sim("a1", new DateTime(2026, 10, 23, 1, 0, 0, DateTimeKind.Utc));
        await _results.SaveAsync(A, sim);

        var read = await _results.GetAsync(A, "a1");
        Assert.NotNull(read);
        Assert.Equal(sim.Title, read.Title);
        Assert.Equal(5000, read.Result.MeanDps);
        Assert.Equal(600, read.Result.AbilityDps["Frost Bolt"]);
        Assert.Null(await _results.GetAsync(A, "missing"));
        Assert.Null(await _results.GetAsync(A, "..\\..\\a1"));
    }

    [Fact]
    public async Task Saved_Sims_List_Newest_First_And_Skip_An_Unreadable_File()
    {
        await _results.SaveAsync(A, Sim("old", new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc)));
        await _results.SaveAsync(A, Sim("new", new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc)));
        await _store.WriteAsync(A.Key("results/broken.json"), "{ not json");

        Assert.Equal(["new", "old"], (await _results.ListAsync(A)).Select(s => s.Id));
    }

    [Fact]
    public async Task A_Library_That_Keeps_Some_Drops_The_Oldest_As_New_Ones_Come()
    {
        var kept = new SimResultLibrary(_store) { Keep = 2 };
        var day = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        foreach (int i in Enumerable.Range(0, 4)) await kept.SaveAsync(A, Sim($"s{i}", day.AddDays(i)));

        Assert.Equal(["s3", "s2"], (await kept.ListAsync(A)).Select(s => s.Id));
        Assert.Null(await kept.GetAsync(A, "s0"));
    }

    [Fact]
    public async Task A_Visitor_Reads_Only_Their_Own_Sims()
    {
        await _results.SaveAsync(A, Sim("a-sim", DateTime.UtcNow));

        Assert.Empty(await _results.ListAsync(B));
        Assert.Null(await _results.GetAsync(B, "a-sim"));
        Assert.True(File.Exists(Path.Combine(_data, "visitors", A.Id, "results", "a-sim.json")));
    }

    [Fact]
    public async Task The_Data_Folders_Owner_Has_Sims_In_The_Results_Folder()
    {
        await _results.SaveAsync(Visitor.DataFolderOwner, Sim("local", DateTime.UtcNow));

        Assert.True(File.Exists(Path.Combine(_data, "results", "local.json")));
        Assert.Equal(["local"], (await _results.ListAsync(Visitor.DataFolderOwner)).Select(s => s.Id));
    }

    [Fact]
    public async Task No_Results_Folder_Lists_Nothing() => Assert.Empty(await _results.ListAsync(A));

    public void Dispose()
    {
        if (Directory.Exists(_data)) Directory.Delete(_data, recursive: true);
    }
}
