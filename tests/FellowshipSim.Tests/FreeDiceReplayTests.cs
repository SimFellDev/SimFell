using FellowshipSim.Engine.Replay;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Replays a downloaded fight many times with free dice (the engine rolls its own crits and procs) and writes the spread of
/// the engine's damage against the log's to data/reviews/&lt;review&gt;.freedice.txt.
/// Choose the fight and the number of runs with the environment variables FREEDICE_REVIEW (a review ID such as
/// yMAZVJL9hTpFjqbC_fight7_p1, or "all" for every downloaded script; default: the first one the engine can replay), FREEDICE_ITERATIONS
/// (default 10) and FREEDICE_SEED (the first run's seed, default 1; "random" picks one). Run i uses seed + i, so a run
/// can be repeated exactly.
/// Run with: dotnet test --filter FreeDice
/// </summary>
public class FreeDiceReplayTests(ITestOutputHelper output)
{
    [Fact]
    public void FreeDice_Replay_Of_One_Fight()
    {
        string data = Path.Combine(TestPaths.RepositoryRoot, "data");
        string scripts = Path.Combine(data, "scripts");
        if (!Directory.Exists(scripts) || Directory.GetFiles(scripts, "*.replay.json").Length == 0)
        {
            output.WriteLine("No replay scripts downloaded; nothing to run.");
            return;
        }

        var all = Directory.GetFiles(scripts, "*.replay.json").OrderBy(f => f)
            .Select(f => Path.GetFileName(f).Replace(".replay.json", "")).ToList();
        string? setting = Environment.GetEnvironmentVariable("FREEDICE_REVIEW");
        var reviews = setting is null or "all" ? all : [setting];
        int iterations = int.TryParse(Environment.GetEnvironmentVariable("FREEDICE_ITERATIONS"), out int n) ? n : 10;
        string? seedSetting = Environment.GetEnvironmentVariable("FREEDICE_SEED");
        int seed = seedSetting == "random" ? Random.Shared.Next(1, int.MaxValue / 2) : int.TryParse(seedSetting, out int s) ? s : 1;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        int replayed = 0;
        foreach (string review in reviews)
        {
            var script = ReplayScript.Load(Path.Combine(scripts, $"{review}.replay.json"));
            // A script of a hero the engine lacks, or of a player with no character, has no fight to replay.
            if (!ReplayDriver.SupportsHero(script.Player.Hero) || script.Character == null) continue;
            var result = FreeDiceReplay.Run(script, iterations, seed);
            string text = $"seeds {seed}-{seed + iterations - 1}\n" + result;
            output.WriteLine($"{review}: {result.Mean / result.LogDamage:P1} of log, log at the {result.LogPercentile:P0} point ({result.Elapsed.TotalSeconds:F1}s)");
            File.WriteAllText(Path.Combine(data, "reviews", $"{review}.freedice.txt"), text);
            File.WriteAllText(Path.Combine(data, "reviews", $"{review}.freedice.json"), System.Text.Json.JsonSerializer.Serialize(result.ToSummary()));
            FreeDiceReportWriter.Write(result, Path.Combine(data, "reviews", $"{review}.freedice.html"),
                $"<nav class=\"fightnav\"><a href=\"{review}.html\">Exact replay</a></nav>");

            Assert.Equal(iterations, result.EngineTotals.Count);
            Assert.True(result.Mean > 0);
            replayed++;
            if (setting == null) break;
        }
        output.WriteLine($"{replayed} fights x {iterations} runs in {clock.Elapsed.TotalSeconds:F1}s");
    }
}
