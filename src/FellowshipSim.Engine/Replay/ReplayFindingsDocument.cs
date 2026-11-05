using System.Globalization;
using System.Text;
using System.Text.Json;
using FellowshipSim.Logs;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The JSON of a review's findings, for an agent working a finding through the engine (docs/REPLAY_ACCURACY.md has the
/// fields). A fight's document (<c>data/reviews/&lt;review&gt;.findings.json</c>) has each finding's examples, hits and
/// engine states. A hero's document (<c>data/accuracy/&lt;Hero&gt;/&lt;label&gt;.findings.json</c>) pools the hero's fights,
/// has each finding's numbers per fight, and compares with an earlier document by key. A fight's document is unindented,
/// since a reader takes one finding from it, and a hero's is indented, since a reader lists its findings. Written with
/// <see cref="Utf8JsonWriter"/>, since a browser worker runs it.
/// </summary>
public static class ReplayFindingsDocument
{
    /// <summary>The document's format. A change to a field's name or meaning moves it.</summary>
    public const int Schema = 1;

    private const int RatioDigits = 4;
    private const int SecondsDigits = 3;
    private const int AmountDigits = 1;
    private const int ImpactDigits = 6;



    /// <summary>One fight's document.</summary>
    public static string ForFight(ReviewedFight fight) =>
        Write(fight.Script.Player.Hero, null, [fight], ReplayFindings.From(fight.Result), null, null);

    /// <summary>The hero's fights pooled, with the comparison against <paramref name="before"/> (an earlier hero document) when given.</summary>
    public static string ForHero(string hero, string label, IReadOnlyList<ReviewedFight> fights, string? beforeLabel = null, string? before = null) =>
        Write(hero, label, fights, ReplayFindings.From(fights.Select(f => f.Result).ToList()), beforeLabel, before);

