using System.Globalization;
using System.Text;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Engine;

/// <summary>
/// A sim's whole <c>.simc</c> profile: the character's lines and action list (<see cref="SimcProfile"/>), the sim's
/// settings, and the dungeon of a dungeon fight (<see cref="DungeonProfile"/>'s lines). The lines are in any order. The
/// last line of a setting is the setting, so a line added at the end replaces the profile's. A <c>dungeon=</c> line starts
/// a dungeon, and the profile's dungeon is the last one. <see cref="Format"/> writes the character's lines, the settings,
/// the dungeon, then the action list.
/// <code>
/// rime="sww"
/// primary_stat=161
/// ...
/// iterations=1000
/// threads=8
/// seed=1
/// duration=300
/// targets=1
/// length_variance=0.2
/// hero_share_variance=0.2
/// health_follows_time=true
/// boss=true
/// starting_spirit=0
/// reaction_delays=true
/// proc_reaction=0.5
///
/// actions+=/frost_bolt
/// </code>
/// A dungeon fight has the dungeon's lines (<c>dungeon=</c>, <c>difficulty=</c>, <c>hero_share=</c>, <c>time_limit=</c>
/// and <c>pull.X=</c>) in place of <c>duration</c> and <c>targets</c>: its time limit is each fight's length, and its pulls
/// set the enemies. <see cref="SettingKeys"/> are the setting keys the text has, <c>dungeon</c> for the dungeon's lines; a
/// setting the text does not have is <see cref="SimSettings"/>'s default, and the iterations and duration are
/// <see cref="DefaultIterations"/> and <see cref="DefaultDurationSeconds"/>.
/// </summary>
public sealed record SimProfile(SimcProfile Profile, SimSettings Settings, IReadOnlySet<string> SettingKeys)
{
    public const string Iterations = "iterations";
    public const string Threads = "threads";
    public const string Seed = "seed";
    public const string Duration = "duration";
    public const string Targets = "targets";
    public const string LengthVariance = "length_variance";
    public const string HeroShareVariance = "hero_share_variance";
    public const string HealthFollowsTime = "health_follows_time";
    public const string Boss = "boss";
    public const string StartingSpirit = "starting_spirit";
    public const string ReactionDelays = "reaction_delays";
    public const string ProcReaction = "proc_reaction";

    /// <summary>The key in <see cref="SettingKeys"/> of the dungeon's lines.</summary>
    public const string Dungeon = "dungeon";

    /// <summary>Fights of a profile with no <c>iterations</c> line.</summary>
    public const int DefaultIterations = 1000;

    /// <summary>Length of each training dummy fight of a profile with no <c>duration</c> line, in seconds.</summary>
    public const double DefaultDurationSeconds = 300;

    private static readonly HashSet<string> DungeonKeys = ["dungeon", "difficulty", "hero_share", "time_limit"];

    /// <summary>A profile with every setting of <paramref name="settings"/>.</summary>
    public SimProfile(SimcProfile profile, SimSettings settings) : this(profile, settings, AllKeys(settings))
    {
    }

    public static SimProfile Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>The profile <paramref name="text"/> writes; a <see cref="FormatException"/> names the line at fault.</summary>
    public static SimProfile Parse(string text)
    {
        string[] lines = text.Split('\n');
        // Each part keeps every line number: a line of another part is blank in it.
        var character = new string[lines.Length];
        var dungeon = new string[lines.Length];
        Array.Fill(character, "");
        Array.Fill(dungeon, "");
        var keys = new HashSet<string>();
        var values = new Dictionary<string, (int Line, string Text, string Value)>();
        // A comment goes with the line right under it. Comments with a blank line under them, or after the last line, go
        // with the line above them.
        var comments = new List<int>();
        string[]? last = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
            {
                if (last != null)
                {
                    foreach (int c in comments) last[c] = lines[c];
                    comments.Clear();
                }
                continue;
            }
            if (line.StartsWith('#'))
            {
                comments.Add(i);
                continue;
            }
            int eq = line.IndexOf('=');
            string key = eq > 0 ? line[..eq].Trim().TrimEnd('+') : "";
            string[] part;
            if (DungeonKeys.Contains(key) || key.StartsWith("pull.", StringComparison.Ordinal))
            {
                // A dungeon= line starts a dungeon, which replaces the one before.
                if (key == Dungeon) Array.Fill(dungeon, "");
                keys.Add(Dungeon);
                part = dungeon;
            }
            else if (IsSettingKey(key))
            {
                // The last line of a setting is the setting.
                keys.Add(key);
                values[key] = (i + 1, line, line[(eq + 1)..].Trim());
                comments.Clear();
                continue;
            }
            else
            {
                part = character;
            }
            foreach (int c in comments) part[c] = lines[c];
            comments.Clear();
            part[i] = lines[i];
            last = part;
        }
        foreach (int c in comments) (last ?? character)[c] = lines[c];

