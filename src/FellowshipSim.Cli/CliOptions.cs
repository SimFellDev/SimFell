using System.Globalization;
using FellowshipSim.Core.Engine;
using FellowshipSim.Engine;

namespace FellowshipSim.Cli;

/// <summary>The <c>simfell</c> command line: the profile file, the sim's fights and the output format.</summary>
public sealed record CliOptions(string ProfilePath, SimSettings Settings, CliOutputFormat Format)
{
    /// <summary>
    /// Settings of a command line naming none. Iterations are <see cref="MonteCarloSimulator.Run"/>'s default and the
    /// duration is the benchmarks' Monte Carlo fight; targets, seed and threads are <see cref="SimSettings"/>'s defaults.
    /// </summary>
    public static readonly SimSettings DefaultSettings = new(Iterations: 100, DurationSeconds: 300);

    public static string Usage => $"""
        Usage: simfell --profile <file.simc> [options]

        Runs a Monte Carlo sim of a simc profile. The action list is the profile's actions lines.

        Options:
          --profile <file>      Profile to simulate (required). A relative path is relative to the current directory.
          --iterations <n>      Fights (default {DefaultSettings.Iterations}).
          --duration <seconds>  Length of each fight (default {DefaultSettings.DurationSeconds.ToString(CultureInfo.InvariantCulture)}).
          --targets <n>         Targets in each fight (default {DefaultSettings.TargetCount}).
          --seed <n>            Seed of the first fight; fight i has seed + i (default {DefaultSettings.FirstSeed}).
          --threads <n>         Most threads the fights use (default {MonteCarloSimulator.DefaultDegreeOfParallelism}, 80% of the logical processors).
          --format <text|json>  Result format on stdout (default text). json is the SimResult record.
          -h, --help            This text.

        Progress is on stderr. Ctrl+C cancels the sim (exit code 130).
        """;

    /// <summary>The options in <paramref name="args"/>, or null for <c>--help</c>.</summary>
    /// <exception cref="CliUsageException">An unknown option, a missing or malformed value, or no <c>--profile</c>.</exception>
    public static CliOptions? Parse(IReadOnlyList<string> args)
    {
        string? profile = null;
        var settings = DefaultSettings;
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
                case "--iterations": settings = settings with { Iterations = Integer(name, value, min: 1) }; break;
                case "--duration": settings = settings with { DurationSeconds = Seconds(name, value) }; break;
                case "--targets": settings = settings with { TargetCount = Integer(name, value, min: 1) }; break;
                case "--seed": settings = settings with { FirstSeed = Integer(name, value, min: int.MinValue) }; break;
                case "--threads": settings = settings with { DegreeOfParallelism = Integer(name, value, min: 1) }; break;
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
        return new CliOptions(profile, settings, format);
    }

    private static int Integer(string name, string value, int min) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n) && n >= min
            ? n
            : throw new CliUsageException(min == int.MinValue ? $"{name} is a whole number, not '{value}'." : $"{name} is a whole number of at least {min}, not '{value}'.");

    private static double Seconds(string name, string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double s) && s > 0 && double.IsFinite(s)
            ? s
            : throw new CliUsageException($"{name} is a number of seconds above 0, not '{value}'.");
}
