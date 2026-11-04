using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Replays every downloaded fight (data/fights) for its Gunde players alone, and prints per damage ID how the engine's
/// hits compare with the log's, and the replay's most common issues. With no such fight downloaded it does nothing.
/// GUNDE_FIGHT (a report code, or report code and fight ID as "code_fight") limits it to one fight; GUNDE_CHARACTER_JSON
/// writes the replayed character there, GUNDE_HITS_CSV every hit pair; GUNDE_FREEDICE (a number of runs) adds a free dice
/// replay of each fight.
/// Run with: dotnet test --filter GundeReplayTests
/// </summary>
public class GundeReplayTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Gunde_Fights_Replay()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        string? only = Environment.GetEnvironmentVariable("GUNDE_FIGHT");
        foreach (var (reportCode, fightId) in library.ListDownloadedFights())
        {
            if (only != null && only != reportCode && only != $"{reportCode}_{fightId}") continue;
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId);
            foreach (var player in ReplayScriptBuilder.Players(raw).Where(p => p.Hero == "Gunde"))
            {
                var script = ReplayScript.FromJson(ReplayScriptBuilder.Build(raw, player.Id));
                if (Environment.GetEnvironmentVariable("GUNDE_CHARACTER_JSON") is { } characterFile)
                {
                    File.WriteAllText(characterFile, System.Text.Json.JsonSerializer.Serialize(script.Character, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                var options = new ReplayOptions { StopOnFirstFailure = false };
                var result = new ReplayDriver(ReplayDriver.CreateHero(script), script, options).Run();
                var (engineDamage, logDamage) = ReplayReportWriter.DamageTotals(result);
                output.WriteLine($"{player.Name} {reportCode} fight {fightId} ({script.FightDate:yyyy-MM-dd}): {result.MatchedLogHits}/{result.LogHits} hits, " +
                                 $"damage {engineDamage / logDamage:P1} of log, {result.PassedSteps}/{result.Steps.Count} clean steps, " +
                                 $"{result.ForcedCasts.Count} forced casts");
                Report(result, options);
                ExactReplayAssertions.Hold(result);
                if (int.TryParse(Environment.GetEnvironmentVariable("GUNDE_FREEDICE"), out int runs) && runs > 0)
                {
                    var dice = FreeDiceReplay.Run(script, runs, firstSeed: 1);
                    output.WriteLine($"free dice, {runs} runs: {dice.Mean / dice.LogDamage:P1} of log, log at the {dice.LogPercentile:P0} point");
                    output.WriteLine(dice.ToString());
                }
            }
        }
    }

    private void Report(ReplayResult result, ReplayOptions options)
    {
        output.WriteLine("id        name                  log hits  matched  engine-only  log dmg      engine dmg   in spread  ratio(log/engine)");
        var logById = result.LogDamage.GroupBy(h => h.AbilityId).ToDictionary(g => g.Key, g => g.ToList());
        var engineById = result.AllHits.GroupBy(h => h.AbilityId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (int id in logById.Keys.Union(engineById.Keys).OrderByDescending(id => logById.GetValueOrDefault(id)?.Sum(h => h.TotalAmount) ?? 0))
        {
            var log = logById.GetValueOrDefault(id) ?? [];
            var engine = engineById.GetValueOrDefault(id) ?? [];
            var matched = engine.Where(h => h.Log != null && h.LandedTime.HasValue).ToList();
            double ratio = matched.Sum(h => h.EngineAmount) > 0 ? matched.Sum(h => h.Log!.TotalAmount) / matched.Sum(h => h.EngineAmount) : 0;
            string name = (engine.FirstOrDefault()?.AbilityName ?? log.FirstOrDefault()?.AbilityName ?? "?").PadRight(20)[..20];
            output.WriteLine($"{id,-9} {name}  {log.Count,8}  {matched.Count,7}  {engine.Count - matched.Count,11}  {log.Sum(h => h.TotalAmount),11:N0}  {engine.Sum(h => h.EngineAmount),11:N0}  " +
                             $"{matched.Count(h => h.WithinRange),9}  {ratio,6:F3}");
        }
        foreach (var group in result.Steps.SelectMany(s => s.Issues).GroupBy(i => (i.Category, i.Subject)).OrderByDescending(g => g.Count()).Take(30))
        {
            output.WriteLine($"{group.Count(),5} [{group.Key.Category}] {group.Key.Subject}: {group.First().Message}");
        }
        foreach (var unmodeled in result.UnmodeledAbilities) output.WriteLine($"unmodeled ability {unmodeled.Key}: {unmodeled.Value}");
        foreach (var unmodeled in result.UnmodeledCustomizations) output.WriteLine($"unmodeled {unmodeled}");
        foreach (var proc in result.ProcStats) output.WriteLine($"proc {proc.Key}: {proc.Procs}/{proc.Rolls} (expected {proc.ExpectedProcs:F1})");
        foreach (var forced in result.ForcedCasts.GroupBy(f => f.Split(':')[0][8..]).OrderByDescending(g => g.Count()).Take(12))
        {
            output.WriteLine($"forced {forced.Count()}: {forced.First()}");
        }
        output.WriteLine("");
        if (Environment.GetEnvironmentVariable("GUNDE_HITS_CSV") is not { } dump) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        File.WriteAllLines(dump, result.AllHits.Select(h => string.Format(inv, "{0:F3},{1},{2},{3:F1},{4},{5},{6},{7:F3},{8},{9:F3}",
            h.EngineTime, h.AbilityId, h.Target == null ? "" : $"{h.Target.Id}.{h.Target.Instance}", h.EngineAmount, h.Crit ? 1 : 0,
            h.Log == null ? "" : h.Log.TotalAmount.ToString("F0", inv), h.Log == null ? "" : h.Log.Time.ToString("F3", inv), h.TickFraction,
            h.State == null ? "" : string.Join(" ", h.State.Hero.Auras.Select(a => a.LogId > 0 ? a.LogId.ToString(inv) : a.Name.Replace(" ", "_"))),
            h.TargetHealthFraction)));
    }
}
