using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Replays every downloaded fight (data/fights) for its Elarion players alone, and prints per damage ID how the engine's
/// hits compare with the log's. With no such fight downloaded it does nothing.
/// Run with: dotnet test --filter ElarionReplayTests
/// </summary>
public class ElarionReplayTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Elarion_Fights_Replay()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        foreach (var (reportCode, fightId, playerId) in library.ListDownloadedFights())
        {
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId, playerId);
            foreach (var player in ReplayScriptBuilder.Players(raw).Where(p => p.Id == playerId && p.Hero == "Elarion"))
            {
                var script = ReplayScript.FromJson(ReplayScriptBuilder.Build(raw, player.Id));
                if (script.Character == null) continue;
                if (Environment.GetEnvironmentVariable("ELARION_CHARACTER_JSON") is { } characterFile)
                {
                    File.WriteAllText(characterFile, System.Text.Json.JsonSerializer.Serialize(script.Character, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                var hero = ReplayDriver.CreateHero(script);
                var trace = new List<string>();
                if (Environment.GetEnvironmentVariable("ELARION_TRACE") != null)
                {
                    // Each hit with the Lunarlight Mark stacks its enemy holds once the hit's salvo (if any) took its stack.
                    hero.OnDamageDealt += (in Core.Domain.DamageContext hit) => trace.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0:F3},{1},{2}.{3},{4}", hit.Timestamp.TotalSeconds, hit.AbilityId, hit.Target?.Id, hit.Target?.InstanceId,
                        hit.Target?.Auras.GetStacks(Core.Heroes.Elarion.Abilities.LunarlightMark.Debuff, hit.Timestamp)));
                }
                var options = new ReplayOptions { StopOnFirstFailure = false };
                var result = new ReplayDriver(hero, script, options).Run();
                var (engineDamage, logDamage) = ReplayReportWriter.DamageTotals(result);
                output.WriteLine($"{player.Name} {reportCode} fight {fightId}: {result.MatchedLogHits}/{result.LogHits} hits, " +
                                 $"damage {engineDamage / logDamage:P1} of log, {result.PassedSteps}/{result.Steps.Count} clean steps, " +
                                 $"{result.ForcedCasts.Count} forced casts");
                Report(result, options);
                if (trace.Count > 0 && Environment.GetEnvironmentVariable("ELARION_HITS_FOLDER") is { } traceFolder)
                {
                    File.WriteAllLines(Path.Combine(traceFolder, $"trace_{reportCode}_{fightId}.csv"), trace);
                }
                ExactReplayAssertions.Hold(result);
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
            var matched = engine.Where(h => h.Log != null && h.LandedTime.HasValue).ToList();
            double ratio = matched.Sum(h => h.EngineAmount) > 0 ? matched.Sum(h => h.Log!.UnblockedAmount) / matched.Sum(h => h.EngineAmount) : 0;
            string name = (engine.FirstOrDefault()?.AbilityName ?? log.FirstOrDefault()?.AbilityName ?? "?").PadRight(20)[..20];
            output.WriteLine($"{id,-9} {name}  {log.Count,8}  {matched.Count,7}  {engine.Count - matched.Count,11}  {log.Sum(h => h.TotalAmount),11:N0}  {engine.Sum(h => h.EngineAmount),11:N0}  " +
                             $"{matched.Count(h => h.WithinRange),9}  {ratio,6:F3}");
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
        string? folder = Environment.GetEnvironmentVariable("ELARION_HITS_FOLDER");
        if (folder == null) return;
        string dump = Path.Combine(folder, $"hits_{result.ReportCode}_{result.FightId}.csv");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        File.WriteAllLines(Path.Combine(folder, $"steps_{result.ReportCode}_{result.FightId}.csv"), result.Steps.Select(s => string.Format(inv,
            "{0:F3},{1},{2:F3},{3}", s.Time, s.AbilityName, s.EngineHaste,
            string.Join(";", s.Resources.Select(r => string.Format(inv, "{0}={1:F1}/{2:F1}", r.Resource, r.Engine, r.Log))))));
        File.WriteAllLines(dump, result.AllHits.Select(h => string.Format(inv, "{0:F3},{1},{2},{3:F1},{4},{5},{6},{7:F3}",
            h.EngineTime, h.AbilityId, h.Target == null ? "" : $"{h.Target.Id}.{h.Target.Instance}", h.EngineAmount, h.Crit ? 1 : 0,
            h.Log == null ? "" : h.Log.UnblockedAmount.ToString("F0", inv), h.Log == null ? "" : h.Log.Time.ToString("F3", inv), h.TickFraction)));
    }
}
