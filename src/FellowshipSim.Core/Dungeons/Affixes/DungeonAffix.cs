using System.Text;

namespace FellowshipSim.Core.Dungeons.Affixes;

/// <summary>
/// A dungeon affix (the game's dungeon modifier) the engine models. A dungeon's text names its run's affixes by
/// <see cref="Key"/> (<c>affixes=anomalous_orbs,storm_shield</c>), and a dungeon fight runs each of them
/// (<see cref="Begin"/>). <see cref="Catalog"/> has every class deriving from this one with a public constructor without parameters.
/// </summary>
public abstract class DungeonAffix
{
    /// <summary>The affix's game ID: the <c>affixes</c> of a log's <c>dungeonstart</c> event.</summary>
    public abstract int Id { get; }

    public abstract string Name { get; }

    /// <summary>The affix's name in a dungeon's text: <see cref="Name"/> in lower case, its words joined by '_'.</summary>
    public string Key => KeyOf(Name);

    /// <summary>The affix's effects on one dungeon fight, from its start.</summary>
    internal abstract void Begin(AffixRun run);

    /// <summary>
    /// The affix's lasting change to <paramref name="hero"/>, with no aura in the log: a replay applies it to the hero it
    /// builds. Nothing for an affix with none.
    /// </summary>
    public virtual void ApplyToHero(Hero hero) { }

    /// <summary>Every affix the engine models, in ID order (<see cref="Catalog.DungeonAffixes"/>).</summary>
    public static IReadOnlyList<DungeonAffix> All => Catalog.DungeonAffixes;

    /// <summary>The affix of key <paramref name="key"/>; null when the engine models none.</summary>
    public static DungeonAffix? ByKey(string key) => All.FirstOrDefault(a => a.Key == key);

    /// <summary>The affix of game ID <paramref name="id"/>; null when the engine models none.</summary>
    public static DungeonAffix? ById(int id) => All.FirstOrDefault(a => a.Id == id);

    private static string KeyOf(string name)
    {
        var key = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c)) key.Append(char.ToLowerInvariant(c));
            // An apostrophe stays inside its word: Storm's is storms.
            else if (c != '\'' && key.Length > 0 && key[^1] != '_') key.Append('_');
        }
        return key.ToString().TrimEnd('_');
    }
}
