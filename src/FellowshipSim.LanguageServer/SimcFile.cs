using System.Text.RegularExpressions;
using FellowshipSim.Core;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// A .simc file's two parts at the file's own offsets: <see cref="ActionList"/> is its text with each profile line in spaces,
/// and <see cref="Profile"/> its text with every other line in spaces. <see cref="ProfileLines"/> has the index of each profile
/// line: a character, setting or dungeon line. Below a hero line every line other than a comment or an action line is one;
/// with no hero line, a <c>key=value</c> line is one, and <c>frost_bolt,if=x</c> is an action.
/// <see cref="ProfileHero"/> is the hero line's hero (<c>rime="sww"</c>), any hero of the game; <see cref="Hero"/> is the hero
/// of the action list, the hero line's or else the nearest folder's of the path named after one
/// (<c>Heroes/Rime/ActionLists</c>, <c>data/apls/Rime</c>), and is null for a hero the engine does not have.
/// </summary>
public sealed partial record SimcFile(string? Hero, string? ProfileHero, string ActionList, string Profile, IReadOnlySet<int> ProfileLines)
{
    [GeneratedRegex(@"^[A-Za-z0-9_.]+\s*\+?=")]
    private static partial Regex KeyedLine();

    public static SimcFile Of(string? path, string text)
    {
        string[] lines = text.Split('\n');
        string? profileHero = lines.FirstOrDefault(SimProfile.IsCharacterLine) is { } first ? SimcProfile.HeroOf(SimProfile.KeyOf(first.Trim())) : null;

        var actionList = text.ToCharArray();
        var profile = text.ToCharArray();
        var profileLines = new HashSet<int>();
        for (int n = 0, lineStart = 0; n < lines.Length; lineStart += lines[n].Length + 1, n++)
        {
            string line = lines[n].Trim();
            bool isProfileLine = line.Length > 0 && !line.StartsWith('#') && !SimcProfile.IsActionLine(line) && (profileHero != null || KeyedLine().IsMatch(line));
            if (isProfileLine) profileLines.Add(n);
            Array.Fill(isProfileLine ? actionList : profile, ' ', lineStart, lines[n].TrimEnd('\r').Length);
        }
        string? hero = profileHero ?? HeroOfFolder(path);
        return new SimcFile(hero != null && Catalog.HasHero(hero) ? hero : null, profileHero, new string(actionList), new string(profile), profileLines);
    }

    private static string? HeroOfFolder(string? path)
    {
        for (var folder = path == null ? null : new FileInfo(path).Directory; folder != null; folder = folder.Parent)
        {
            if (BuildHeroes.Find(folder.Name) is { } hero) return hero;
        }
        return null;
    }
}
