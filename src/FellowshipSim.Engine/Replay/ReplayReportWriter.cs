using System.Net;
using System.Text;

namespace FellowshipSim.Engine.Replay;

/// <summary>Renders a <see cref="ReplayResult"/> as a self-contained HTML review page and a short text summary.</summary>
public static class ReplayReportWriter
{

    public static string Summary(ReplayResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Replay {r.ReportCode} fight {r.FightId} ({r.FightName}) - {r.PlayerName} ({r.Hero})");
        sb.AppendLine(r.HaltedAt == null
            ? $"Replayed all {r.Steps.Count} steps: {r.PassedSteps} passed, {r.Steps.Count - r.PassedSteps} failed"
            : $"HALTED at step {r.HaltedAt.Index}/{r.TotalSteps} ({r.HaltedAt.AbilityName} @ {r.HaltedAt.Time:F3}s); {r.Steps.Count(s => s.Index < r.HaltedAt.Index)} steps passed before it");
        if (r.ForcedCasts.Count > 0) sb.AppendLine($"  casts forced to follow the log: {r.ForcedCasts.Count}");
        if (r.HaltedAt == null && r.PassedSteps < r.Steps.Count)
        {
            var first = r.Steps.First(s => !s.Passed);
            sb.AppendLine($"  first failing step: {first.Index} ({first.AbilityName} @ {first.Time:F3}s)");
            foreach (var g in r.Steps.SelectMany(s => s.Issues).GroupBy(i => i.Category).OrderByDescending(g => g.Count()))
            {
                sb.AppendLine($"  {g.Key}: {g.Count()} issues in {g.Select(i => i.Step).Distinct().Count()} steps");
                foreach (var sub in g.GroupBy(i => i.Subject ?? "other").OrderByDescending(x => x.Count()).Take(6))
                {
                    sb.AppendLine($"      {sub.Count(),6} x {sub.Key} (first at step {sub.Min(i => i.Step)})");
                }
            }
        }
        if (r.AllHits.Count > 0)
        {
            sb.AppendLine("  calibration (mean log/engine over matched hits; spread averages out to 1.000):");
            foreach (var row in Calibration(r))
            {
                sb.AppendLine($"    {row.Name,-26} {row.Id,8}  normal {Fmt(row.Normal)}  crit {Fmt(row.Crit)}");
            }
        }
        foreach (var i in r.HaltIssues) sb.AppendLine($"  {i}");
        sb.AppendLine($"  log hits matched: {r.MatchedLogHits}/{r.LogHits}");
        sb.AppendLine("  open item: Amethyst Splinter (1000101) damage not checked yet (hits still must match)");
        return sb.ToString();
    }

    public static void Write(ReplayResult r, string path, string? navigation = null) => File.WriteAllText(path, Render(r, navigation));

    /// <summary>Engine and log damage totals of a replay (engine: hits that landed).</summary>
    public static (double Engine, double Log) DamageTotals(ReplayResult r) =>
        (r.AllHits.Where(h => h.LandedTime.HasValue).Sum(h => h.EngineAmount), r.LogDamage.Sum(d => d.TotalAmount));

    /// <summary>An index page linking the review page of every replayed fight, with headline numbers.</summary>
    public static string RenderIndex(IReadOnlyList<(ReplayResult Result, string File)> fights)
    {
        var sb = new StringBuilder();
        sb.Append(Head("Replay Reviews"));
        sb.Append("<h1>Replay reviews</h1><div class=\"muted\">Every fight replayed through the engine (survey mode: the whole fight, casts follow the log). Click a fight for its full review.</div>");
        sb.Append("<div class=\"panel scroll\"><table><thead><tr><th>Fight</th><th>Player</th><th>Report</th><th class=\"num\">Steps passed</th><th class=\"num\">Log hits matched</th><th class=\"num\">Log DPS</th><th class=\"num\">Engine DPS</th><th class=\"num\">Engine vs log</th><th class=\"num\">Forced casts</th></tr></thead><tbody>");
        foreach (var (r, file) in fights)
        {
            var (eng, log) = DamageTotals(r);
            double d = log > 0 ? eng / log - 1 : 0;
            double dur = Math.Max(1, r.DurationSeconds);
            string cls = Math.Abs(d) <= 0.03 ? "diff-ok" : "diff-bad";
            sb.Append($"<tr><td><a href=\"{E(file)}\">{E(r.FightName)}</a></td><td>{E(r.PlayerName)}</td><td>{E(r.ReportCode)} #{r.FightId}</td>");
            sb.Append($"<td class=\"num\">{r.PassedSteps} / {r.Steps.Count}</td><td class=\"num\">{(r.LogHits > 0 ? (double)r.MatchedLogHits / r.LogHits : 0):P1}</td>");
            sb.Append($"<td class=\"num\">{log / dur:N0}</td><td class=\"num\">{eng / dur:N0}</td><td class=\"num {cls}\">{d:+0.0%;-0.0%;0.0%}</td><td class=\"num\">{r.ForcedCasts.Count}</td></tr>");
        }
        sb.Append("</tbody></table></div></main></body></html>");
        return sb.ToString();
    }

