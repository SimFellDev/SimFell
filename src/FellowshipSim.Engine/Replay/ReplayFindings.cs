using static System.FormattableString;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Reads a <see cref="ReplayResult"/> as findings: one per damage ID, resource, aura, ability or proc, in the order of work
/// of docs/REPLAY_ACCURACY.md and ranked within each group by the damage at stake. Grouping changes no check: every issue
/// of the result is in exactly one finding.
/// </summary>
public static class ReplayFindings
{
    /// <summary>Calibration target: mean log/engine within this of 1 (docs/REPLAY_ACCURACY.md).</summary>
    public const double CalibrationTolerance = 0.02;
    /// <summary>The calibration and hit count targets apply to a damage ID over this share of the log's damage.</summary>
    public const double TargetDamageShare = 0.01;
    /// <summary>Hit count target: engine-only and missing hits, each at most this share of the ID's log hits.</summary>
    public const double HitCountTolerance = 0.02;

    /// <summary>A mean this many standard errors from 1 is off; closer is the spread of a small sample.</summary>
    private const double CalibrationStandardErrors = 3.0;
    /// <summary>Ratios this many times wider than the damage spread have a cause of their own, whatever their mean.</summary>
    private const double ScatterFactor = 1.5;
    /// <summary>A split's means differ by at least this, and by this many standard errors: a review tries every aura on every ID.</summary>
    private const double SplitMinimumDifference = 0.02;
    private const double SplitStandardErrors = 5.0;
    private const double SplitMinimumShare = 0.03;
    private const int SplitMinimumHits = 8;
    private const int SplitsShown = 3;
    /// <summary>A hit out of range counts as mispaired when this share of them fit another engine hit of their release.</summary>
    private const double PairingShare = 0.4;
    /// <summary>The share of an ID's matched hits out of range before pairing is in question.</summary>
    private const double PairingOutOfRangeShare = 0.1;
    /// <summary>
    /// A swap between hits of one release keeps the release's total, so its ratios' scatter falls by at least this factor
    /// in the totals. A wrong multiplier moves the totals with the hits.
    /// </summary>
    private const double PairingScatterReduction = 3.0;
    private const int PairingMinimumReleases = 8;
    /// <summary>Two state differences on adjacent steps cancel when their sum is within this share of the larger.</summary>
    private const double CancellingShare = 0.25;
    private const int DriftAbilitiesShown = 3;

    public static IReadOnlyList<Finding> From(ReplayResult r)
    {
        var issues = IssuesOf(r);
        var byEntity = issues.GroupBy(i => i.Entity.Key).ToDictionary(g => g.Key, g => g.ToList());
        var hitsById = r.AllHits.ToLookup(h => h.AbilityId);
        var logById = r.LogDamage.ToLookup(d => d.AbilityId);
        double logTotal = r.LogDamage.Sum(d => d.UnblockedAmount);

        var findings = new List<Finding>();
        foreach (int id in hitsById.Select(g => g.Key).Union(logById.Select(g => g.Key)))
        {
            var entity = IssueEntity.Damage(id, hitsById[id].FirstOrDefault()?.AbilityName ?? logById[id].First().AbilityName);
            byEntity.Remove(entity.Key, out var own);
            if (Damage(entity, hitsById[id].ToList(), logById[id].ToList(), own ?? [], logTotal, r.Options) is { } finding) findings.Add(finding);
        }
        foreach (var own in byEntity.Values)
        {
            var entity = own[0].Entity;
            findings.Add(entity.Kind switch
            {
                IssueEntityKind.DamageId => Damage(entity, [], [], own, logTotal, r.Options)!,
                IssueEntityKind.Resource => Drift(entity, FindingGroup.Resources, own, r, r.Steps.Sum(s => s.Resources.Count(c => c.Resource == entity.Name))),
                IssueEntityKind.Aura => Drift(entity, FindingGroup.Auras, own, r, r.Steps.Sum(s => s.Auras.Count(a => a.Aura == entity.Name))),
                IssueEntityKind.Proc => Proc(entity, own, r),
                _ => Plain(entity, FindingGroup.CastsAndCooldowns, own),
            });
        }
        return findings.OrderBy(f => f.Group).ThenByDescending(f => f.Impact ?? 0).ThenByDescending(f => f.Issues.Count).ToList();
    }

