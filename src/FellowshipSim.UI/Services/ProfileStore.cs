namespace FellowshipSim.UI.Services;

/// <summary>A saved character build: its hero and its name.</summary>
public sealed record ProfileEntry(string Hero, string Name);

/// <summary>
/// A visitor's saved character builds, as <c>.simc</c> profiles: their <c>profiles/&lt;hero&gt;/&lt;name&gt;.simc</c> in the
/// <see cref="IBlobStore"/>, which for the data folder's owner is the data folder's <c>profiles</c> folder.
/// </summary>
public sealed class ProfileStore(IBlobStore store)
{
    private const string Extension = ".simc";

    /// <summary>The visitor's saved builds of <paramref name="heroes"/>, by hero and name.</summary>
    public async Task<IReadOnlyList<ProfileEntry>> ListAsync(Visitor visitor, IEnumerable<string> heroes, CancellationToken ct = default)
    {
        var entries = new List<ProfileEntry>();
        foreach (string hero in heroes)
        {
            string prefix = visitor.Key($"profiles/{hero}");
            entries.AddRange((await store.ListAsync(prefix, ct))
                .Where(key => key.EndsWith(Extension, StringComparison.Ordinal) && key.LastIndexOf('/') == prefix.Length)
                .Select(key => new ProfileEntry(hero, key[(prefix.Length + 1)..^Extension.Length])));
        }
        return entries.OrderBy(e => e.Hero).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The profile text of a saved build, or null when there is none.</summary>
    public Task<string?> ReadAsync(Visitor visitor, ProfileEntry entry, CancellationToken ct = default) =>
        ActionListStore.IsStoredName(entry.Name) ? store.ReadAsync(KeyOf(visitor, entry), ct) : Task.FromResult<string?>(null);

    /// <summary>Saves <paramref name="profile"/> as the visitor's build <paramref name="entry"/>; the name follows <see cref="ActionListStore.IsValidName"/>.</summary>
    public Task SaveAsync(Visitor visitor, ProfileEntry entry, string profile, CancellationToken ct = default)
    {
        if (!ActionListStore.IsValidName(entry.Name))
        {
            throw new ArgumentException("A build name has letters, digits, spaces, '_' and '-', up to 64 characters.", nameof(entry));
        }
        return store.WriteAsync(KeyOf(visitor, entry), profile, ct);
    }

    public Task DeleteAsync(Visitor visitor, ProfileEntry entry, CancellationToken ct = default) =>
        ActionListStore.IsStoredName(entry.Name) ? store.DeleteAsync(KeyOf(visitor, entry), ct) : Task.CompletedTask;

    private static string KeyOf(Visitor visitor, ProfileEntry entry) => visitor.Key($"profiles/{entry.Hero}/{entry.Name}{Extension}");
}
