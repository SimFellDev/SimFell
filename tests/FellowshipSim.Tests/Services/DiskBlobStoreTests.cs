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