    public sealed record CalibrationRow(string Name, int Id, (int N, double Mean) Normal, (int N, double Mean) Crit);

    /// <summary>Per damage ID: mean log/engine ratio of matched hits, separately for normal hits and crits.</summary>
    public static IEnumerable<CalibrationRow> Calibration(ReplayResult r) =>
        r.AllHits.Where(h => h.Ratio.HasValue)
            .GroupBy(h => h.AbilityId)
            .Select(g => new CalibrationRow(g.First().AbilityName, g.Key, Mean(g.Where(h => !h.Crit)), Mean(g.Where(h => h.Crit))))
            .OrderByDescending(row => row.Normal.N + row.Crit.N);

    private static (int, double) Mean(IEnumerable<HitPair> hits)
    {
        var ratios = hits.Select(h => h.Ratio!.Value).ToList();
        return (ratios.Count, ratios.Count == 0 ? 0 : ratios.Average());
    }

    private static string Fmt((int N, double Mean) v) => v.N == 0 ? "     -      " : $"{v.Mean:F3} (n={v.N,-4})";

    public static string Render(ReplayResult r, string? navigation = null)
    {
        var sb = new StringBuilder();
        sb.Append(Head("Replay Parity Review"));
        if (navigation != null) sb.Append(navigation);
        return RenderBody(sb, r);
    }

