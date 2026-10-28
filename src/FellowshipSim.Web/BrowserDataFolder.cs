using Microsoft.JSInterop;

namespace FellowshipSim.Web;

/// <summary>
/// The browser app's data folder. Its files are in the browser runtime's in-memory file system at <see cref="Root"/>, so
/// every store that reads and writes the folder (fights, replay scripts, reviews, settings, builds, action lists, sims)
/// runs as it does on a computer; storage.js keeps a copy of each file in the browser's IndexedDB. The copies come back
/// at start (<see cref="RestoreAsync"/>), and a changed or removed file is copied every couple of seconds
/// (<see cref="KeepSaving"/>). The browser keeps the newest <see cref="KeptSims"/> sims (<see cref="FellowshipSim.UI.Services.SimResultLibrary.Keep"/>),
/// <see cref="KeptReviews"/> reviews and <see cref="KeptFights"/> downloaded fights.
/// </summary>
public sealed class BrowserDataFolder(IJSRuntime js)
{
    public const string Root = "/simfell";

    public const int KeptSims = 20;
    public const int KeptReviews = 20;

    /// <summary>Downloaded fights kept: each is several MB of events, and is downloaded again when a replay needs it.</summary>
    public const int KeptFights = 10;

    private static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(2);

    // Each file copied, by its path under Root: its length and when it was written, as copied.
    private readonly Dictionary<string, (long Length, DateTime Written)> _copied = new(StringComparer.Ordinal);
    private IJSObjectReference? _storage;

    private async Task<IJSObjectReference> StorageAsync() =>
        _storage ??= await js.InvokeAsync<IJSObjectReference>("import", "./storage.js");

    /// <summary>Writes every file the browser keeps into the data folder.</summary>
    public async Task RestoreAsync()
    {
        Directory.CreateDirectory(Root);
        var storage = await StorageAsync();
        foreach (string key in await storage.InvokeAsync<string[]>("list"))
        {
            if (await storage.InvokeAsync<string?>("read", key) is not { } text || PathOf(key) is not { } path) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, text);
            _copied[key] = Stamp(path);
        }
    }

    /// <summary>Copies the folder's changes to the browser's storage from now on, every <see cref="SaveEvery"/>.</summary>
    public void KeepSaving() => _ = SaveLoopAsync();

    private async Task SaveLoopAsync()
    {
        while (true)
        {
            await Task.Delay(SaveEvery);
            try
            {
                await SaveAsync();
            }
            catch (Exception e) when (e is IOException or JSException)
            {
                Console.Error.WriteLine($"SimFell could not save its files in the browser: {e.Message}");
            }
        }
    }

    /// <summary>Drops what the browser does not keep, then copies each new or changed file and forgets each removed one.</summary>
    public async Task SaveAsync()
    {
        Prune();
        var storage = await StorageAsync();
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            string key = Path.GetRelativePath(Root, path).Replace('\\', '/');
            present.Add(key);
            var stamp = Stamp(path);
            if (_copied.TryGetValue(key, out var copied) && copied == stamp) continue;
            await storage.InvokeVoidAsync("write", key, await File.ReadAllTextAsync(path));
            _copied[key] = stamp;
        }
        foreach (string key in _copied.Keys.Where(k => !present.Contains(k)).ToList())
        {
            await storage.InvokeVoidAsync("remove", key);
            _copied.Remove(key);
        }
    }

    // The newest fights and reviews (with their replay scripts) stay; the rest go.
    private static void Prune()
    {
        var library = new DirectoryInfo(Path.Combine(Root, "fights"));
        if (library.Exists)
        {
            foreach (var old in library.GetFiles("*.raw.json").OrderByDescending(f => f.LastWriteTimeUtc).Skip(KeptFights)) old.Delete();
        }
        var reviews = new DirectoryInfo(Path.Combine(Root, "reviews"));
        if (!reviews.Exists) return;
        foreach (var old in reviews.GetFiles("*.summary.json").OrderByDescending(f => f.LastWriteTimeUtc).Skip(KeptReviews))
        {
            string id = old.Name[..^".summary.json".Length];
            foreach (var file in reviews.GetFiles($"{id}.*")) file.Delete();
            var script = new FileInfo(Path.Combine(Root, "scripts", $"{id}.replay.json"));
            if (script.Exists) script.Delete();
        }
    }

    private static (long Length, DateTime Written) Stamp(string path)
    {
        var file = new FileInfo(path);
        return (file.Length, file.LastWriteTimeUtc);
    }

    // A storage key as a path under Root; null for one that would leave it.
    private static string? PathOf(string key)
    {
        string path = Path.GetFullPath(Path.Combine(Root, key));
        return path.StartsWith(Root + "/", StringComparison.Ordinal) ? path : null;
    }
}
