using System.Globalization;
using System.Text;
using System.Text.Json;
using FellowshipSim.Logs;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The JSON of a review's findings, the file a person downloads from the review and hands to /replay-fix
/// (docs/REPLAY_ACCURACY.md has the fields): each finding's numbers, examples, hits and engine states. It is unindented,
/// since a reader takes one finding from it. Written with <see cref="Utf8JsonWriter"/>, since a browser worker runs it.
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
    public static string ForFight(ReviewedFight fight)
    {
        var findings = ReplayFindings.From(fight.Result);
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", Schema);
            w.WriteString("hero", fight.Script.Player.Hero);
            w.WriteString("patch", GamePatch.Current.Name);
            Thresholds(w, fight.Result.Options);
            Totals(w, fight.Result, findings);
            Unmodeled(w, fight.Result);
            Fight(w, fight);
            w.WriteStartArray("findings");
            for (int i = 0; i < findings.Count; i++) WriteFinding(w, findings[i], i + 1, fight.Result);
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

    private static void Totals(Utf8JsonWriter w, ReplayResult r, IReadOnlyList<Finding> findings)
    {
        var (engine, log) = ReplayReportWriter.DamageTotals(r);
        w.WriteStartObject("totals");
        Number(w, "logDamage", log, AmountDigits);
        Number(w, "engineDamage", engine, AmountDigits);
        w.WriteNumber("logHits", r.LogHits);
        w.WriteNumber("matchedLogHits", r.MatchedLogHits);
        w.WriteNumber("steps", r.Steps.Count);
        w.WriteNumber("passedSteps", r.PassedSteps);
        w.WriteNumber("forcedCasts", r.ForcedCasts.Count);
        w.WriteNumber("issues", ReplayFindings.IssuesOf(r).Count);
        w.WriteNumber("findings", findings.Count);
        Number(w, "impact", findings.Where(f => f.Group != FindingGroup.WithinTargets).Sum(f => f.Impact ?? 0), RatioDigits);
        w.WriteStartObject("groups");
        foreach (var group in findings.GroupBy(f => f.Group).OrderBy(g => g.Key)) w.WriteNumber(group.Key.ToString(), group.Count());
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void Unmodeled(Utf8JsonWriter w, ReplayResult r)
    {
        void Counts(string name, IEnumerable<KeyValuePair<string, int>> counts)
        {
            w.WriteStartObject(name);
            foreach (var count in counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key)) w.WriteNumber(count.Key, count.Value);
            w.WriteEndObject();
        }

        w.WriteStartObject("unmodeled");
        Counts("abilities", r.UnmodeledAbilities);
        w.WriteStartArray("customizations");
        foreach (string customization in r.UnmodeledCustomizations.Distinct().Order()) w.WriteStringValue(customization);
        w.WriteEndArray();
        Counts("externalAuras", r.UnmodeledExternalAuras);
        Counts("untrackedLogAuras", r.UntrackedLogAuras);
        w.WriteEndObject();
    }

    private static void Fight(Utf8JsonWriter w, ReviewedFight fight)
    {
        string id = fight.ReviewId;
        w.WriteStartObject("fight");
        w.WriteString("review", id);
        w.WriteString("reportCode", fight.Script.ReportCode);
        w.WriteNumber("fightId", fight.Script.FightId);
        w.WriteString("fightName", fight.Script.FightName);
        if (fight.Script.FightDate is { } date) w.WriteString("fightDate", date);
        w.WriteString("player", fight.Result.PlayerName);
        w.WriteNumber("playerId", fight.PlayerId);
        Number(w, "durationSeconds", fight.Result.DurationSeconds, SecondsDigits);
        w.WriteStartObject("files");

        w.WriteString("summary", $"data/reviews/{id}.summary.txt");
        w.WriteString("hits", $"data/reviews/{id}.hits.csv");
        w.WriteString("page", $"data/reviews/{id}.html");
        w.WriteString("script", $"data/scripts/{id}.replay.json");
        w.WriteString("rawFight", $"data/fights/{id}.raw.json");
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteFinding(Utf8JsonWriter w, Finding f, int rank, ReplayResult r)
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
            w.WriteStartArray("examples");
            // A kind's first example has the engine state: every state in a finding is a page of auras.
            var kinds = new HashSet<string>();
            foreach (var example in f.Examples) Example(w, example, r, kinds.Add(ReplayFindings.Kind(example)));
            w.WriteEndArray();
            w.WriteStartArray("sampleHits");
            foreach (var hit in f.SampleHits) Hit(w, hit);
            w.WriteEndArray();
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

    /// <summary>Omits a null value, and an infinite bound of a one-sided range.</summary>
    private static void Number(Utf8JsonWriter w, string name, double? value, int digits)
    {
        if (value is { } number && double.IsFinite(number)) w.WriteNumber(name, Math.Round(number, digits));
    }
}
