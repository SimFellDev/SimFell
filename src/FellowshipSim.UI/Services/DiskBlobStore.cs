using System.Text.RegularExpressions;

namespace FellowshipSim.UI.Services;

/// <summary>An <see cref="IBlobStore"/> in the folder <paramref name="root"/>: each key is a file path under it.</summary>
public sealed partial class DiskBlobStore(string root) : IBlobStore
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 ._-]{0,127}$")]
    private static partial Regex SegmentPattern();

    /// <summary>True when <paramref name="segment"/> can be one segment of a key.</summary>
    public static bool IsSegment(string segment) => SegmentPattern().IsMatch(segment) && !segment.Contains("..");

    public async Task<string?> ReadAsync(string key, CancellationToken ct = default)
    {
        string path = PathOf(key);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
    }

    public async Task WriteAsync(string key, string text, CancellationToken ct = default)
    {
        string path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, ct);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        File.Delete(PathOf(key));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken ct = default)
    {
        string folder = PathOf(prefix);
        IReadOnlyList<string> keys = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(file => $"{prefix}/{Path.GetRelativePath(folder, file).Replace('\\', '/')}")
                .ToList()
            : [];
        return Task.FromResult(keys);
    }

    private string PathOf(string key)
    {
        string[] segments = key.Split('/');
        if (!segments.All(IsSegment)) throw new ArgumentException($"'{key}' is not a blob key.", nameof(key));
        return Path.Combine([root, .. segments]);
    }
}