    /// <summary>The issues of a replay: those reported before its first step too.</summary>
    public static IReadOnlyList<ReplayIssue> IssuesOf(ReplayResult r) => r.Issues.Count > 0 ? r.Issues : r.Steps.SelectMany(s => s.Issues).ToList();



    // ------------------------------------------------------------------ damage IDs

    private static Finding? Damage(IssueEntity entity, List<HitPair> hits, List<LogDamage> logHits, List<ReplayIssue> issues, double logTotal, ReplayOptions options)
    {
        var matched = hits.Where(h => h.Ratio.HasValue).ToList();
        var normal = matched.Where(h => !h.Crit).ToList();
        var crit = matched.Where(h => h.Crit).ToList();
        var engineOnly = hits.Where(h => h.Log == null).ToList();
        var missing = hits.Where(h => h.Log != null && !h.Ratio.HasValue).ToList();
        var outOfRange = matched.Where(h => !h.WithinRange).ToList();

        double logDamage = logHits.Sum(d => d.UnblockedAmount);
        double share = logTotal > 0 ? logDamage / logTotal : 0;
        double calibrationImpact = (Stake(normal) + Stake(crit)) / Math.Max(1.0, logTotal);
        double countImpact = (missing.Sum(h => h.Log!.UnblockedAmount) + engineOnly.Sum(h => h.EngineAmount)) / Math.Max(1.0, logTotal);

        bool inScope = share >= TargetDamageShare;
        double ratioDeviation = StandardDeviation(matched.Select(h => h.Ratio!.Value).ToList());
        double spreadDeviation = options.DamageSpread / Math.Sqrt(3.0);
        bool scattered = ratioDeviation > spreadDeviation * ScatterFactor && outOfRange.Count > matched.Count * HitCountTolerance;
        bool normalOff = inScope && IsOff(normal), critOff = inScope && IsOff(crit);
        bool calibrationOff = normalOff || critOff || inScope && scattered;
        bool engineOnlyOff = inScope && engineOnly.Count > logHits.Count * HitCountTolerance;
        bool missingOff = inScope && missing.Count > logHits.Count * HitCountTolerance;
        bool countsOff = engineOnlyOff || missingOff;
        var ticks = issues.Where(i => i.Category == IssueCategory.Ticks).ToList();
        if (issues.Count == 0 && !calibrationOff && !countsOff) return null;

        int fits = ReleaseFits(outOfRange, hits, options);
        var totals = ReleaseTotals(matched);
        double totalsMean = totals.Count > 0 ? totals.Average() : 0, totalsDeviation = StandardDeviation(totals);
        bool mispaired = inScope && outOfRange.Count > matched.Count * PairingOutOfRangeShare && fits >= outOfRange.Count * PairingShare
            && totals.Count >= PairingMinimumReleases && Math.Abs(1.0 - totalsMean) <= CalibrationTolerance
            && totalsDeviation * PairingScatterReduction <= ratioDeviation;
        var group =
            matched.Count == 0 && missing.Count > 0 ? FindingGroup.UnmodeledContent
            : mispaired ? FindingGroup.ReplayPairing
            : calibrationOff && (!countsOff || calibrationImpact >= countImpact) ? FindingGroup.Calibration
            : countsOff ? FindingGroup.HitCounts
            : ticks.Count > 0 ? FindingGroup.Ticks
            : FindingGroup.WithinTargets;

        string name = hits.FirstOrDefault()?.AbilityName ?? logHits.FirstOrDefault()?.AbilityName ?? entity.Name;
        var headline = new List<string> { Invariant($"{share:0.0%} of log damage") };
        if (normal.Count > 0) headline.Add(Invariant($"normal {normal.Average(h => h.Ratio!.Value):F3} (n={normal.Count:N0})"));
        if (crit.Count > 0) headline.Add(Invariant($"crit {crit.Average(h => h.Ratio!.Value):F3} (n={crit.Count:N0})"));
        if (outOfRange.Count > 0) headline.Add(Invariant($"{outOfRange.Count:N0} of {matched.Count:N0} hits out of range"));
        if (engineOnly.Count > 0) headline.Add(Invariant($"{engineOnly.Count:N0} engine-only hits"));
        if (missing.Count > 0) headline.Add(Invariant($"{missing.Count:N0} missing hits"));
        if (ticks.Count > 0) headline.Add(Invariant($"{ticks.Count:N0} tick issues"));

        int engineOnlyGroups = Events(engineOnly.Select(h => h.EngineTime), options.GroupSecondsOf(entity.Id));
        int missingGroups = Events(missing.Select(h => h.Log!.Time), options.GroupSecondsOf(entity.Id));
        var tickOffsets = Ticks(ticks);
        var lags = matched.Select(h => h.Log!.Time - h.EngineTime).Order().ToList();
        double lagMedian = Percentile(lags, 0.5), lag90 = Percentile(lags, 0.9);

        var facts = new List<string>();
        if (matched.Count > 1)
        {
            facts.Add(Invariant($"Ratio standard deviation {ratioDeviation:F3}; the damage spread alone gives {spreadDeviation:F3}."));
        }
        if (engineOnly.Count > 0 && logHits.Count > 0)
        {
            facts.Add(Invariant($"Engine-only hits: {(double)engineOnly.Count / logHits.Count:0.0%} of the ID's log hits, in {engineOnlyGroups:N0} groups."));
        }
        if (missing.Count > 0 && logHits.Count > 0)
        {
            facts.Add(Invariant($"Missing hits: {(double)missing.Count / logHits.Count:0.0%} of the ID's log hits, in {missingGroups:N0} groups."));
        }
        if (outOfRange.Count > 0)
        {
            facts.Add(Invariant($"{fits:N0} of the {outOfRange.Count:N0} hits out of range fit another engine hit of their release. Log hits land {lagMedian:F3}s after release at the median, {lag90:F3}s at the 90th percentile."));
        }
        if (totals.Count > 0)
        {
            facts.Add(Invariant($"Release totals over {totals.Count:N0} releases: mean log/engine {totalsMean:F3}, standard deviation {totalsDeviation:F3}."));
        }
        if (tickOffsets != null) facts.Add(TickFact(tickOffsets, options));

        // A swap moves no damage, and it makes the single hits' ratios say nothing about the game.
        double pairedStake = logDamage * Math.Abs(1.0 - totalsMean) / Math.Max(1.0, logTotal);
        double impact = group switch
        {
            FindingGroup.HitCounts or FindingGroup.UnmodeledContent => countImpact,
            FindingGroup.ReplayPairing => Math.Max(pairedStake, countImpact),
            _ => Math.Max(calibrationImpact, countImpact),
        };
        var splits = group == FindingGroup.ReplayPairing ? [] : Splits(matched);
        return new Finding(entity, name, group, impact, string.Join(" · ", headline), facts, splits, issues, Examples(issues))
        {
            Damage = new DamageMetrics(logDamage, share, logHits.Count, matched.Count, Stats(normal), Stats(crit), outOfRange.Count,
                engineOnly.Count, engineOnlyGroups, missing.Count, missingGroups, ratioDeviation, spreadDeviation, fits,
                new RatioStats(totals.Count, totalsMean, totalsDeviation), lagMedian, lag90, tickOffsets,
                new DamageGates(inScope, normalOff, critOff, inScope && scattered, engineOnlyOff, missingOff, mispaired)),
            SampleHits = Samples(normal, crit, engineOnly, missing),
        };
    }

