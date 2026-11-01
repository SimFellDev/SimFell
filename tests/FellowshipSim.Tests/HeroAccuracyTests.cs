using System.Text;
using System.Text.Json;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// The engine's accuracy for one hero over every downloaded fight (data/fights) of the patch it models
/// (<see cref="GamePatch.Current"/>): an exact replay and a free-dice run of each of the hero's players, then the totals,
/// the calibration per damage ID, and the most common issues. ACCURACY_HERO names the hero; without it the test does
/// nothing. The result is saved as data/accuracy/&lt;Hero&gt;/&lt;label&gt;.txt and .json, the label from ACCURACY_LABEL
/// (default: the time). With a saved baseline.json beside it, the headline numbers print next to the baseline's.
/// ACCURACY_FREEDICE_ITERATIONS sets the free-dice runs per fight (default 100, 0 for none), on ACCURACY_THREADS threads
/// (default 8).
/// Run with: ACCURACY_HERO=Rime ACCURACY_LABEL=baseline dotnet test -c Release --filter HeroAccuracyTests
/// </summary>
public class HeroAccuracyTests(ITestOutputHelper output)
{
    public sealed record FightAccuracy(
        string ReportCode, int FightId, string FightName, DateTime? FightDate, string PlayerName,
        int LogHits, int MatchedHits, double LogDamage, double ExactDamage, int PassedSteps, int TotalSteps, int ForcedCasts,
        int FreeDiceRuns, double FreeDiceMean, double FreeDiceLogPercentile);

    public sealed record AbilityAccuracy(
        int Id, string Name, int LogHits, int MatchedHits, int EngineOnlyHits, double LogDamage, double ExactDamage,
        int NormalN, double NormalRatio, int CritN, double CritRatio, double FreeDiceLogDamage, double FreeDiceMean);

    public sealed record IssueCount(string Category, string? Subject, int Count, string Example);

