using System.Text.RegularExpressions;

namespace FellowshipSim.Core.APL;

/// <summary>
/// The names an action list used before the dungeon fight, as the names that replaced them: <c>raid_event.&lt;x&gt;</c> is
/// <c>dungeon_event.&lt;x&gt;</c>, and <c>fight_remains</c> is <c>dungeon_event.fight.remains</c>. A list written with the
/// old names runs and opens with the new ones.
/// </summary>
public static partial class AplPaths
{
    private const string OldEventPrefix = "raid_event.";
    private const string EventPrefix = "dungeon_event.";
    private const string OldFightRemains = "fight_remains";
    private const string FightRemains = "dungeon_event.fight.remains";

    /// <summary>The path <paramref name="path"/> (lower case) names now.</summary>
    public static string Migrate(string path) =>
        path.StartsWith(OldEventPrefix, StringComparison.Ordinal) ? EventPrefix + path[OldEventPrefix.Length..]
        : path == OldFightRemains ? FightRemains
        : path;

    /// <summary>The action list <paramref name="text"/> with every old name replaced by the name it has now.</summary>
    public static string MigrateText(string text) => OldName().Replace(text, m => m.Value.StartsWith('r') ? EventPrefix : FightRemains);

    // An old name as a whole word: not part of a longer name ("my_raid_event." or "fight_remains_x").
    [GeneratedRegex(@"(?<![\w.])(?:raid_event\.|fight_remains(?![\w.]))", RegexOptions.CultureInvariant)]
    private static partial Regex OldName();
}
