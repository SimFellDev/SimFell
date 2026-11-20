using System.Text;

namespace FellowshipSim.Codex.Generators;

/// <summary>
/// The class name of each creature of the game data, for <see cref="CreatureGenerator"/> and <see cref="DungeonGenerator"/>:
/// the creature's name in Pascal case (letters and digits only, an apostrophe inside its word) and <c>GameCreature</c>. The
/// creatures sharing a name, a creature without one and a name starting with a digit have the creature's ID after the name.
/// </summary>
internal static class CreatureClassNames
{
    /// <summary>Each creature's class name, by creature ID.</summary>
    public static Dictionary<int, string> Of(IEnumerable<(int Id, string Name)> creatures)
    {
        var all = creatures.ToList();
        var shared = new HashSet<string>(all.GroupBy(c => PascalCase(c.Name)).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.Ordinal);
        var names = new Dictionary<int, string>();
        foreach (var (id, creatureName) in all)
        {
            string name = PascalCase(creatureName);
            if (name.Length == 0 || shared.Contains(name) || !char.IsLetter(name[0])) name = $"{(name.Length == 0 ? "Creature" : name)}{id}";
            names[id] = $"{name}GameCreature";
        }
        return names;
    }

    /// <summary>The name's words, each starting upper case, with letters and digits only.</summary>
    public static string PascalCase(string name)
    {
        var result = new StringBuilder();
        bool start = true;
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                result.Append(start ? char.ToUpperInvariant(c) : c);
                start = false;
            }
            // An apostrophe stays inside its word: Al'zerac is Alzerac.
            else if (c != '\'') start = true;
        }
        return result.ToString();
    }
}