    private static RatioStats Stats(List<HitPair> hits)
    {
        var ratios = hits.Select(h => h.Ratio!.Value).ToList();
        return new RatioStats(ratios.Count, ratios.Count > 0 ? ratios.Average() : 0, StandardDeviation(ratios));
    }

    private static List<HitPair> Samples(List<HitPair> normal, List<HitPair> crit, List<HitPair> engineOnly, List<HitPair> missing)
    {
        var samples = new List<HitPair>();
        foreach (var side in new[] { normal, crit }.Where(s => s.Count > 0))
        {
            samples.Add(side.OrderBy(h => h.Ratio!.Value).ElementAt(side.Count / 2));
            samples.Add(side.MaxBy(h => Math.Abs(1.0 - h.Ratio!.Value))!);
        }
        samples.AddRange(engineOnly.Take(1));
        samples.AddRange(missing.Take(1));
        return samples.Distinct().ToList();
    }

    /// <summary>Log over engine damage of each release with several matched hits.</summary>
    private static List<double> ReleaseTotals(List<HitPair> matched) =>
        matched.GroupBy(h => h.EngineTime).Where(g => g.Count() >= 2)
            .Select(g => g.Sum(h => h.Log!.UnblockedAmount) / g.Sum(h => h.EngineAmount)).ToList();

