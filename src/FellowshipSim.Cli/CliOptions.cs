using System.Globalization;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Engine;
using FellowshipSim.Engine;

namespace FellowshipSim.Cli;

/// <summary>
/// The <c>simfell</c> command line: the profile file, the sim's fights and the output format. Each sim option is a
/// profile line (<see cref="Lines"/>), added at the end of the profile so it replaces the profile's own line.
/// </summary>
public sealed record CliOptions(string ProfilePath, IReadOnlyList<string> Lines, CliOutputFormat Format)
{
    /// <summary>
    /// The sim of <paramref name="profileText"/> with <see cref="Lines"/> at its end. Iterations and a training dummy
    /// fight's length that neither sets are <see cref="DefaultSettings"/>'.
    /// </summary>
    /// <exception cref="FormatException">The profile does not parse.</exception>
    public SimProfile Read(string profileText)
    {
        var sim = SimProfile.Parse(string.Join('\n', [profileText, .. Lines]));
        var settings = sim.Settings;
        if (!sim.SettingKeys.Contains(SimProfile.Iterations)) settings = settings with { Iterations = DefaultSettings.Iterations };
        if (!sim.SettingKeys.Contains(SimProfile.Duration) && settings.Dungeon == null) settings = settings with { DurationSeconds = DefaultSettings.DurationSeconds };
        return sim with { Settings = settings };
    }

    /// <summary>
    /// Settings of a command line naming none. Iterations are <see cref="MonteCarloSimulator.Run"/>'s default and the
    /// duration is the benchmarks' Monte Carlo fight; targets, seed and threads are <see cref="SimSettings"/>'s defaults.
    /// </summary>
    public static readonly SimSettings DefaultSettings = new(Iterations: 100, DurationSeconds: 300);

    public static string Usage => $"""
        Usage: simfell --profile <file.simc> [options]

        Runs a Monte Carlo sim of a simc profile. The action list is the profile's actions lines, and its sim settings
        (iterations=, duration=, a dungeon's lines and the rest the Sim page writes) are the defaults of the options below:
        each option is a line added at the end of the profile, and the last line of a setting is the setting.

        Options:
          --profile <file>      Profile to simulate (required). A relative path is relative to the current directory.
          --iterations <n>      Fights (default {DefaultSettings.Iterations}).
          --duration <seconds>  Length of each fight of training dummies (default {DefaultSettings.DurationSeconds.ToString(CultureInfo.InvariantCulture)}).
          --dungeon <name|file> Fights of a dungeon: a built-in one ({string.Join(", ", DefaultDungeons.Names)}) or a dungeon .simc file.
                                Its pulls set the enemies, and its time limit the longest fight. Without it, the fights are training dummies.
          --targets <n>         Training dummies in each fight (default {DefaultSettings.TargetCount}).
          --seed <n>            Seed of the first fight; fight i has seed + i (default {DefaultSettings.FirstSeed}).
          --threads <n>         Most threads the fights use (default {MonteCarloSimulator.DefaultDegreeOfParallelism}, 80% of the logical processors).
          --reactions <on|off>  A player's reaction time before the cast after a channel, as in the combat logs (default on).
                                Off, the hero casts the moment a channel ends.
          --proc-reaction <ms>  Milliseconds a proc's gain takes to reach the action list (default {DefaultSettings.ProcReactionSeconds * 1000:0}).
                                Until then, buff.<proc> reads the proc as it was before the gain. 0 shows the gain at once.
          --format <text|json>  Result format on stdout (default text). json is the SimResult record.
          -h, --help            This text.

        Progress is on stderr. Ctrl+C cancels the sim (exit code 130).
        """;

    /// <summary>The options in <paramref name="args"/>, or null for <c>--help</c>.</summary>
    /// <exception cref="CliUsageException">An unknown option, a missing or malformed value, or no <c>--profile</c>.</exception>
    public static CliOptions? Parse(IReadOnlyList<string> args)
    {
        string? profile = null;
        var lines = new List<string>();
        var format = CliOutputFormat.Text;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg is "-h" or "--help") return null;
            if (!arg.StartsWith("--", StringComparison.Ordinal)) throw new CliUsageException($"Unexpected argument '{arg}'.");

            int eq = arg.IndexOf('=');
            string name = eq < 0 ? arg : arg[..eq];
            string value = eq >= 0 ? arg[(eq + 1)..]
                : i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i]
                : throw new CliUsageException($"{name} needs a value.");
            switch (name)
            {
                case "--profile": profile = value; break;
                case "--iterations": lines.Add(Line(SimProfile.Iterations, Integer(name, value, min: 1))); break;
                case "--duration": lines.Add(Line(SimProfile.Duration, Seconds(name, value))); break;
                case "--dungeon": lines.Add(Dungeon(value).Format()); break;
                case "--targets": lines.Add(Line(SimProfile.Targets, Integer(name, value, min: 1))); break;
                case "--seed": lines.Add(Line(SimProfile.Seed, Integer(name, value, min: int.MinValue))); break;
                case "--threads": lines.Add(Line(SimProfile.Threads, Integer(name, value, min: 1))); break;
                case "--reactions":
                {
                    bool reactions = value switch
                    {
                        "on" => true,
                        "off" => false,
                        _ => throw new CliUsageException($"--reactions is on or off, not '{value}'."),
                    };
                    lines.Add($"{SimProfile.ReactionDelays}={(reactions ? "true" : "false")}");
                    break;
                }
                case "--proc-reaction": lines.Add(Line(SimProfile.ProcReaction, Milliseconds(name, value) / 1000)); break;
                case "--format":
                    format = value switch
                    {
                        "text" => CliOutputFormat.Text,
                        "json" => CliOutputFormat.Json,
                        _ => throw new CliUsageException($"--format is text or json, not '{value}'."),
                    };
                    break;
                default: throw new CliUsageException($"Unknown option '{name}'.");
            }
        }
        if (string.IsNullOrWhiteSpace(profile)) throw new CliUsageException("--profile is required.");
        return new CliOptions(profile, lines, format);
    }

    private static string Line(string key, double value) => $"{key}={value.ToString(CultureInfo.InvariantCulture)}";

    // The built-in dungeon of that name, or the dungeon in that file.
    private static DungeonProfile Dungeon(string value)
    {
        try
        {
            var dungeon = DefaultDungeons.Read(value) is { } builtIn ? DungeonProfile.Parse(builtIn) : DungeonProfile.Load(value);
            return dungeon.Problem() is { } problem ? throw new CliUsageException(problem) : dungeon;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            throw new CliUsageException($"--dungeon is a built-in dungeon or a dungeon file: {e.Message}");
        }
    }

    private static int Integer(string name, string value, int min) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n) && n >= min
            ? n
            : throw new CliUsageException(min == int.MinValue ? $"{name} is a whole number, not '{value}'." : $"{name} is a whole number of at least {min}, not '{value}'.");

    private static double Milliseconds(string name, string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) && ms >= 0 && double.IsFinite(ms)
            ? ms
            : throw new CliUsageException($"{name} is a number of milliseconds of at least 0, not '{value}'.");

    private static double Seconds(string name, string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double s) && s > 0 && double.IsFinite(s)
            ? s
            : throw new CliUsageException($"{name} is a number of seconds above 0, not '{value}'.");
}