        var profile = SimcProfile.Parse(string.Join('\n', character));
        var fight = keys.Contains(Dungeon) ? DungeonProfile.Parse(string.Join('\n', dungeon)) : null;
        T Read<T>(string key, T missing, Func<string, T> read)
        {
            if (!values.TryGetValue(key, out var v)) return missing;
            try
            {
                return read(v.Value);
            }
            catch (FormatException e)
            {
                throw Error(v.Line, v.Text, e.Message);
            }
        }
        var settings = new SimSettings(
            Read(Iterations, DefaultIterations, v => Integer(v, min: 1)),
            fight?.TimeLimitSeconds ?? Read(Duration, DefaultDurationSeconds, Seconds),
            fight != null ? 1 : Read(Targets, 1, v => Integer(v, min: 1)),
            Read(Seed, 1, v => Integer(v, min: int.MinValue)),
            Read<int?>(Threads, null, v => Integer(v, min: 1)),
            Read(LengthVariance, 0.0, NonNegative),
            Read(HealthFollowsTime, true, Bool),
            Read(StartingSpirit, 0.0, NonNegative),
            Read(Boss, true, Bool),
            fight,
            Read(HeroShareVariance, 0.0, NonNegative),
            Read(ReactionDelays, true, Bool),
            Read(ProcReaction, SimSettings.DefaultProcReactionSeconds, NonNegative));
        return new SimProfile(profile, settings, keys);
    }

    /// <summary>
    /// The profile's text: the character's lines, the settings, the dungeon's lines, then the action list.
    /// <see cref="Parse"/> reads it back to the same profile.
    /// </summary>
    public string Format()
    {
        var s = new StringBuilder(new SimcProfile(Profile.Build, "").Format());
        s.AppendLine();
        s.AppendLine($"{Iterations}={Text(Settings.Iterations)}");
        if (Settings.DegreeOfParallelism is { } threads) s.AppendLine($"{Threads}={Text(threads)}");
        s.AppendLine($"{Seed}={Text(Settings.FirstSeed)}");
        if (Settings.Dungeon == null)
        {
            s.AppendLine($"{Duration}={Text(Settings.DurationSeconds)}");
            s.AppendLine($"{Targets}={Text(Settings.TargetCount)}");
        }
        s.AppendLine($"{LengthVariance}={Text(Settings.LengthVariance)}");
        s.AppendLine($"{HeroShareVariance}={Text(Settings.HeroShareVariance)}");
        s.AppendLine($"{HealthFollowsTime}={Text(Settings.HealthFollowsTime)}");
        s.AppendLine($"{Boss}={Text(Settings.Boss)}");
        s.AppendLine($"{StartingSpirit}={Text(Settings.StartingSpirit)}");
        s.AppendLine($"{ReactionDelays}={Text(Settings.ReactionDelays)}");
        s.AppendLine($"{ProcReaction}={Text(Settings.ProcReactionSeconds)}");
        if (Settings.Dungeon is { } fight)
        {
            s.AppendLine();
            s.Append(fight.Format());
        }
        if (Profile.ActionList.Length > 0)
        {
            s.AppendLine();
            s.Append(Profile.ActionList);
        }
        return s.ToString();
    }

    private static bool IsSettingKey(string key) => key is Iterations or Threads or Seed or Duration or Targets or LengthVariance
        or HeroShareVariance or HealthFollowsTime or Boss or StartingSpirit or ReactionDelays or ProcReaction;

    private static HashSet<string> AllKeys(SimSettings settings)
    {
        HashSet<string> keys = [Iterations, Seed, LengthVariance, HeroShareVariance, HealthFollowsTime, Boss, StartingSpirit, ReactionDelays, ProcReaction];
        if (settings.DegreeOfParallelism != null) keys.Add(Threads);
        if (settings.Dungeon != null) keys.Add(Dungeon);
        else keys.UnionWith([Duration, Targets]);
        return keys;
    }

    private static FormatException Error(int lineNumber, string line, string problem) =>
        new($"Profile line {lineNumber} '{line}': {problem}.");

    private static int Integer(string value, int min) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n) && n >= min
            ? n
            : throw new FormatException(min == int.MinValue ? $"'{value}' is not a whole number" : $"'{value}' is not a whole number of at least {min}");

    private static double Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && double.IsFinite(n)
            ? n
            : throw new FormatException($"'{value}' is not a number");

    private static double Seconds(string value) => Number(value) is > 0 and var s ? s : throw new FormatException($"'{value}' is not a number of seconds above 0");

    private static double NonNegative(string value) => Number(value) is >= 0 and var n ? n : throw new FormatException($"'{value}' is below 0");

    private static bool Bool(string value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => throw new FormatException($"'{value}' is not true or false"),
    };

    private static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Text(bool value) => value ? "true" : "false";
}
