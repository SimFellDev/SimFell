namespace FellowshipSim.UI.Services;

/// <summary>
/// Text files by key, such as <c>visitors/&lt;id&gt;/results/&lt;job id&gt;.json</c>. A key is segments joined by '/';
/// each segment starts with a letter or digit and has only letters, digits, spaces, '.', '_' and '-'. A key breaking that
/// rule throws <see cref="ArgumentException"/>. <see cref="DiskBlobStore"/> keeps them in a folder.
/// </summary>
public interface IBlobStore
{
    /// <summary>The text of <paramref name="key"/>, or null when there is none.</summary>
    Task<string?> ReadAsync(string key, CancellationToken ct = default);

    Task WriteAsync(string key, string text, CancellationToken ct = default);

    /// <summary>Removes <paramref name="key"/>; nothing happens when there is none.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Every key under the folder <paramref name="prefix"/>, at any depth.</summary>
    Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken ct = default);
}
