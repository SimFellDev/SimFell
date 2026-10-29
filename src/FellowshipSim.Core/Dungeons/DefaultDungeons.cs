namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// The dungeons that come with the engine: the files in <c>Dungeons</c>, built into the engine. <see cref="DefaultName"/>
/// is the dungeon a new sim starts with.
/// </summary>
public static class DefaultDungeons
{
    /// <summary>The dungeon a new sim starts with.</summary>
    public const string DefaultName = "Default";

    private const string Prefix = "FellowshipSim.Core.Dungeons.";
    private const string Extension = ".simc";

    // Dungeon name -> resource name, from names such as FellowshipSim.Core.Dungeons.Default.simc.
    private static readonly Dictionary<string, string> Resources = typeof(DefaultDungeons).Assembly
        .GetManifestResourceNames()
        .Where(r => r.StartsWith(Prefix, StringComparison.Ordinal) && r.EndsWith(Extension, StringComparison.Ordinal))
        .ToDictionary(r => r[Prefix.Length..^Extension.Length], r => r, StringComparer.OrdinalIgnoreCase);

    /// <summary>The names of the built-in dungeons, <see cref="DefaultName"/> first.</summary>
    public static IReadOnlyList<string> Names { get; } = Resources.Keys
        .OrderBy(n => n == DefaultName ? 0 : 1).ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>The text of the built-in dungeon <paramref name="name"/>, or null when there is none of that name.</summary>
    public static string? Read(string name)
    {
        if (!Resources.TryGetValue(name, out var resource)) return null;
        using var stream = typeof(DefaultDungeons).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The built-in dungeon <see cref="DefaultName"/>.</summary>
    public static DungeonProfile Default => DungeonProfile.Parse(Read(DefaultName)!);
}
