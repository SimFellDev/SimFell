using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.UI.Services;

/// <summary>A dungeon a visitor can pick: one of their own, or one built into the engine.</summary>
public sealed record DungeonEntry(string Name, bool BuiltIn = false);

/// <summary>
/// A visitor's saved dungeons, as <c>.simc</c> dungeon profiles: their <c>dungeons/&lt;name&gt;.simc</c> in the
/// <see cref="IBlobStore"/>, which for the data folder's owner is the data folder's <c>dungeons</c> folder; and the
/// dungeons built into the engine (<see cref="DefaultDungeons"/>).
/// </summary>
public sealed class DungeonStore(IBlobStore store)
{
    private const string Extension = ".simc";

    /// <summary>The built-in dungeons, then the visitor's own, each by name.</summary>
    public async Task<IReadOnlyList<DungeonEntry>> ListAsync(Visitor visitor, CancellationToken ct = default)
    {
        string prefix = visitor.Key("dungeons");
        var own = (await store.ListAsync(prefix, ct))
            .Where(key => key.EndsWith(Extension, StringComparison.Ordinal) && key.LastIndexOf('/') == prefix.Length)
            .Select(key => new DungeonEntry(key[(prefix.Length + 1)..^Extension.Length]))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
        return DefaultDungeons.Names.Select(name => new DungeonEntry(name, BuiltIn: true)).Concat(own).ToList();
    }

    /// <summary>The profile text of a dungeon, or null when there is none.</summary>
    public async Task<string?> ReadAsync(Visitor visitor, DungeonEntry entry, CancellationToken ct = default) =>
        entry.BuiltIn ? DefaultDungeons.Read(entry.Name)
        : ActionListStore.IsValidName(entry.Name) ? await store.ReadAsync(KeyOf(visitor, entry.Name), ct)
        : null;

    /// <summary>Saves <paramref name="dungeon"/> as the visitor's dungeon of its name; the name follows <see cref="ActionListStore.IsValidName"/>.</summary>
    public Task SaveAsync(Visitor visitor, DungeonProfile dungeon, CancellationToken ct = default)
    {
        if (!ActionListStore.IsValidName(dungeon.Name))
        {
            throw new ArgumentException("A dungeon name has letters, digits, spaces, '_' and '-', up to 64 characters.", nameof(dungeon));
        }
        return store.WriteAsync(KeyOf(visitor, dungeon.Name), dungeon.Format(), ct);
    }

    public Task DeleteAsync(Visitor visitor, DungeonEntry entry, CancellationToken ct = default) =>
        !entry.BuiltIn && ActionListStore.IsValidName(entry.Name) ? store.DeleteAsync(KeyOf(visitor, entry.Name), ct) : Task.CompletedTask;

    private static string KeyOf(Visitor visitor, string name) => visitor.Key($"dungeons/{name}{Extension}");
}
