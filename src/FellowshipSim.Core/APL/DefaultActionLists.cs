namespace FellowshipSim.Core.APL;

/// <summary>
/// The action lists that come with each hero: the files in <c>Heroes/&lt;hero&gt;/ActionLists</c>, built into the engine.
/// <see cref="DefaultName"/> is the list a new sim starts with.
/// </summary>
public static class DefaultActionLists
{
    /// <summary>The list a new sim of a hero starts with.</summary>
    public const string DefaultName = "Default";

    private const string Prefix = "FellowshipSim.Core.Heroes.";
    private const string Folder = ".ActionLists.";
    private const string Extension = ".simc";

    // (hero, list name) -> resource name, from names such as FellowshipSim.Core.Heroes.Rime.ActionLists.Talons.simc.
    private static readonly Dictionary<(string Hero, string Name), string> Resources = typeof(DefaultActionLists).Assembly
        .GetManifestResourceNames()
        .Where(r => r.StartsWith(Prefix, StringComparison.Ordinal) && r.EndsWith(Extension, StringComparison.Ordinal) && r.Contains(Folder))
        .ToDictionary(r =>
        {
            string rest = r[Prefix.Length..^Extension.Length];
            int folder = rest.IndexOf(Folder, StringComparison.Ordinal);
            return (rest[..folder], rest[(folder + Folder.Length)..]);
        }, r => r, new KeyComparer());

    /// <summary>The names of <paramref name="hero"/>'s lists, <see cref="DefaultName"/> first.</summary>
    public static IReadOnlyList<string> Names(string hero) => Resources.Keys
        .Where(k => k.Hero.Equals(hero, StringComparison.OrdinalIgnoreCase))
        .Select(k => k.Name)
        .OrderBy(n => n == DefaultName ? 0 : 1).ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>The text of <paramref name="hero"/>'s list <paramref name="name"/>, or null when it has none of that name.</summary>
    public static string? Read(string hero, string name)
    {
        if (!Resources.TryGetValue((hero, name), out var resource)) return null;
        using var stream = typeof(DefaultActionLists).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class KeyComparer : IEqualityComparer<(string Hero, string Name)>
    {
        public bool Equals((string Hero, string Name) a, (string Hero, string Name) b) =>
            a.Hero.Equals(b.Hero, StringComparison.OrdinalIgnoreCase) && a.Name.Equals(b.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Hero, string Name) key) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Hero), StringComparer.OrdinalIgnoreCase.GetHashCode(key.Name));
    }
}
