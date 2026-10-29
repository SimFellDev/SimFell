using System.Globalization;
using System.Text;

namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// A dungeon fight: its segments in order, each starting as the one before it ends. In simc style, one
/// <c>key=value</c> per line:
/// <code>
/// dungeon=Default
/// segment=trash,enemies=5,priority=1,length=60
/// segment=boss,length=60,creature="Ancient Koros"
/// segment=council,enemies=3,length=75,final=1
/// </code>
/// Lines starting with '#' are comments. A segment's kind is <c>trash</c>, <c>boss</c> or <c>council</c>; its options
/// are <c>length</c> (seconds), <c>enemies</c> (trash and council), <c>priority</c> (trash), <c>final</c> (boss and
/// council) and <c>creature</c> (a quoted name when it has a comma).
/// </summary>
public sealed record DungeonProfile(string Name, IReadOnlyList<DungeonSegment> Segments)
{
    /// <summary>The fight's length: every segment's, in seconds.</summary>
    public double LengthSeconds => Segments.Sum(s => s.LengthSeconds);

    /// <summary>The problem with the profile, or null when it is valid.</summary>
    public string? Problem() =>
        Segments.Count == 0 ? "A dungeon has at least one segment."
        : Segments.Select((s, i) => s.Problem() is { } p ? $"Segment {i + 1}: {p}." : null).FirstOrDefault(p => p != null);

    public static DungeonProfile Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>The profile <paramref name="text"/> writes; a <see cref="FormatException"/> names the line at fault.</summary>
    public static DungeonProfile Parse(string text)
    {
        string? name = null;
        var segments = new List<DungeonSegment>();
        int lineNumber = 0;
        foreach (string raw in text.Split('\n'))
        {
            lineNumber++;
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) throw Error(lineNumber, line, "expected key=value");
            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            try
            {
                switch (key)
                {
                    case "dungeon":
                        name = Unquote(value);
                        break;
                    case "segment":
                        var segment = ParseSegment(value);
                        if (segment.Problem() is { } problem) throw new FormatException(problem);
                        segments.Add(segment);
                        break;
                    default:
                        throw new FormatException($"'{key}' is not a dungeon option (dungeon, segment)");
                }
            }
            catch (FormatException e)
            {
                throw Error(lineNumber, line, e.Message);
            }
        }
        if (string.IsNullOrWhiteSpace(name)) throw new FormatException("The dungeon has no dungeon=<name> line.");
        if (segments.Count == 0) throw new FormatException("The dungeon has no segment line.");
        return new DungeonProfile(name, segments);
    }

    /// <summary>The profile's text, which <see cref="Parse"/> reads back to the same profile.</summary>
    public string Format()
    {
        var text = new StringBuilder();
        text.Append("dungeon=").Append(Quote(Name)).Append('\n');
        foreach (var s in Segments)
        {
            text.Append("segment=").Append(s.Kind.ToString().ToLowerInvariant());
            if (s.Kind != DungeonSegmentKind.Boss) text.Append(",enemies=").Append(s.Enemies.ToString(CultureInfo.InvariantCulture));
            if (s.Priority > 0) text.Append(",priority=").Append(s.Priority.ToString(CultureInfo.InvariantCulture));
            text.Append(",length=").Append(s.LengthSeconds.ToString(CultureInfo.InvariantCulture));
            if (s.Final) text.Append(",final=1");
            if (!string.IsNullOrEmpty(s.Creature)) text.Append(",creature=").Append(Quote(s.Creature));
            text.Append('\n');
        }
        return text.ToString();
    }

    private static DungeonSegment ParseSegment(string value)
    {
        var parts = SplitOptions(value);
        var kind = parts[0] switch
        {
            "trash" => DungeonSegmentKind.Trash,
            "boss" => DungeonSegmentKind.Boss,
            "council" => DungeonSegmentKind.Council,
            _ => throw new FormatException($"'{parts[0]}' is not a segment kind (trash, boss, council)"),
        };
        var segment = new DungeonSegment(kind, 0, kind == DungeonSegmentKind.Council ? DungeonSegment.CouncilBosses : 1);
        foreach (string option in parts.Skip(1))
        {
            int eq = option.IndexOf('=');
            if (eq <= 0) throw new FormatException($"expected option=value, not '{option}'");
            string key = option[..eq].Trim();
            string v = option[(eq + 1)..].Trim();
            segment = key switch
            {
                "length" => segment with { LengthSeconds = Number(key, v) },
                "enemies" when kind != DungeonSegmentKind.Boss => segment with { Enemies = Integer(key, v) },
                "priority" => segment with { Priority = Integer(key, v) },
                "final" => segment with { Final = Integer(key, v) != 0 },
                "creature" => segment with { Creature = Unquote(v) },
                "enemies" => throw new FormatException("a boss segment has one enemy; a council has enemies"),
                _ => throw new FormatException($"'{key}' is not a segment option (length, enemies, priority, final, creature)"),
            };
        }
        return segment;
    }

    // The segment's kind and options: split at each comma outside quotes.
    private static List<string> SplitOptions(string value)
    {
        var parts = new List<string>();
        var part = new StringBuilder();
        bool quoted = false;
        foreach (char c in value)
        {
            if (c == '"') quoted = !quoted;
            if (c == ',' && !quoted)
            {
                parts.Add(part.ToString().Trim());
                part.Clear();
            }
            else part.Append(c);
        }
        if (quoted) throw new FormatException("a quote has no closing quote");
        parts.Add(part.ToString().Trim());
        return parts;
    }

    private static double Number(string key, string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)
            ? n : throw new FormatException($"{key} is a number, not '{value}'");

    private static int Integer(string key, string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
            ? n : throw new FormatException($"{key} is a whole number, not '{value}'");

    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static string Quote(string value) => value.Contains(',') || value.Contains('"') || value != value.Trim() ? $"\"{value}\"" : value;

    private static FormatException Error(int lineNumber, string line, string problem) =>
        new($"Dungeon line {lineNumber} '{line}': {problem}.");
}