    /// <summary>The docs' <c>log damage x |1 - ratio|</c> of a set of matched hits.</summary>
    private static double Stake(List<HitPair> hits) =>
        hits.Count == 0 ? 0 : hits.Sum(h => h.Log!.UnblockedAmount) * Math.Abs(1.0 - hits.Average(h => h.Ratio!.Value));

    private static bool IsOff(List<HitPair> hits)
    {
        if (hits.Count < 2) return false;
        var ratios = hits.Select(h => h.Ratio!.Value).ToList();
        double offset = Math.Abs(1.0 - ratios.Average());
        return offset > CalibrationTolerance && offset > CalibrationStandardErrors * StandardDeviation(ratios) / Math.Sqrt(ratios.Count);
    }

    /// <summary>
    /// How many hits out of range have a log amount inside the range of another engine hit of their release: the log hit
    /// belongs to that engine hit's target, or the same target's later release.
    /// </summary>
    private static int ReleaseFits(List<HitPair> outOfRange, List<HitPair> hits, ReplayOptions options)
    {
        var releases = hits.Where(h => h.EngineAmount > 0).ToLookup(h => h.EngineTime);
        return outOfRange.Count(h =>
        {
            double multiplier = h.State?.Hit?.CritMultiplier ?? 1.0;
            return releases[h.EngineTime].Any(other =>
            {
                if (ReferenceEquals(other, h)) return false;
                double otherNormal = other.Crit ? other.EngineAmount / (other.State?.Hit?.CritMultiplier ?? 1.0) : other.EngineAmount;
                double average = h.Crit ? otherNormal * multiplier : otherNormal;
                return Math.Abs(h.Log!.UnblockedAmount - average) <= average * options.DamageSpread;
            });
        });
    }

    private static int Events(IEnumerable<double> times, double windowSeconds)
    {
        int events = 0;
        double last = double.NegativeInfinity;
        foreach (double time in times.Order())
        {
            if (time - last > windowSeconds) events++;
            last = time;
        }
        return events;
    }