    public sealed record HeroAccuracy(
        string Hero, string Label, string Patch, DateTime CreatedAt,
        int Fights, int LogHits, int MatchedHits, double LogDamage, double ExactDamage, double FreeDiceLogDamage, double FreeDiceMean,
        double MeanFreeDiceLogPercentile, IReadOnlyList<FightAccuracy> FightResults, IReadOnlyList<AbilityAccuracy> Abilities,
        IReadOnlyList<IssueCount> Issues, IReadOnlyDictionary<string, int> UnmodeledAbilities, IReadOnlyList<string> UnmodeledCustomizations);

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public async Task Hero_Accuracy_Over_Current_Patch_Fights()
    {
        if (Environment.GetEnvironmentVariable("ACCURACY_HERO") is not { Length: > 0 } hero)
        {
            output.WriteLine("ACCURACY_HERO is not set; nothing to measure.");
            return;
        }
        string label = Environment.GetEnvironmentVariable("ACCURACY_LABEL") ?? DateTime.Now.ToString("yyyyMMdd-HHmmss");
        int iterations = int.TryParse(Environment.GetEnvironmentVariable("ACCURACY_FREEDICE_ITERATIONS"), out int n) ? n : 100;
        int threads = int.TryParse(Environment.GetEnvironmentVariable("ACCURACY_THREADS"), out int t) ? t : 8;
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));

        var fights = new List<FightAccuracy>();
        var results = new List<ReplayResult>();
        var freeDice = new List<FreeDiceReplay.Result>();
        foreach (var (reportCode, fightId, playerId) in library.ListDownloadedFights())
        {
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId, playerId);
            foreach (var player in ReplayScriptBuilder.Players(raw).Where(p => p.Id == playerId && p.Hero.Equals(hero, StringComparison.OrdinalIgnoreCase)))
            {
                var script = ReplayScript.FromJson(ReplayScriptBuilder.Build(raw, player.Id));
                if (script.FightDate is not { } date || !GamePatch.Current.Covers(date, null) || script.Character == null) continue;

                var result = new ReplayDriver(ReplayDriver.CreateHero(script), script, new ReplayOptions { StopOnFirstFailure = false }).Run();
                var (exactDamage, logDamage) = ReplayReportWriter.DamageTotals(result);
                var dice = iterations > 0 ? FreeDiceReplay.Run(script, iterations, maxThreads: threads) : null;
                if (dice != null) freeDice.Add(dice);
                results.Add(result);
                fights.Add(new FightAccuracy(reportCode, fightId, script.FightName, script.FightDate, result.PlayerName,
                    result.LogHits, result.MatchedLogHits, logDamage, exactDamage, result.PassedSteps, result.Steps.Count,
                    result.ForcedCasts.Count, dice?.EngineTotals.Count ?? 0, dice?.Mean ?? 0, dice?.LogPercentile ?? 0));
            }
        }
        if (fights.Count == 0)
        {
            output.WriteLine($"No {hero} fight of patch {GamePatch.Current.Name} in {library.FightsDirectory}; run tools/fetch-fights.cs first.");
            return;
        }

        var accuracy = new HeroAccuracy(hero, label, GamePatch.Current.Name, DateTime.UtcNow,
            fights.Count, fights.Sum(f => f.LogHits), fights.Sum(f => f.MatchedHits), fights.Sum(f => f.LogDamage),
            fights.Sum(f => f.ExactDamage), freeDice.Sum(d => d.LogDamage), freeDice.Sum(d => d.Mean),
            freeDice.Count == 0 ? 0 : freeDice.Average(d => d.LogPercentile),
            fights, Abilities(results, freeDice), Issues(results),
            results.SelectMany(r => r.UnmodeledAbilities).GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Value), StringComparer.OrdinalIgnoreCase),
            results.SelectMany(r => r.UnmodeledCustomizations).Distinct().Order().ToList());

        string folder = Path.Combine(library.DataDirectory, "accuracy", hero);
        Directory.CreateDirectory(folder);
        string baselinePath = Path.Combine(folder, "baseline.json");
        var baseline = label != "baseline" && File.Exists(baselinePath)
            ? JsonSerializer.Deserialize<HeroAccuracy>(File.ReadAllText(baselinePath))
            : null;
        string text = Format(accuracy, baseline);
        File.WriteAllText(Path.Combine(folder, $"{label}.txt"), text);
        File.WriteAllText(Path.Combine(folder, $"{label}.json"), JsonSerializer.Serialize(accuracy, Indented));
        output.WriteLine(text);
        output.WriteLine($"-> {Path.Combine(folder, label)}.txt");
        Assert.True(accuracy.MatchedHits > 0);
    }

    private static List<AbilityAccuracy> Abilities(List<ReplayResult> results, List<FreeDiceReplay.Result> freeDice)
    {
        var log = results.SelectMany(r => r.LogDamage).GroupBy(h => h.AbilityId).ToDictionary(g => g.Key, g => g.ToList());
        var engine = results.SelectMany(r => r.AllHits).GroupBy(h => h.AbilityId).ToDictionary(g => g.Key, g => g.ToList());
        var dice = freeDice.SelectMany(d => d.Abilities).GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.ToList());
        return log.Keys.Union(engine.Keys).Union(dice.Keys)
            .Select(id =>
            {
                var logHits = log.GetValueOrDefault(id) ?? [];
                var engineHits = engine.GetValueOrDefault(id) ?? [];
                var diceRows = dice.GetValueOrDefault(id) ?? [];
                var normal = engineHits.Where(h => h.Ratio.HasValue && !h.Crit).Select(h => h.Ratio!.Value).ToList();
                var crit = engineHits.Where(h => h.Ratio.HasValue && h.Crit).Select(h => h.Ratio!.Value).ToList();
                string name = engineHits.FirstOrDefault()?.AbilityName ?? logHits.FirstOrDefault()?.AbilityName ?? diceRows.FirstOrDefault()?.Name ?? "?";
                return new AbilityAccuracy(id, name, logHits.Count, engineHits.Count(h => h.Log != null && h.LandedTime.HasValue), engineHits.Count(h => h.Log == null),
                    logHits.Sum(h => h.UnblockedAmount), engineHits.Sum(h => h.EngineAmount),
                    normal.Count, normal.Count == 0 ? 0 : normal.Average(), crit.Count, crit.Count == 0 ? 0 : crit.Average(),
                    diceRows.Sum(a => a.LogDamage), diceRows.Sum(a => a.EngineMean));
            })
            .OrderByDescending(a => Math.Max(a.LogDamage, a.ExactDamage))
            .ToList();
    }

    private static List<IssueCount> Issues(List<ReplayResult> results) =>
        results.SelectMany(r => r.Steps.SelectMany(s => s.Issues))
            .GroupBy(i => (i.Category, i.Subject))
            .OrderByDescending(g => g.Count())
            .Take(40)
            .Select(g => new IssueCount(g.Key.Category.ToString(), g.Key.Subject, g.Count(), g.First().Message))
            .ToList();

    private static string Format(HeroAccuracy a, HeroAccuracy? baseline)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{a.Hero} accuracy '{a.Label}', patch {a.Patch}: {a.Fights} fights");
        Headline(sb, "hits matched", a.LogHits == 0 ? 0 : (double)a.MatchedHits / a.LogHits, baseline is { LogHits: > 0 } b1 ? (double)b1.MatchedHits / b1.LogHits : null);
        Headline(sb, "exact replay damage / log", a.LogDamage == 0 ? 0 : a.ExactDamage / a.LogDamage, baseline is { LogDamage: > 0 } b2 ? b2.ExactDamage / b2.LogDamage : null);
        Headline(sb, "free dice mean / log", a.FreeDiceLogDamage == 0 ? 0 : a.FreeDiceMean / a.FreeDiceLogDamage, baseline is { FreeDiceLogDamage: > 0 } b3 ? b3.FreeDiceMean / b3.FreeDiceLogDamage : null);
        Headline(sb, "mean log percentile in free dice", a.MeanFreeDiceLogPercentile, baseline?.MeanFreeDiceLogPercentile);

        sb.AppendLine();
        sb.AppendLine("fight                              player            hits matched  exact/log  dice/log  log pct  clean steps  forced");
        foreach (var f in a.FightResults)
        {
            string fight = $"{f.ReportCode}#{f.FightId} {f.FightName}".PadRight(34)[..34];
            sb.AppendLine($"{fight} {f.PlayerName.PadRight(16)[..16]}  {Ratio(f.MatchedHits, f.LogHits),12:P1}  {Ratio(f.ExactDamage, f.LogDamage),9:P1}  " +
                          $"{Ratio(f.FreeDiceMean, f.LogDamage),8:P1}  {f.FreeDiceLogPercentile,7:P0}  {f.PassedSteps,5}/{f.TotalSteps,-5}  {f.ForcedCasts,6}");
        }

        sb.AppendLine();
        sb.AppendLine("id        ability               log hits  matched  engine-only  log dmg        exact/log  dice/log  normal (n)       crit (n)");
        foreach (var x in a.Abilities)
        {
            sb.AppendLine($"{x.Id,-9} {x.Name.PadRight(20)[..20]}  {x.LogHits,8}  {x.MatchedHits,7}  {x.EngineOnlyHits,11}  {x.LogDamage,13:N0}  " +
                          $"{Ratio(x.ExactDamage, x.LogDamage),9:P1}  {Ratio(x.FreeDiceMean, x.FreeDiceLogDamage),8:P1}  " +
                          $"{(x.NormalN == 0 ? "-" : $"{x.NormalRatio:F3} ({x.NormalN})"),-15}  {(x.CritN == 0 ? "-" : $"{x.CritRatio:F3} ({x.CritN})")}");
        }

        sb.AppendLine();
        sb.AppendLine("most common issues over all fights:");
        foreach (var issue in a.Issues) sb.AppendLine($"{issue.Count,6} [{issue.Category}] {issue.Subject}: {issue.Example}");
        if (a.UnmodeledAbilities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("unmodeled abilities: " + string.Join(", ", a.UnmodeledAbilities.OrderByDescending(u => u.Value).Select(u => $"{u.Key} ({u.Value})")));
        }
        if (a.UnmodeledCustomizations.Count > 0)
        {
            sb.AppendLine("unmodeled customizations: " + string.Join(", ", a.UnmodeledCustomizations));
        }
        return sb.ToString();
    }

    private static void Headline(StringBuilder sb, string name, double value, double? baseline) =>
        sb.AppendLine($"  {name,-34} {value,8:P1}" + (baseline is { } b ? $"   baseline {b,8:P1}   change {value - b:+0.0%;-0.0%;0.0%}" : ""));

    private static double Ratio(double a, double b) => b == 0 ? 0 : a / b;
}