    internal static string Head(string title)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Replay Parity Review</title>
<style>
:root{color-scheme:light dark;--bg:#f7f7f5;--panel:#fff;--text:#1d1d1b;--muted:#6b6b66;--line:#e2e1dc;--ok:#1f7a4d;--ok-bg:#e5f3ec;--bad:#b42318;--bad-bg:#fdecea;--warn:#9a6700;--warn-bg:#fff4d6;--accent:#3b5bdb}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#151514;--panel:#1e1e1c;--text:#ecebe6;--muted:#a3a29b;--line:#34332f;--ok:#5cc58f;--ok-bg:#16301f;--bad:#ff8a80;--bad-bg:#3a1a17;--warn:#f2c14e;--warn-bg:#3a3014;--accent:#8ea2ff}}
:root[data-theme="dark"]{--bg:#151514;--panel:#1e1e1c;--text:#ecebe6;--muted:#a3a29b;--line:#34332f;--ok:#5cc58f;--ok-bg:#16301f;--bad:#ff8a80;--bad-bg:#3a1a17;--warn:#f2c14e;--warn-bg:#3a3014;--accent:#8ea2ff}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.45 system-ui,-apple-system,Segoe UI,sans-serif}
main{max-width:1180px;margin:0 auto;padding:24px 16px 64px}h1{font-size:22px;margin:0 0 4px}h2{font-size:16px;margin:28px 0 10px}
.muted{color:var(--muted)}.panel{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:14px 16px;margin:12px 0}
.halt{border-color:var(--bad);background:var(--bad-bg)}.done{border-color:var(--ok);background:var(--ok-bg)}
.tag{display:inline-block;font-size:11px;font-weight:600;padding:1px 7px;border-radius:999px;background:var(--line);margin-right:6px}
.tag.bad{background:var(--bad);color:var(--panel)}.ok{color:var(--ok)}.bad{color:var(--bad)}.warn{color:var(--warn)}
.scroll{overflow-x:auto}table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums}th,td{text-align:left;padding:4px 8px;border-bottom:1px solid var(--line);white-space:nowrap}
th{font-size:12px;color:var(--muted);font-weight:600}td.num,th.num{text-align:right}tr.fail td{background:var(--bad-bg)}tr.skip td{color:var(--muted)}
details{margin:8px 0}summary{cursor:pointer;font-weight:600}ul{margin:6px 0;padding-left:20px}code{font-size:12px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px}
:root{--engine:#3b5bdb;--log:#d9480f}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--engine:#8ea2ff;--log:#ff922b}}
:root[data-theme="dark"]{--engine:#8ea2ff;--log:#ff922b}
.chart{width:100%;height:auto;display:block}.chart text{fill:var(--muted);font-size:11px}.chart .axis{stroke:var(--line)}
.legend{display:flex;gap:16px;font-size:12px;color:var(--muted);margin-bottom:4px}.legend i{display:inline-block;width:14px;height:3px;vertical-align:middle;margin-right:6px}
td.diff-bad{color:var(--bad)}td.diff-ok{color:var(--ok)}tr.total td{font-weight:600;border-top:2px solid var(--line)}
.fightnav{display:flex;flex-wrap:wrap;gap:6px;margin:0 0 14px}.fightnav a{font-size:12px;padding:3px 9px;border:1px solid var(--line);border-radius:999px;color:var(--text);text-decoration:none;background:var(--panel)}
.fightnav a.cur{background:var(--accent);border-color:var(--accent);color:var(--panel)}a{color:var(--accent)}
</style></head><body><main>
""");
        return sb.ToString().Replace("<title>Replay Parity Review</title>", $"<title>{title}</title>");
    }

    private static string RenderBody(StringBuilder sb, ReplayResult r)
    {
        sb.Append($"<h1>Replay parity review</h1><div class=\"muted\">{E(r.FightName)} · report {E(r.ReportCode)} fight {r.FightId} · {E(r.PlayerName)} ({E(r.Hero)})</div>");

        if (r.HaltedAt != null)
        {
            sb.Append($"<div class=\"panel halt\"><strong>Halted at step {r.HaltedAt.Index} of {r.TotalSteps}</strong> — {E(r.HaltedAt.AbilityName)} at {r.HaltedAt.Time:F3}s. ");
            sb.Append($"{r.Steps.Count(s => s.Index < r.HaltedAt.Index)} steps passed before it.<ul>");
            foreach (var i in r.HaltIssues) sb.Append($"<li><span class=\"tag bad\">{i.Category}</span>step {i.Step} @ {i.Time:F3}s — {E(i.Message)}</li>");
            sb.Append("</ul></div>");
        }
        else
        {
            string cls = r.Completed ? "done" : "halt";
            sb.Append($"<div class=\"panel {cls}\"><strong>Replayed all {r.Steps.Count} steps</strong> — {r.PassedSteps} passed, {r.Steps.Count - r.PassedSteps} failed.</div>");
        }

        sb.Append($"<div class=\"muted\">Log hits matched by the engine: {r.MatchedLogHits:N0} of {r.LogHits:N0}</div>");
        if (r.ForcedCasts.Count > 0)
        {
            sb.Append($"<details class=\"panel\"><summary>{r.ForcedCasts.Count} casts forced to follow the log (the engine would have refused them)</summary><ul>");
            foreach (var f in r.ForcedCasts) sb.Append($"<li>{E(f)}</li>");
            sb.Append("</ul></details>");
        }
        sb.Append("<div class=\"panel\"><span class=\"tag\">Open item</span>Amethyst Splinter (1000101) damage is not checked yet; its hits must still match.</div>");
        RenderDamageOverview(sb, r);

        // Every step with issues, collapsed (the halted step open); the "All steps" table links to them.
        var failing = r.Steps.Where(s => !s.Passed).ToList();
        if (failing.Count > 0)
        {
            var byCategory = failing.SelectMany(s => s.Issues).GroupBy(i => i.Category).OrderByDescending(g => g.Count());
            sb.Append($"<h2>Steps with issues ({failing.Count})</h2><div class=\"muted\">");
            sb.Append(string.Join(" · ", byCategory.Select(g => $"{g.Key}: {g.Count()}")));
            sb.Append("</div>");
            foreach (var s in failing) RenderStepDetail(sb, s, open: s.Index == r.HaltedAt?.Index);
        }

        sb.Append("<h2>All steps</h2><div class=\"panel scroll\"><table><thead><tr><th class=\"num\">#</th><th class=\"num\">Time</th><th>Action</th><th>Ability</th><th class=\"num\">Enemies</th><th class=\"num\">Hits</th><th class=\"num\">Log dmg / engine</th><th>State</th><th>Result</th></tr></thead><tbody>");
        foreach (var s in r.Steps)
        {
            var matched = s.Hits.Where(h => h.Log != null).ToList();
            double engineSum = matched.Sum(h => h.EngineAmount), logSum = matched.Sum(h => h.Log!.UnblockedAmount);
            bool stateOk = s.Resources.All(c => c.Matches) && s.Auras.All(a => a.Matches);
            string cls = !s.Passed ? "fail" : !s.Modeled ? "skip" : "";
            string index = s.Passed ? s.Index.ToString() : $"<a href=\"#step-{s.Index}\">{s.Index}</a>";
            sb.Append($"<tr class=\"{cls}\"><td class=\"num\">{index}</td><td class=\"num\">{s.Time:F3}</td><td>{E(s.Kind)}</td><td>{E(s.AbilityName)}</td><td class=\"num\">{s.ActiveEnemies}</td>");
            sb.Append($"<td class=\"num\">{matched.Count}/{s.Hits.Count}</td><td class=\"num\">{(engineSum > 0 ? (logSum / engineSum).ToString("F3") : "—")}</td>");
            sb.Append($"<td>{(stateOk ? "<span class=ok>match</span>" : "<span class=bad>differs</span>")}</td>");
            sb.Append($"<td>{(!s.Modeled ? "skipped (no model)" : s.Passed ? "<span class=ok>pass</span>" : $"<span class=bad>{s.Issues.Count} issue(s)</span>")}</td></tr>");
        }
        sb.Append("</tbody></table></div>");

        sb.Append("<h2>Coverage</h2><div class=\"grid\">");
        RenderCounts(sb, "Abilities the engine does not model (skipped)", r.UnmodeledAbilities, "casts");
        RenderCounts(sb, "Player auras in the log with no engine model (not checked)", r.UntrackedLogAuras, "steps active");
        RenderCounts(sb, "Auras from other players on the player (no engine effect)", r.UnmodeledExternalAuras, "applications");
        sb.Append("</div>");

        if (r.ProcStats.Count > 0)
        {
            sb.Append("<h2>Proc rolls</h2><div class=\"panel scroll\"><table><thead><tr><th>Proc</th><th class=\"num\">Rolls</th><th class=\"num\">Expected procs</th><th class=\"num\">Log procs</th></tr></thead><tbody>");
            foreach (var p in r.ProcStats) sb.Append($"<tr><td>{E(p.Key)}</td><td class=\"num\">{p.Rolls}</td><td class=\"num\">{p.ExpectedProcs:F1}</td><td class=\"num\">{p.Procs}</td></tr>");
            sb.Append("</tbody></table><div class=\"muted\">Expected procs is the sum of the engine's chances; a large gap to the log count means the proc chance or its trigger is wrong.</div></div>");
        }

        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private const double DpsWindowSeconds = 10.0;

    /// <summary>Damage over time and DPS charts (engine vs log), then a per-ability breakdown.</summary>
    private static void RenderDamageOverview(StringBuilder sb, ReplayResult r)
    {
        var engineHits = r.AllHits.Where(h => h.LandedTime.HasValue).ToList();
        double duration = Math.Max(r.DurationSeconds,
            Math.Max(r.LogDamage.Select(d => d.Time).DefaultIfEmpty(0).Max(), engineHits.Select(h => h.LandedTime!.Value).DefaultIfEmpty(0).Max()));
        if (duration <= 0) return;
        int bins = (int)Math.Ceiling(duration);

        double[] Bin(IEnumerable<(double T, double Amount)> hits)
        {
            var b = new double[bins];
            foreach (var (t, a) in hits) b[Math.Clamp((int)t, 0, bins - 1)] += a;
            return b;
        }
        var logBins = Bin(r.LogDamage.Select(d => (d.Time, d.TotalAmount)));
        var engBins = Bin(engineHits.Select(h => (h.LandedTime!.Value, h.EngineAmount)));

        double[] Cumulative(double[] b)
        {
            var c = new double[b.Length];
            double sum = 0;
            for (int i = 0; i < b.Length; i++) c[i] = sum += b[i];
            return c;
        }
        double[] Rolling(double[] b)
        {
            int w = (int)DpsWindowSeconds;
            var d = new double[b.Length];
            double sum = 0;
            for (int i = 0; i < b.Length; i++)
            {
                sum += b[i];
                if (i >= w) sum -= b[i - w];
                d[i] = sum / Math.Min(w, i + 1);
            }
            return d;
        }

        double logTotal = logBins.Sum(), engTotal = engBins.Sum();
        sb.Append("<h2>Damage overview</h2>");
        sb.Append($"<div class=\"grid\"><div class=\"panel\"><div class=\"muted\">Log damage</div><strong>{logTotal:N0}</strong> · {logTotal / duration:N0} DPS</div>");
        sb.Append($"<div class=\"panel\"><div class=\"muted\">Engine damage (matched and unmatched hits)</div><strong>{engTotal:N0}</strong> · {engTotal / duration:N0} DPS</div>");
        sb.Append($"<div class=\"panel\"><div class=\"muted\">Engine vs log</div><strong>{(logTotal > 0 ? engTotal / logTotal - 1 : 0):+0.0%;-0.0%;0.0%}</strong> over {duration:F0}s</div></div>");

        sb.Append("<div class=\"panel\"><div class=\"legend\"><span><i style=\"background:var(--log)\"></i>Log</span><span><i style=\"background:var(--engine)\"></i>Engine</span></div>");
        sb.Append("<strong>Damage done over time</strong>");
        Chart(sb, Cumulative(logBins), Cumulative(engBins), duration);
        sb.Append($"<strong>DPS over time</strong> <span class=\"muted\">({DpsWindowSeconds:F0}s rolling average)</span>");
        Chart(sb, Rolling(logBins), Rolling(engBins), duration);
        sb.Append("</div>");

        RenderAbilityBreakdown(sb, r, engineHits, duration);
    }

    private static void Chart(StringBuilder sb, double[] log, double[] engine, double duration)
    {
        const double W = 1100, H = 240, L = 64, R = 12, T = 10, B = 26;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double max = Math.Max(log.DefaultIfEmpty(0).Max(), engine.DefaultIfEmpty(0).Max());
        if (max <= 0) max = 1;
        double X(double t) => L + (W - L - R) * t / Math.Max(1, log.Length - 1);
        double Y(double v) => T + (H - T - B) * (1 - v / max);
        string F(double v) => v.ToString("F1", inv);
        string Points(double[] v) => string.Join(" ", v.Select((y, i) => $"{F(X(i))},{F(Y(y))}"));

        sb.Append($"<svg class=\"chart\" viewBox=\"0 0 {W} {H}\" role=\"img\">");
        for (int g = 0; g <= 4; g++)
        {
            double v = max * g / 4;
            sb.Append($"<line class=\"axis\" x1=\"{L}\" x2=\"{W - R}\" y1=\"{F(Y(v))}\" y2=\"{F(Y(v))}\"/>");
            sb.Append($"<text x=\"{L - 6}\" y=\"{F(Y(v) + 4)}\" text-anchor=\"end\">{Short(v)}</text>");
        }
        for (int t = 0; t <= duration; t += 60)
        {
            sb.Append($"<text x=\"{F(X(t))}\" y=\"{H - 6}\" text-anchor=\"middle\">{t / 60}:00</text>");
        }
        sb.Append($"<polyline fill=\"none\" stroke=\"var(--log)\" stroke-width=\"1.6\" points=\"{Points(log)}\"/>");
        sb.Append($"<polyline fill=\"none\" stroke=\"var(--engine)\" stroke-width=\"1.6\" points=\"{Points(engine)}\"/>");
        sb.Append("</svg>");
    }

    private static string Short(double v) => v >= 1e6 ? $"{v / 1e6:0.#}M" : v >= 1e3 ? $"{v / 1e3:0.#}k" : $"{v:0}";

    private static void RenderAbilityBreakdown(StringBuilder sb, ReplayResult r, List<HitPair> engineHits, double duration)
    {
        var ids = r.LogDamage.Select(d => d.AbilityId).Union(engineHits.Select(h => h.AbilityId)).Distinct();
        var rows = ids.Select(id =>
        {
            var lh = r.LogDamage.Where(d => d.AbilityId == id).ToList();
            var eh = engineHits.Where(h => h.AbilityId == id).ToList();
            string name = lh.FirstOrDefault()?.AbilityName ?? eh.First().AbilityName;
            return (Id: id, Name: name,
                LogDmg: lh.Sum(d => d.TotalAmount), EngDmg: eh.Sum(h => h.EngineAmount),
                LogHits: lh.Count, EngHits: eh.Count,
                LogCrits: lh.Count(d => d.Crit), EngCrits: eh.Count(h => h.Crit),
                LogCasts: r.LogCasts.GetValueOrDefault(name), EngCasts: r.EngineCasts.GetValueOrDefault(name));
        }).OrderByDescending(x => x.LogDmg).ToList();
        double logTotal = rows.Sum(x => x.LogDmg), engTotal = rows.Sum(x => x.EngDmg);

        string Diff(double eng, double log)
        {
            if (log <= 0) return "<td class=\"num\">-</td>";
            double d = eng / log - 1;
            return $"<td class=\"num {(Math.Abs(d) <= 0.05 ? "diff-ok" : "diff-bad")}\">{(Math.Abs(d) < 0.0005 ? 0 : d):+0.0%;-0.0%;0.0%}</td>";
        }
        string Pct(int n, int of) => of == 0 ? "-" : $"{(double)n / of:P1}";
        string Avg(double dmg, int n) => n == 0 ? "-" : $"{dmg / n:N0}";
        string Casts(int n) => n == 0 ? "-" : n.ToString();

        sb.Append("<h2>Damage by ability</h2><div class=\"panel scroll\"><table><thead><tr>");
        sb.Append("<th>Ability</th><th class=\"num\">ID</th><th class=\"num\">Log damage</th><th class=\"num\">Engine damage</th><th class=\"num\">Diff</th><th class=\"num\">Share</th>");
        sb.Append("<th class=\"num\">Log DPS</th><th class=\"num\">Engine DPS</th><th class=\"num\">Log hits</th><th class=\"num\">Engine hits</th>");
        sb.Append("<th class=\"num\">Log crit</th><th class=\"num\">Engine crit</th><th class=\"num\">Log avg hit</th><th class=\"num\">Engine avg hit</th>");
        sb.Append("<th class=\"num\">Log casts</th><th class=\"num\">Engine casts</th></tr></thead><tbody>");
        foreach (var x in rows)
        {
            sb.Append($"<tr><td>{E(x.Name)}</td><td class=\"num\">{x.Id}</td><td class=\"num\">{x.LogDmg:N0}</td><td class=\"num\">{x.EngDmg:N0}</td>{Diff(x.EngDmg, x.LogDmg)}");
            sb.Append($"<td class=\"num\">{(logTotal > 0 ? x.LogDmg / logTotal : 0):P1}</td><td class=\"num\">{x.LogDmg / duration:N0}</td><td class=\"num\">{x.EngDmg / duration:N0}</td>");
            sb.Append($"<td class=\"num\">{x.LogHits:N0}</td><td class=\"num\">{x.EngHits:N0}</td><td class=\"num\">{Pct(x.LogCrits, x.LogHits)}</td><td class=\"num\">{Pct(x.EngCrits, x.EngHits)}</td>");
            sb.Append($"<td class=\"num\">{Avg(x.LogDmg, x.LogHits)}</td><td class=\"num\">{Avg(x.EngDmg, x.EngHits)}</td><td class=\"num\">{Casts(x.LogCasts)}</td><td class=\"num\">{Casts(x.EngCasts)}</td></tr>");
        }
        sb.Append($"<tr class=\"total\"><td>Total</td><td></td><td class=\"num\">{logTotal:N0}</td><td class=\"num\">{engTotal:N0}</td>{Diff(engTotal, logTotal)}<td class=\"num\">100%</td>");
        sb.Append($"<td class=\"num\">{logTotal / duration:N0}</td><td class=\"num\">{engTotal / duration:N0}</td><td class=\"num\">{rows.Sum(x => x.LogHits):N0}</td><td class=\"num\">{rows.Sum(x => x.EngHits):N0}</td>");
        sb.Append($"<td class=\"num\">{Pct(rows.Sum(x => x.LogCrits), rows.Sum(x => x.LogHits))}</td><td class=\"num\">{Pct(rows.Sum(x => x.EngCrits), rows.Sum(x => x.EngHits))}</td><td></td><td></td><td></td><td></td></tr>");
        sb.Append("</tbody></table></div>");
        sb.Append("<div class=\"muted\">Engine crits follow the log for matched hits (the replay takes crits from the log); engine damage is before the ±10% spread.</div>");
    }

    private static void RenderStepDetail(StringBuilder sb, ReplayStep s, bool open)
    {
        string status = !s.Modeled ? "skipped" : s.Passed ? "<span class=ok>pass</span>" : $"<span class=bad>{s.Issues.Count} issue(s)</span>";
        sb.Append($"<details class=\"panel\" id=\"step-{s.Index}\"{(open ? " open" : "")}><summary>Step {s.Index} · {s.Time:F3}s · {E(s.Kind)} {E(s.AbilityName)}{(s.Target != null ? $" → {E(s.Target)}" : "")} · {status}</summary>");
        if (s.Note != null) sb.Append($"<p class=\"muted\">{E(s.Note)}</p>");
        if (s.Issues.Count > 0)
        {
            sb.Append("<ul>");
            foreach (var i in s.Issues) sb.Append($"<li><span class=\"tag bad\">{i.Category}</span>@ {i.Time:F3}s — {E(i.Message)}</li>");
            sb.Append("</ul>");
        }

        sb.Append("<div class=\"grid\">");
        if (s.Resources.Count > 0)
        {
            sb.Append("<div><strong>Pre-cast resources</strong><table><tr><th>Resource</th><th class=num>Engine</th><th class=num>Log</th></tr>");
            foreach (var c in s.Resources) sb.Append($"<tr><td>{E(c.Resource)}</td><td class=num>{c.Engine:0.##}</td><td class=\"num {(c.Matches ? "ok" : "bad")}\">{c.Log:0.##}</td></tr>");
            sb.Append("</table></div>");
        }
        if (s.Auras.Count > 0)
        {
            sb.Append("<div><strong>Pre-cast tracked auras (stacks)</strong><table><tr><th>Aura</th><th class=num>Engine</th><th class=num>Log</th></tr>");
            foreach (var a in s.Auras) sb.Append($"<tr><td>{E(a.Aura)}</td><td class=num>{a.EngineStacks}</td><td class=\"num {(a.Matches ? "ok" : "bad")}\">{a.LogStacks}</td></tr>");
            sb.Append("</table></div>");
        }
        sb.Append("<div><strong>Context</strong><div class=\"muted\">");
        sb.Append($"Active enemies: {s.ActiveEnemies}<br>");
        sb.Append($"Engine haste {s.EngineHaste:P2}{(s.HasteSources.Count > 0 ? " from " + E(string.Join("; ", s.HasteSources)) : "")}<br>");
        if (s.UntrackedLogAuras.Count > 0) sb.Append($"Untracked log auras: {E(string.Join(", ", s.UntrackedLogAuras))}<br>");
        if (s.ExternalAurasOnPlayer.Count > 0) sb.Append($"Auras from others: {E(string.Join(", ", s.ExternalAurasOnPlayer))}");
        sb.Append("</div></div></div>");

        if (s.Hits.Count > 0)
        {
            sb.Append("<div class=\"scroll\"><table><thead><tr><th class=num>Engine t</th><th class=num>Log t</th><th>Ability</th><th>Target</th><th>Crit</th><th class=num>Engine</th><th class=num>Log</th><th class=num>Ratio</th></tr></thead><tbody>");
            foreach (var h in s.Hits)
            {
                bool bad = h.Log == null || h.Ratio is < 0.9 or > 1.1;
                sb.Append($"<tr class=\"{(bad ? "fail" : "")}\"><td class=num>{h.EngineTime:F3}</td><td class=num>{(h.Log != null ? h.Log.Time.ToString("F3") : "—")}</td><td>{E(h.AbilityName)}</td><td>{E(h.Target?.ToString() ?? "")}</td>");
                sb.Append($"<td>{(h.Log == null ? $"({h.CritChance:P0})" : h.Crit ? "crit" : "")}</td><td class=num>{h.EngineAmount:F0}</td><td class=num>{(h.Log != null ? E(h.Log.ComparedAmountText()) : "missing")}</td><td class=num>{(h.Ratio.HasValue ? h.Ratio.Value.ToString("F3") : "—")}</td></tr>");
            }
            sb.Append("</tbody></table></div>");
        }
        sb.Append("</details>");
    }

    private static void RenderCounts(StringBuilder sb, string title, Dictionary<string, int> counts, string unit)
    {
        sb.Append($"<div class=\"panel\"><strong>{E(title)}</strong>");
        if (counts.Count == 0)
        {
            sb.Append("<div class=\"muted\">None</div></div>");
            return;
        }
        sb.Append("<ul>");
        foreach (var (k, v) in counts.OrderByDescending(kv => kv.Value)) sb.Append($"<li>{E(k)} <span class=\"muted\">({v} {unit})</span></li>");
        sb.Append("</ul></div>");
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);
}