    /// <summary>The conditions that divide <paramref name="matched"/> most cleanly and pass the gates above, tightest first.</summary>
    private static List<DamageSplit> Splits(List<HitPair> matched)
    {
        int minimum = Math.Max(SplitMinimumHits, (int)(matched.Count * SplitMinimumShare));
        if (matched.Count < minimum * 2) return [];
        var sides = new Dictionary<string, (int N, double Sum, double Squares)>();
        void Add(string condition, double ratio)
        {
            var (n, sum, squares) = sides.GetValueOrDefault(condition);
            sides[condition] = (n + 1, sum + ratio, squares + ratio * ratio);
        }
        double totalSum = 0, totalSquares = 0;
        foreach (var hit in matched)
        {
            double ratio = hit.Ratio!.Value;
            totalSum += ratio;
            totalSquares += ratio * ratio;
            if (hit.TickFraction < 1.0) Add("Partial tick", ratio);
            if (hit.State is not { } state) continue;
            if (state.Target is { } target)
            {
                Add($"Enemy {target.Name}", ratio);
                foreach (var condition in AuraConditions(target.Auras, "on the target")) Add(condition, ratio);
            }
            foreach (var condition in AuraConditions(state.Hero.Auras, "on the hero")) Add(condition, ratio);
        }

        var splits = new List<(DamageSplit Split, double Within)>();
        foreach (var (condition, (n, sum, squares)) in sides)
        {
            int rest = matched.Count - n;
            if (n < minimum || rest < minimum) continue;
            double restSum = totalSum - sum, restSquares = totalSquares - squares;
            double meanWith = sum / n, meanWithout = restSum / rest;
            double varianceWith = Math.Max(0.0, squares / n - meanWith * meanWith), varianceWithout = Math.Max(0.0, restSquares / rest - meanWithout * meanWithout);
            double difference = Math.Abs(meanWith - meanWithout);
            double standardError = Math.Sqrt(varianceWith / n + varianceWithout / rest);
            if (difference < SplitMinimumDifference || difference < SplitStandardErrors * standardError) continue;
            double within = Math.Sqrt((varianceWith * n + varianceWithout * rest) / matched.Count);
            splits.Add((new DamageSplit(condition, n, meanWith, rest, meanWithout), within));
        }
        // Auras always up together divide the hits the same way, and one line of them says it all.
        return splits.OrderBy(s => s.Within)
            .DistinctBy(s => (s.Split.HitsWith, Math.Round(s.Split.RatioWith, 3)))
            .Take(SplitsShown).Select(s => s.Split).ToList();
    }

    private static IEnumerable<string> AuraConditions(IReadOnlyList<EngineAura> auras, string where) =>
        auras.Select(a => a.LogId > 0 ? $"{a.Name} ({a.LogId}) {where}" : $"{a.Name} {where}").Distinct();

    private static TickOffsets? Ticks(List<ReplayIssue> ticks)
    {
        var offsets = ticks.Where(i => i.Subject?.StartsWith("tick time") == true).Select(i => Offset(i)).Order().ToList();
        return offsets.Count == 0 ? null : new TickOffsets(offsets.Count, Percentile(offsets, 0.5), (double)offsets.Count(o => o > 0) / offsets.Count);
    }

    private static string TickFact(TickOffsets ticks, ReplayOptions options) =>
        Invariant($"{ticks.Count:N0} log ticks outside the engine's tick time by {options.TickTimeToleranceSeconds * 1000:F0}ms or more: median offset {ticks.MedianSeconds * 1000:+0;-0;0}ms, {ticks.LateShare:0%} of them late.");

    // ------------------------------------------------------------------ resources and auras

    /// <summary>
    /// The replay takes the log's state after every step's check (<see cref="ReplayDriver"/>), so each issue is one step's
    /// difference and the differences add up. A difference on one step and its opposite on the next is one event logged on
    /// the other side of the reading.
    /// </summary>
    private static Finding Drift(IssueEntity entity, FindingGroup group, List<ReplayIssue> issues, ReplayResult r, int checks)
    {
        var offsets = issues.Select(i => (Issue: i, Offset: Offset(i))).ToList();
        double net = offsets.Sum(o => o.Offset), gross = offsets.Sum(o => Math.Abs(o.Offset));
        int cancelling = 0;
        for (int i = 1; i < offsets.Count; i++)
        {
            var (before, after) = (offsets[i - 1], offsets[i]);
            bool adjacent = after.Issue.Step - before.Issue.Step == 1 && before.Offset * after.Offset < 0;
            if (!adjacent || Math.Abs(before.Offset + after.Offset) > CancellingShare * Math.Max(Math.Abs(before.Offset), Math.Abs(after.Offset))) continue;
            cancelling += 2;
            i++;
        }

        var facts = new List<string>();
        if (cancelling > 0) facts.Add(Invariant($"{cancelling:N0} of them cancel on adjacent steps."));
        // A step's difference built up since the step before it.
        var abilities = offsets
            .Where(o => o.Issue.Step >= 2 && o.Issue.Step - 2 < r.Steps.Count)
            .GroupBy(o => r.Steps[o.Issue.Step - 2].AbilityName)
            .Select(g => new DriftAbility(g.Key, g.Count(), g.Sum(o => o.Offset)))
            .OrderByDescending(a => Math.Abs(a.Net)).Take(DriftAbilitiesShown).ToList();
        if (abilities.Count > 0)
        {
            facts.Add("Log minus engine after each ability: " + string.Join(", ", abilities.Select(a => Invariant($"{a.Ability} {a.Net:+0.##;-0.##;0} ({a.Steps:N0} steps)"))) + ".");
        }
        string headline = Invariant($"{issues.Count:N0} of {checks:N0} checks differ · log minus engine net {net:+0.##;-0.##;0}, gross {gross:0.##}");
        // A tracked aura's name ends in its log ID, and the finding shows the ID on its own.
        string name = entity.Id > 0 ? entity.Name.Replace($" ({entity.Id})", "") : entity.Name;
        return new Finding(entity, name, group, null, headline, facts, [], issues, Examples(issues))
        {
            Drift = new DriftMetrics(checks, net, gross, cancelling, abilities),
        };
    }

