using FellowshipSim.Core;
using FellowshipSim.Core.Import;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The action list of a .simc file and its hero. <see cref="ActionList"/> is the file's text with each profile line
/// (<c>key=value</c> with a key other than <c>actions</c>) in spaces, so each offset is the file's own.
/// <see cref="ProfileLines"/> has the index of each profile line. A profile's hero is its hero line's (<c>rime="sww"</c>);
/// an action list alone has the hero of the nearest folder of its path named after one (<c>Heroes/Rime/ActionLists</c>,
/// <c>data/apls/Rime</c>). <see cref="Hero"/> is null for a hero the engine does not have.
/// </summary>
public sealed record SimcFile(string? Hero, string ActionList, IReadOnlySet<int> ProfileLines)
{
    public static SimcFile Of(string? path, string text)
    {
        string? hero = null;
        bool heroLineRead = false;
        var actionList = text.ToCharArray();
        var profileLines = new HashSet<int>();
        string[] lines = text.Split('\n');
        for (int n = 0, lineStart = 0; n < lines.Length; lineStart += lines[n].Length + 1, n++)
        {
            string line = lines[n].Trim();
            if (line.Length == 0 || line.StartsWith('#') || SimcProfile.IsActionLine(line) || line.IndexOf('=') is not (> 0 and var eq)) continue;
            if (!heroLineRead) hero = SimcProfile.HeroOf(line[..eq].Trim());
            heroLineRead = true;
            profileLines.Add(n);
            for (int i = lineStart; i < lineStart + lines[n].Length; i++)
            {
                if (actionList[i] != '\r') actionList[i] = ' ';
            }
        }
        hero ??= HeroOfFolder(path);
        return new SimcFile(hero != null && Catalog.HasHero(hero) ? hero : null, new string(actionList), profileLines);
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