    private static string Write(string hero, string? label, IReadOnlyList<ReviewedFight> fights, IReadOnlyList<Finding> findings, string? beforeLabel, string? before)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = fights.Count > 1 }))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", Schema);
            w.WriteString("hero", hero);
            if (label != null) w.WriteString("label", label);
            w.WriteString("patch", GamePatch.Current.Name);
            Thresholds(w, fights[0].Result.Options);
            Totals(w, fights, findings);
            Unmodeled(w, fights);
            if (before != null) Comparison(w, beforeLabel ?? "", before, fights, findings);
            w.WriteStartArray("fights");
            foreach (var fight in fights) Fight(w, fight);
            w.WriteEndArray();

            bool single = fights.Count == 1;
            var perFight = single ? null : fights.Select(f => (f.ReviewId, Findings: ReplayFindings.From(f.Result))).ToList();
            w.WriteStartArray("findings");
            for (int i = 0; i < findings.Count; i++)
            {
                WriteFinding(w, findings[i], i + 1, single ? fights[0] : null, perFight);
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Thresholds(Utf8JsonWriter w, ReplayOptions options)
    {
        w.WriteStartObject("thresholds");
        w.WriteNumber("calibrationTolerance", ReplayFindings.CalibrationTolerance);
        w.WriteNumber("targetDamageShare", ReplayFindings.TargetDamageShare);
        w.WriteNumber("hitCountTolerance", ReplayFindings.HitCountTolerance);
        w.WriteNumber("damageSpread", options.DamageSpread);
        w.WriteNumber("tickTimeToleranceSeconds", options.TickTimeToleranceSeconds);
        w.WriteNumber("auraToleranceSeconds", options.AuraToleranceSeconds);
        w.WriteNumber("castTimeToleranceSeconds", options.CastTimeToleranceSeconds);
        w.WriteNumber("globalCooldownToleranceSeconds", options.GlobalCooldownToleranceSeconds);
        w.WriteEndObject();
    }

    private static double TotalImpact(IReadOnlyList<Finding> findings) =>
        findings.Where(f => f.Group != FindingGroup.WithinTargets).Sum(f => f.Impact ?? 0);

    private static void Totals(Utf8JsonWriter w, IReadOnlyList<ReviewedFight> fights, IReadOnlyList<Finding> findings)
    {
        var results = fights.Select(f => f.Result).ToList();
        var damage = results.Select(ReplayReportWriter.DamageTotals).ToList();
        w.WriteStartObject("totals");
        w.WriteNumber("fights", fights.Count);
        Number(w, "logDamage", damage.Sum(d => d.Log), AmountDigits);
        Number(w, "engineDamage", damage.Sum(d => d.Engine), AmountDigits);
        w.WriteNumber("logHits", results.Sum(r => r.LogHits));
        w.WriteNumber("matchedLogHits", results.Sum(r => r.MatchedLogHits));
        w.WriteNumber("steps", results.Sum(r => r.Steps.Count));
        w.WriteNumber("passedSteps", results.Sum(r => r.PassedSteps));
        w.WriteNumber("forcedCasts", results.Sum(r => r.ForcedCasts.Count));
        w.WriteNumber("issues", results.Sum(r => ReplayFindings.IssuesOf(r).Count));
        w.WriteNumber("findings", findings.Count);
        Number(w, "impact", TotalImpact(findings), RatioDigits);
        w.WriteStartObject("groups");
        foreach (var group in findings.GroupBy(f => f.Group).OrderBy(g => g.Key)) w.WriteNumber(group.Key.ToString(), group.Count());
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void Unmodeled(Utf8JsonWriter w, IReadOnlyList<ReviewedFight> fights)
    {
        void Counts(string name, IEnumerable<KeyValuePair<string, int>> counts)
        {
            w.WriteStartObject(name);
            foreach (var count in counts.GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase).Select(g => (g.Key, Count: g.Sum(c => c.Value))).OrderByDescending(c => c.Count).ThenBy(c => c.Key))
            {
                w.WriteNumber(count.Key, count.Count);
            }
            w.WriteEndObject();
        }

        w.WriteStartObject("unmodeled");
        Counts("abilities", fights.SelectMany(f => f.Result.UnmodeledAbilities));
        w.WriteStartArray("customizations");
        foreach (string customization in fights.SelectMany(f => f.Result.UnmodeledCustomizations).Distinct().Order()) w.WriteStringValue(customization);
        w.WriteEndArray();
        Counts("externalAuras", fights.SelectMany(f => f.Result.UnmodeledExternalAuras));
        Counts("untrackedLogAuras", fights.SelectMany(f => f.Result.UntrackedLogAuras));
        w.WriteEndObject();
    }

    private static void Fight(Utf8JsonWriter w, ReviewedFight fight)
    {
        var r = fight.Result;
        string id = fight.ReviewId;
        var (engine, log) = ReplayReportWriter.DamageTotals(r);
        w.WriteStartObject();
        w.WriteString("review", id);
        w.WriteString("reportCode", fight.Script.ReportCode);
        w.WriteNumber("fightId", fight.Script.FightId);
        w.WriteString("fightName", fight.Script.FightName);
        if (fight.Script.FightDate is { } date) w.WriteString("fightDate", date);
        w.WriteString("player", r.PlayerName);
        w.WriteNumber("playerId", fight.PlayerId);
        Number(w, "durationSeconds", r.DurationSeconds, SecondsDigits);
        Number(w, "logDamage", log, AmountDigits);
        Number(w, "engineDamage", engine, AmountDigits);
        w.WriteNumber("logHits", r.LogHits);
        w.WriteNumber("matchedLogHits", r.MatchedLogHits);
        w.WriteNumber("issues", ReplayFindings.IssuesOf(r).Count);
        w.WriteStartObject("files");
        w.WriteString("findings", $"data/reviews/{id}.findings.json");
        w.WriteString("issues", $"data/reviews/{id}.issues.csv");
        w.WriteString("hits", $"data/reviews/{id}.hits.csv");
        w.WriteString("page", $"data/reviews/{id}.html");
        w.WriteString("script", $"data/scripts/{id}.replay.json");
        w.WriteString("rawFight", $"data/fights/{id}.raw.json");
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteFinding(Utf8JsonWriter w, Finding f, int rank, ReviewedFight? evidence, List<(string ReviewId, IReadOnlyList<Finding> Findings)>? perFight)
    {
        w.WriteStartObject();
        w.WriteNumber("rank", rank);
        w.WriteString("key", f.Entity.KeyText);
        w.WriteString("group", f.Group.ToString());
        w.WriteString("kind", f.Entity.Kind.ToString());
        w.WriteNumber("id", f.Entity.Id);
        w.WriteString("name", f.Name);
        Number(w, "impact", f.Impact, ImpactDigits);
        w.WriteNumber("issues", f.Issues.Count);
        w.WriteStartObject("issueKinds");
        foreach (var kind in f.Issues.GroupBy(ReplayFindings.Kind).OrderByDescending(g => g.Count())) w.WriteNumber(kind.Key, kind.Count());
        w.WriteEndObject();
        if (f.Damage is { } damage) Damage(w, damage);
        if (f.Drift is { } drift) Drift(w, drift);
        if (f.Proc is { } proc)
        {
            w.WriteStartObject("proc");
            w.WriteNumber("rolls", proc.Rolls);
            Number(w, "expectedProcs", proc.ExpectedProcs, AmountDigits);
            w.WriteNumber("procs", proc.Procs);
            w.WriteEndObject();
        }
        if (f.Group != FindingGroup.WithinTargets)
        {
            Splits(w, f.Splits);
            if (perFight != null) ByFight(w, f, perFight);
            if (evidence != null)
            {
                w.WriteStartArray("examples");
                // A kind's first example has the engine state: every state in a finding is a page of auras.
                var kinds = new HashSet<string>();
                foreach (var example in f.Examples) Example(w, example, evidence.Result, kinds.Add(ReplayFindings.Kind(example)));
                w.WriteEndArray();
                w.WriteStartArray("sampleHits");
                foreach (var hit in f.SampleHits) Hit(w, hit);
                w.WriteEndArray();
            }
        }
        w.WriteEndObject();
    }

    private static void Damage(Utf8JsonWriter w, DamageMetrics d)
    {
        w.WriteStartObject("damage");
        Number(w, "logDamage", d.LogDamage, AmountDigits);
        Number(w, "share", d.Share, RatioDigits);
        w.WriteNumber("logHits", d.LogHits);
        w.WriteNumber("matched", d.Matched);
        Ratios(w, "normal", d.Normal);
        Ratios(w, "crit", d.Crit);
        w.WriteNumber("outOfRange", d.OutOfRange);
        w.WriteNumber("engineOnly", d.EngineOnly);
        w.WriteNumber("engineOnlyGroups", d.EngineOnlyGroups);
        w.WriteNumber("missing", d.Missing);
        w.WriteNumber("missingGroups", d.MissingGroups);
        Number(w, "ratioDeviation", d.RatioDeviation, RatioDigits);
        Number(w, "spreadDeviation", d.SpreadDeviation, RatioDigits);
        w.WriteNumber("releaseFits", d.ReleaseFits);
        Ratios(w, "releases", d.Releases);
        Number(w, "lagMedianSeconds", d.LagMedian, SecondsDigits);
        Number(w, "lag90Seconds", d.Lag90, SecondsDigits);
        if (d.Ticks is { } ticks)
        {
            w.WriteStartObject("tickOffsets");
            w.WriteNumber("count", ticks.Count);
            Number(w, "medianSeconds", ticks.MedianSeconds, SecondsDigits);
            Number(w, "lateShare", ticks.LateShare, RatioDigits);
            w.WriteEndObject();
        }
        w.WriteStartObject("gates");
        w.WriteBoolean("inScope", d.Gates.InScope);
        w.WriteBoolean("normalOff", d.Gates.NormalOff);
        w.WriteBoolean("critOff", d.Gates.CritOff);
        w.WriteBoolean("scattered", d.Gates.Scattered);
        w.WriteBoolean("engineOnlyOff", d.Gates.EngineOnlyOff);
        w.WriteBoolean("missingOff", d.Gates.MissingOff);
        w.WriteBoolean("mispaired", d.Gates.Mispaired);
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void Ratios(Utf8JsonWriter w, string name, RatioStats ratios)
    {
        w.WriteStartObject(name);
        w.WriteNumber("count", ratios.Count);
        Number(w, "mean", ratios.Mean, RatioDigits);
        Number(w, "deviation", ratios.Deviation, RatioDigits);
        w.WriteEndObject();
    }

    private static void Drift(Utf8JsonWriter w, DriftMetrics d)
    {
        w.WriteStartObject("drift");
        w.WriteNumber("checks", d.Checks);
        Number(w, "net", d.Net, 2);
        Number(w, "gross", d.Gross, 2);
        w.WriteNumber("cancelling", d.Cancelling);
        w.WriteStartArray("afterAbilities");
        foreach (var ability in d.Abilities)
        {
            w.WriteStartObject();
            w.WriteString("ability", ability.Ability);
            w.WriteNumber("steps", ability.Steps);
            Number(w, "net", ability.Net, 2);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Splits(Utf8JsonWriter w, IReadOnlyList<DamageSplit> splits)
    {
        w.WriteStartArray("splits");
        foreach (var split in splits)
        {
            w.WriteStartObject();
            w.WriteString("condition", split.Condition);
            w.WriteNumber("hitsWith", split.HitsWith);
            Number(w, "ratioWith", split.RatioWith, RatioDigits);
            w.WriteNumber("hitsWithout", split.HitsWithout);
            Number(w, "ratioWithout", split.RatioWithout, RatioDigits);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>The fights with their own finding of the entity, the one with the most at stake first.</summary>
    private static void ByFight(Utf8JsonWriter w, Finding f, List<(string ReviewId, IReadOnlyList<Finding> Findings)> perFight)
    {
        var rows = perFight
            .Select(p => (p.ReviewId, Finding: p.Findings.FirstOrDefault(o => o.Entity.Key == f.Entity.Key)))
            .Where(p => p.Finding != null)
            .OrderByDescending(p => p.Finding!.Impact ?? 0).ThenByDescending(p => p.Finding!.Issues.Count).ToList();
        w.WriteNumber("fightsOff", rows.Count(p => p.Finding!.Group != FindingGroup.WithinTargets));
        w.WriteStartArray("byFight");
        foreach (var (reviewId, finding) in rows)
        {
            w.WriteStartObject();
            w.WriteString("review", reviewId);
            w.WriteString("group", finding!.Group.ToString());
            Number(w, "impact", finding.Impact, ImpactDigits);
            w.WriteNumber("issues", finding.Issues.Count);
            if (finding.Damage is { } d)
            {
                Number(w, "normalMean", d.Normal.Count > 0 ? d.Normal.Mean : null, RatioDigits);
                w.WriteNumber("normalHits", d.Normal.Count);
                Number(w, "critMean", d.Crit.Count > 0 ? d.Crit.Mean : null, RatioDigits);
                w.WriteNumber("critHits", d.Crit.Count);
                w.WriteNumber("engineOnly", d.EngineOnly);
                w.WriteNumber("missing", d.Missing);
            }
            if (finding.Drift is { } drift)
            {
                Number(w, "net", drift.Net, 2);
                Number(w, "gross", drift.Gross, 2);
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static void Example(Utf8JsonWriter w, ReplayIssue issue, ReplayResult r, bool withState)
    {
        w.WriteStartObject();
        Number(w, "time", issue.Time, SecondsDigits);
        w.WriteNumber("step", issue.Step);
        if (issue.Step >= 1 && issue.Step <= r.Steps.Count) w.WriteString("ability", r.Steps[issue.Step - 1].AbilityName);
        w.WriteString("issueKind", ReplayFindings.Kind(issue));
        w.WriteString("category", issue.Category.ToString());
        w.WriteString("message", issue.Message);
        if (issue.Expected is { } expected)
        {
            Number(w, "expectedLow", expected.Low, SecondsDigits);
            Number(w, "expectedHigh", expected.High, SecondsDigits);
            w.WriteString("unit", expected.Unit);
        }
        Number(w, "observed", issue.Observed, SecondsDigits);
        Number(w, "deviation", issue.Deviation, SecondsDigits);
        Number(w, "relativeDeviation", issue.RelativeDeviation, RatioDigits);
        if (withState && issue.State is { } state) State(w, state);
        w.WriteEndObject();
    }

    private static void Hit(Utf8JsonWriter w, HitPair hit)
    {
        w.WriteStartObject();
        Number(w, "engineTime", hit.EngineTime, SecondsDigits);
        Number(w, "logTime", hit.Log?.Time, SecondsDigits);
        if (hit.Target is { } target) w.WriteString("target", $"{target.Id}.{target.Instance}");
        Number(w, "engineAmount", hit.EngineAmount, AmountDigits);
        Number(w, "expectedLow", hit.ExpectedLow, AmountDigits);
        Number(w, "expectedHigh", hit.ExpectedHigh, AmountDigits);
        Number(w, "logAmount", hit.Log?.UnblockedAmount, AmountDigits);
        Number(w, "ratio", hit.Ratio, RatioDigits);
        w.WriteBoolean("crit", hit.Crit);
        Number(w, "critChance", hit.CritChance, RatioDigits);
        Number(w, "tickFraction", hit.TickFraction, RatioDigits);
        Number(w, "targetHealthFraction", hit.TargetHealthFraction, RatioDigits);
        if (hit.State is { } state) State(w, state);
        w.WriteEndObject();
    }

    private static void State(Utf8JsonWriter w, EngineState state)
    {
        w.WriteStartObject("state");
        var hero = state.Hero;
        w.WriteStartObject("hero");
        Number(w, "primaryAttribute", hero.PrimaryAttribute, AmountDigits);
        Number(w, "critChance", hero.CritChance, RatioDigits);
        Number(w, "critMultiplier", hero.CritMultiplier, RatioDigits);
        Number(w, "haste", hero.Haste, RatioDigits);
        Number(w, "expertise", hero.Expertise, RatioDigits);
        Number(w, "spirit", hero.Spirit, RatioDigits);
        Number(w, "damageScale", hero.DamageScale, RatioDigits);
        w.WriteNumber("activeEnemies", hero.ActiveEnemies);
        w.WriteStartObject("resources");
        foreach (var resource in hero.Resources) Number(w, resource.Name, resource.Current, 2);
        w.WriteEndObject();
        Auras(w, hero.Auras);
        w.WriteEndObject();
        if (state.Target is { } target)
        {
            w.WriteStartObject("target");
            w.WriteString("name", target.Name);
            w.WriteString("unit", $"{target.Id}.{target.InstanceId}");
            Number(w, "healthFraction", target.HealthFraction, RatioDigits);
            Number(w, "incomingDamageMultiplier", target.IncomingDamageMultiplier, RatioDigits);
            Auras(w, target.Auras);
            w.WriteEndObject();
        }
        if (state.Hit is { } hit)
        {
            w.WriteStartObject("hit");
            Number(w, "baseDamage", hit.BaseDamage, AmountDigits);
            Number(w, "hitDamageMultiplier", hit.HitDamageMultiplier, RatioDigits);
            Number(w, "abilityDamageMultiplier", hit.AbilityDamageMultiplier, RatioDigits);
            Number(w, "targetDamageMultiplier", hit.TargetDamageMultiplier, RatioDigits);
            Number(w, "critChance", hit.CritChance, RatioDigits);
            Number(w, "castCritChance", hit.CastCritChance, RatioDigits);
            Number(w, "extraCritChance", hit.ExtraCritChance, RatioDigits);
            Number(w, "critMultiplier", hit.CritMultiplier, RatioDigits);
            Number(w, "tickFraction", hit.TickFraction, RatioDigits);
            if (hit.Snapshot is { } snapshot) w.WriteBoolean("snapshot", snapshot);
            Number(w, "releaseTime", hit.ReleaseTime, SecondsDigits);
            Number(w, "landingTime", hit.LandingTime, SecondsDigits);
            w.WriteEndObject();
        }
        if (state.Cast is { } cast)
        {
            w.WriteStartObject("cast");
            w.WriteString("ability", cast.Ability);
            Number(w, "castTime", cast.CastTime, SecondsDigits);
            Number(w, "globalCooldown", cast.GlobalCooldown, SecondsDigits);
            w.WriteBoolean("globalCooldownScalesWithHaste", cast.GlobalCooldownScalesWithHaste);
            Number(w, "cooldownLeft", cast.CooldownLeft, SecondsDigits);
            w.WriteNumber("charges", cast.Charges);
            Number(w, "cooldownRate", cast.CooldownRate, RatioDigits);
            if (cast.Refusal != null) w.WriteString("refusal", cast.Refusal);
            w.WriteEndObject();
        }
        w.WriteEndObject();
    }

    private static void Auras(Utf8JsonWriter w, IReadOnlyList<EngineAura> auras)
    {
        w.WriteStartArray("auras");
        foreach (var aura in auras) w.WriteStringValue(AuraText(aura));
        w.WriteEndArray();
    }

    /// <summary><c>{name} ({log ID}) x{stacks} {seconds left}s: {type} {op} {value}, ...</c>.</summary>
    private static string AuraText(EngineAura aura)
    {
        var text = new StringBuilder(aura.Name);
        if (aura.LogId > 0) text.Append(CultureInfo.InvariantCulture, $" ({aura.LogId})");
        text.Append(CultureInfo.InvariantCulture, $" x{aura.Stacks}");
        if (aura.SecondsLeft is { } left) text.Append(CultureInfo.InvariantCulture, $" {left:0.0}s");
        if (aura.Modifiers.Count > 0)
        {
            text.Append(": ").AppendJoin(", ", aura.Modifiers.Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.Type} {m.Op} {m.Value:0.####}")));
        }
        return text.ToString();
    }

    private readonly record struct Entry(string Name, string Group, double Impact, int Issues)
    {
        public bool Off => Group != nameof(FindingGroup.WithinTargets);
    }

    /// <summary>What moved since an earlier hero document, by finding key: the check of a change to the engine.</summary>
    private static void Comparison(Utf8JsonWriter w, string label, string before, IReadOnlyList<ReviewedFight> fights, IReadOnlyList<Finding> findings)
    {
        using var earlier = JsonDocument.Parse(before);
        var old = earlier.RootElement.GetProperty("findings").EnumerateArray().ToDictionary(
            f => f.GetProperty("key").GetString()!,
            f => new Entry(f.GetProperty("name").GetString()!, f.GetProperty("group").GetString()!,
                f.TryGetProperty("impact", out var impact) ? impact.GetDouble() : 0, f.GetProperty("issues").GetInt32()));
        var current = findings.ToDictionary(f => f.Entity.KeyText, f => new Entry(f.Name, f.Group.ToString(), f.Impact ?? 0, f.Issues.Count));

        bool IsOff(Dictionary<string, Entry> entries, string key) => entries.TryGetValue(key, out var entry) && entry.Off;
        var totals = earlier.RootElement.GetProperty("totals");

        w.WriteStartObject("comparison");
        w.WriteString("against", label);
        Number(w, "impactBefore", totals.GetProperty("impact").GetDouble(), RatioDigits);
        Number(w, "impactAfter", TotalImpact(findings), RatioDigits);
        w.WriteNumber("issuesBefore", totals.GetProperty("issues").GetInt32());
        w.WriteNumber("issuesAfter", findings.Sum(f => f.Issues.Count));

        var reviewsBefore = earlier.RootElement.GetProperty("fights").EnumerateArray().Select(f => f.GetProperty("review").GetString()!).ToHashSet();
        var reviewsAfter = fights.Select(f => f.ReviewId).ToHashSet();
        Names(w, "fightsAdded", reviewsAfter.Except(reviewsBefore));
        Names(w, "fightsRemoved", reviewsBefore.Except(reviewsAfter));

        var unmodeled = earlier.RootElement.GetProperty("unmodeled");
        var unmodeledBefore = unmodeled.GetProperty("abilities").EnumerateObject().Select(a => a.Name)
            .Concat(unmodeled.GetProperty("customizations").EnumerateArray().Select(c => c.GetString()!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unmodeledAfter = fights.SelectMany(f => f.Result.UnmodeledAbilities.Keys.Concat(f.Result.UnmodeledCustomizations)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Names(w, "unmodeledResolved", unmodeledBefore.Except(unmodeledAfter, StringComparer.OrdinalIgnoreCase));
        Names(w, "unmodeledAppeared", unmodeledAfter.Except(unmodeledBefore, StringComparer.OrdinalIgnoreCase));

        w.WriteStartArray("resolved");
        foreach (var (key, entry) in old.Where(o => o.Value.Off && !IsOff(current, o.Key)).OrderByDescending(o => o.Value.Impact))
        {
            Moved(w, key, entry);
        }
        w.WriteEndArray();

        w.WriteStartArray("appeared");
        foreach (var (key, entry) in current.Where(c => c.Value.Off && !IsOff(old, c.Key)).OrderByDescending(c => c.Value.Impact))
        {
            Moved(w, key, entry);
        }
        w.WriteEndArray();

        w.WriteStartArray("changed");
        var changed = current.Where(c => c.Value.Off && IsOff(old, c.Key))
            .Select(c => (c.Key, After: c.Value, Before: old[c.Key]))
            .Where(c => c.Before.Group != c.After.Group || c.Before.Issues != c.After.Issues || c.Before.Impact != Math.Round(c.After.Impact, ImpactDigits))
            .OrderByDescending(c => Math.Abs(c.Before.Impact - c.After.Impact)).ThenByDescending(c => Math.Abs(c.Before.Issues - c.After.Issues));
        foreach (var (key, after, was) in changed)
        {
            w.WriteStartObject();
            w.WriteString("key", key);
            w.WriteString("name", after.Name);
            w.WriteString("groupBefore", was.Group);
            w.WriteString("groupAfter", after.Group);
            Number(w, "impactBefore", was.Impact, ImpactDigits);
            Number(w, "impactAfter", after.Impact, ImpactDigits);
            w.WriteNumber("issuesBefore", was.Issues);
            w.WriteNumber("issuesAfter", after.Issues);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Names(Utf8JsonWriter w, string name, IEnumerable<string> names)
    {
        w.WriteStartArray(name);
        foreach (string value in names.Order()) w.WriteStringValue(value);
        w.WriteEndArray();
    }

    private static void Moved(Utf8JsonWriter w, string key, Entry entry)
    {
        w.WriteStartObject();
        w.WriteString("key", key);
        w.WriteString("name", entry.Name);
        w.WriteString("group", entry.Group);
        Number(w, "impact", entry.Impact, ImpactDigits);
        w.WriteNumber("issues", entry.Issues);
        w.WriteEndObject();
    }

    /// <summary>Omits a null value, and an infinite bound of a one-sided range.</summary>
    private static void Number(Utf8JsonWriter w, string name, double? value, int digits)
    {
        if (value is { } number && double.IsFinite(number)) w.WriteNumber(name, Math.Round(number, digits));
    }
}
