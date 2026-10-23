using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Replays every downloaded fight (data/fights) for its Ardeos players alone, and prints per damage ID how the engine's
/// hits compare with the log's. With no such fight downloaded it does nothing.
/// Run with: dotnet test --filter ArdeosReplayTests
/// </summary>
public class ArdeosReplayTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Ardeos_Fights_Replay()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        foreach (var (reportCode, fightId) in library.ListDownloadedFights())
        {
            var raw = await library.GetRawFightAsync(null, reportCode, fightId);
            foreach (var player in ReplayScriptBuilder.Players(raw).Where(p => p.Hero == "Ardeos"))
            {
                var script = ReplayScript.FromJson(ReplayScriptBuilder.Build(raw, player.Id).ToJsonString());
                if (Environment.GetEnvironmentVariable("ARDEOS_CHARACTER_JSON") is { } characterFile)
                {
                    File.WriteAllText(characterFile, System.Text.Json.JsonSerializer.Serialize(script.Character, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                var hero = ReplayDriver.CreateHero(script);
                var options = new ReplayOptions { StopOnFirstFailure = false };
                var result = new ReplayDriver(hero, script, options).Run();
                var (engineDamage, logDamage) = ReplayReportWriter.DamageTotals(result);
                output.WriteLine($"{player.Name} {reportCode} fight {fightId}: {result.MatchedLogHits}/{result.LogHits} hits, " +
                                 $"damage {engineDamage / logDamage:P1} of log, {result.PassedSteps}/{result.Steps.Count} clean steps, " +
                                 $"{result.ForcedCasts.Count} forced casts");
                Report(result, options);
                Assert.True(result.MatchedLogHits > 0);
            }
        }
    }

    private void Report(ReplayResult result, ReplayOptions options)
    {
        output.WriteLine("");
        output.WriteLine("id        name                  log hits  matched  engine-only  log dmg      engine dmg   in spread  ratio(log/engine)");
        var logById = result.LogDamage.GroupBy(h => h.AbilityId).ToDictionary(g => g.Key, g => g.ToList());
        var engineById = result.AllHits.GroupBy(h => h.AbilityId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (int id in logById.Keys.Union(engineById.Keys).OrderByDescending(id => logById.GetValueOrDefault(id)?.Sum(h => h.TotalAmount) ?? 0))
        {
            var log = logById.GetValueOrDefault(id) ?? [];
            var engine = engineById.GetValueOrDefault(id) ?? [];
            var matched = engine.Where(h => h.Log != null).ToList();
            double ratio = matched.Sum(h => h.EngineAmount) > 0 ? matched.Sum(h => h.Log!.TotalAmount) / matched.Sum(h => h.EngineAmount) : 0;
            string name = (engine.FirstOrDefault()?.AbilityName ?? log.FirstOrDefault()?.AbilityName ?? "?").PadRight(20)[..20];
            output.WriteLine($"{id,-9} {name}  {log.Count,8}  {matched.Count,7}  {engine.Count - matched.Count,11}  {log.Sum(h => h.TotalAmount),11:N0}  {engine.Sum(h => h.EngineAmount),11:N0}  " +
                             $"{matched.Count(h => h.WithinSpread(options)),9}  {ratio,6:F3}");
        }
        output.WriteLine("");
        foreach (var group in result.Steps.SelectMany(s => s.Issues).GroupBy(i => (i.Category, i.Subject)).OrderByDescending(g => g.Count()).Take(40))
        {
            output.WriteLine($"{group.Count(),5} [{group.Key.Category}] {group.Key.Subject}: {group.First().Message} (first @ {group.First().Time:F3})");
        }
        output.WriteLine("");
        foreach (var unmodeled in result.UnmodeledAbilities) output.WriteLine($"unmodeled ability {unmodeled.Key}: {unmodeled.Value}");
        foreach (var unmodeled in result.UnmodeledCustomizations) output.WriteLine($"unmodeled {unmodeled}");
        foreach (var external in result.UnmodeledExternalAuras) output.WriteLine($"unmodeled external aura {external.Key}: {external.Value}");
        foreach (var proc in result.ProcStats) output.WriteLine($"proc {proc.Key}: {proc.Procs}/{proc.Rolls} (expected {proc.ExpectedProcs:F1})");
        foreach (var forced in result.ForcedCasts.GroupBy(f => f.Split(':')[0]).OrderByDescending(g => g.Count()).Take(15))
        {
            output.WriteLine($"forced {forced.Count()}: {forced.First()}");
        }
        string? folder = Environment.GetEnvironmentVariable("ARDEOS_HITS_FOLDER");
        if (folder == null) return;
        string dump = Path.Combine(folder, $"hits_{result.ReportCode}_{result.FightId}.csv");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        File.WriteAllLines(dump, result.AllHits.Select(h => string.Format(inv, "{0:F3},{1},{2},{3:F1},{4},{5},{6},{7:F3}",
            h.EngineTime, h.AbilityId, h.Target == null ? "" : $"{h.Target.Id}.{h.Target.Instance}", h.EngineAmount, h.Crit ? 1 : 0,
            h.Log == null ? "" : h.Log.TotalAmount.ToString("F0", inv), h.Log == null ? "" : h.Log.Time.ToString("F3", inv), h.TickFraction)));
    }
}