    // ------------------------------------------------------------------ casts and procs

    private static Finding Plain(IssueEntity entity, FindingGroup group, List<ReplayIssue> issues) =>
        new(entity, entity.Name, group, null, Kinds(issues), [], [], issues, Examples(issues));

    private static Finding Proc(IssueEntity entity, List<ReplayIssue> issues, ReplayResult r)
    {
        var metrics = r.ProcStats.FirstOrDefault(p => p.Key == entity.Name) is { } stats ? new ProcMetrics(stats.Rolls, stats.ExpectedProcs, stats.Procs) : null;
        var facts = metrics == null ? new List<string>() : [Invariant($"{metrics.Rolls:N0} rolls, {metrics.ExpectedProcs:F1} expected procs, {metrics.Procs:N0} log procs.")];
        return new Finding(entity, entity.Name, FindingGroup.Procs, null, Kinds(issues), facts, [], issues, Examples(issues)) { Proc = metrics };
    }

    private static string Kinds(List<ReplayIssue> issues) =>
        string.Join(" · ", issues.GroupBy(i => Kind(i)).OrderByDescending(g => g.Count()).Select(g => Invariant($"{g.Count():N0} {g.Key}")));

    /// <summary>The kind of an issue: its subject before the entity's name.</summary>
    public static string Kind(ReplayIssue issue)
    {
        string subject = issue.Subject ?? issue.Category.ToString();
        int colon = subject.IndexOf(':');
        return colon > 0 ? subject[..colon] : subject;
    }

    // ------------------------------------------------------------------ shared

    /// <summary>The log's value minus the middle of the engine's range.</summary>
    private static double Offset(ReplayIssue issue) =>
        issue.Observed is { } observed && issue.Expected is { } expected ? observed - (expected.Low + expected.High) / 2 : 0.0;

    /// <summary>For each kind of issue its first, median and worst; with no numbers, its first, middle and last.</summary>
    private static List<ReplayIssue> Examples(List<ReplayIssue> issues)
    {
        var examples = new List<ReplayIssue>();
        foreach (var kind in issues.GroupBy(i => Kind(i)))
        {
            var ranked = kind.OrderBy(i => Math.Abs(i.Deviation ?? 0)).ThenBy(i => i.Time).ToList();
            examples.Add(kind.MinBy(i => i.Time)!);
            examples.Add(ranked[ranked.Count / 2]);
            examples.Add(kind.Any(i => i.Deviation is > 0 or < 0) ? ranked[^1] : kind.MaxBy(i => i.Time)!);
        }
        return examples.Distinct().OrderBy(i => i.Time).ToList();
    }

    private static double StandardDeviation(List<double> values)
    {
        if (values.Count < 2) return 0;
        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    private static double Percentile(List<double> sorted, double fraction) =>
        sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * fraction))];
}
