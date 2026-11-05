using FellowshipSim.UI.Services;

namespace FellowshipSim.Tests.Services;

/// <summary>Text files by key in a folder: written, read, listed at any depth, and never outside the folder.</summary>
public sealed class DiskBlobStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"simfell-blobs-{Guid.NewGuid():N}");
    private readonly DiskBlobStore _store;

    public DiskBlobStoreTests() => _store = new DiskBlobStore(_root);

    [Fact]
    public async Task A_Written_Key_Reads_Back_And_Lists_Under_Its_Prefix()
    {
        await _store.WriteAsync("visitors/a1/apls/Rime/my list.simc", "actions+=/frost_bolt");
        await _store.WriteAsync("visitors/a1/results/j1.json", "{}");

        Assert.Equal("actions+=/frost_bolt", await _store.ReadAsync("visitors/a1/apls/Rime/my list.simc"));
        Assert.Null(await _store.ReadAsync("visitors/a1/results/j2.json"));
        Assert.Equal(["visitors/a1/apls/Rime/my list.simc"], await _store.ListAsync("visitors/a1/apls"));
        Assert.Empty(await _store.ListAsync("visitors/b2"));
    }

    [Fact]
    public async Task A_Listed_Build_Whose_Name_A_Save_Refuses_Reads_And_Deletes()
    {
        await _store.WriteAsync("profiles/Rime/sww (old).simc", "hero=rime");
        var profiles = new ProfileStore(_store);
        var listed = Assert.Single(await profiles.ListAsync(Visitor.DataFolderOwner, ["Rime"]));

        Assert.Equal("sww (old)", listed.Name);
        Assert.Equal("hero=rime", await profiles.ReadAsync(Visitor.DataFolderOwner, listed));
        await Assert.ThrowsAsync<ArgumentException>(() => profiles.SaveAsync(Visitor.DataFolderOwner, listed, "hero=rime"));
        await profiles.DeleteAsync(Visitor.DataFolderOwner, listed);
        Assert.Empty(await profiles.ListAsync(Visitor.DataFolderOwner, ["Rime"]));
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("visitors/../../escape.json")]
    [InlineData("visitors/a1/..")]
    [InlineData("/rooted.json")]
    [InlineData("visitors\\a1\\escape.json")]
    [InlineData("C:/escape.json")]
    public async Task A_Key_Outside_The_Folder_Throws(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.WriteAsync(key, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => _store.ReadAsync(key));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
