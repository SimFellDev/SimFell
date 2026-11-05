using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Replays every downloaded fight (data/fights) for its Tariq players alone, and prints per damage ID how the engine's
/// hits compare with the log's, and the replay's most common issues. With no such fight downloaded it does nothing.
/// TARIQ_FIGHT (a report code, or report code and fight ID as "code_fight") limits it to one fight.
/// Run with: dotnet test --filter TariqReplayTests
/// </summary>
public class TariqReplayTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Tariq_Fights_Replay()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        string? only = Environment.GetEnvironmentVariable("TARIQ_FIGHT");
        foreach (var (reportCode, fightId, playerId) in library.ListDownloadedFights())
        {
            if (only != null && only != reportCode && only != $"{reportCode}_{fightId}") continue;
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId, playerId);
            foreach (var player in ReplayScriptBuilder.Players(raw).Where(p => p.Id == playerId && p.Hero == "Tariq"))
            {
                var script = ReplayScript.FromJson(ReplayScriptBuilder.Build(raw, player.Id));
                // TARIQ_CHARACTER_JSON writes the imported character (the smoke tests' fixture comes from fight 4's).
                if (Environment.GetEnvironmentVariable("TARIQ_CHARACTER_JSON") is { } characterFile)
                {
                    File.WriteAllText(characterFile, System.Text.Json.JsonSerializer.Serialize(script.Character,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                // TARIQ_PROFILE writes it as a .simc profile, less the augmentations and glyphs the catalog does not have
                // (data/profiles/Tariq comes from fight 4's).
                if (Environment.GetEnvironmentVariable("TARIQ_PROFILE") is { } profileFile)
                {
                    var build = script.Character!;
                    build.Augmentations = build.Augmentations.Where(t => FellowshipSim.Core.Catalog.CreateAugmentation(t.Id, t.Rank) != null).ToList();
                    build.Glyphs = build.Glyphs.Where(b => FellowshipSim.Core.Catalog.CreateGlyph(b.Id, b.Rank) != null).ToList();
                    File.WriteAllText(profileFile, new FellowshipSim.Core.Import.SimcProfile(build, "").Format());
                }
                var options = new ReplayOptions { StopOnFirstFailure = false };
                var result = new ReplayDriver(ReplayDriver.CreateHero(script), script, options).Run();
                var (engineDamage, logDamage) = ReplayReportWriter.DamageTotals(result);
                output.WriteLine($"{player.Name} {reportCode} fight {fightId} ({script.FightDate:yyyy-MM-dd}): {result.MatchedLogHits}/{result.LogHits} hits, " +
                                 $"damage {engineDamage / logDamage:P1} of log, {result.PassedSteps}/{result.Steps.Count} clean steps, " +
                                 $"{result.ForcedCasts.Count} forced casts");
                Report(result, options);
                ExactReplayAssertions.Hold(result);
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
        // TARIQ_ISSUE prints the first issues whose subject contains it, with their times.
        if (Environment.GetEnvironmentVariable("TARIQ_ISSUE") is { } subject)
        {
            foreach (var issue in result.Steps.SelectMany(s => s.Issues).Where(i => i.Subject?.Contains(subject) == true).Take(15))
            {
                output.WriteLine($"  {issue.Time:F3} {issue.Message}");
            }
        }
        foreach (var unmodeled in result.UnmodeledAbilities) output.WriteLine($"unmodeled ability {unmodeled.Key}: {unmodeled.Value}");
        foreach (var unmodeled in result.UnmodeledCustomizations) output.WriteLine($"unmodeled {unmodeled}");
        foreach (var proc in result.ProcStats) output.WriteLine($"proc {proc.Key}: {proc.Procs}/{proc.Rolls} (expected {proc.ExpectedProcs:F1})");
        foreach (var forced in result.ForcedCasts.GroupBy(f => f.Split(':')[0][8..]).OrderByDescending(g => g.Count()).Take(12))
        {
            output.WriteLine($"forced {forced.Count()}: {forced.First()}");
        }
        output.WriteLine("");
        // TARIQ_STEPS writes each step's time, ability, Fury (engine and log), the engine's haste and Spirit (engine and log).
        if (Environment.GetEnvironmentVariable("TARIQ_STEPS") is { } steps)
        {
            File.WriteAllLines(steps, result.Steps.Select(s =>
            {
                var fury = s.Resources.FirstOrDefault(r => r.Resource == "Fury");
                var spirit = s.Resources.FirstOrDefault(r => r.Resource == "Spirit");
                return FormattableString.Invariant($"{s.Time:F3},{s.AbilityName},{fury?.Engine:F1},{fury?.Log:F1},{s.EngineHaste:F3},{spirit?.Engine:F1},{spirit?.Log:F1}");
            }));
        }
        if (Environment.GetEnvironmentVariable("TARIQ_HITS_CSV") is not { } dump) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        File.WriteAllLines(dump, result.AllHits.Select(h => string.Format(inv, "{0:F3},{1},{2},{3:F1},{4},{5},{6},{7:F3},{8},{9:F3}",
            h.EngineTime, h.AbilityId, h.Target == null ? "" : $"{h.Target.Id}.{h.Target.Instance}", h.EngineAmount, h.Crit ? 1 : 0,
            h.Log == null ? "" : h.Log.TotalAmount.ToString("F0", inv), h.Log == null ? "" : h.Log.Time.ToString("F3", inv), h.TickFraction,
            h.State == null ? "" : string.Join(" ", h.State.Hero.Auras.Select(a => a.LogId > 0 ? a.LogId.ToString(inv) : a.Name.Replace(" ", "_"))),
            h.TargetHealthFraction)));
    }
}
