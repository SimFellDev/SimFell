namespace FellowshipSim.Engine.Replay;

public class ReplayOptions
{
    /// <summary>
    /// Free dice: the engine rolls its own crits, procs and damage spread with this seed; the log only supplies the
    /// casts, enemies and situational auras, and nothing is compared step by step (see <see cref="FreeDiceReplay"/>).
    /// </summary>
    public int? FreeDiceSeed { get; set; }

    /// <summary>Stop at the first step with any issue, for review.</summary>
    public bool StopOnFirstFailure { get; set; } = true;
    public int? MaxSteps { get; set; }

    /// <summary>Uniform damage spread of the game (+/- fraction of the average roll).</summary>
    public double DamageSpread { get; set; } = 0.10;
    /// <summary>
    /// Extra room on the high side of the damage band for an unexplained offset (0 = none). The former ~3.4% offset is
    /// explained by Rime's class tuning aura.
    /// </summary>
    public double UnexplainedHighMargin { get; set; } = 0.0;
    /// <summary>
    /// OPEN ITEM: damage IDs whose amounts are not checked yet (their hits must still match the log one to one).
    /// 1000101 Amethyst Splinter: per-crit 4-tick re-spread fits early in the fight but not throughout.
    /// </summary>
    public HashSet<int> UnverifiedDamageIds { get; set; } = [1000101];

    /// <summary>Unmatched hits on a target that dies in the log before they would land hit nothing.</summary>
    public bool MissHitsOnDyingTargets { get; set; } = true;
    /// <summary>
    /// A partial final tick's fraction depends on where the game's 33ms server frames fall, about +/-0.1 of a full tick;
    /// its damage may differ from the engine's by that much of a full tick on top of the spread.
    /// </summary>
    public double PartialTickFractionTolerance { get; set; } = 0.1;
    /// <summary>
    /// A hit that picks its own target takes the free log hit landing closest to its release plus the ID's typical
    /// travel time (learned from matched hits; this until there are any). A log hit on the hero's current target counts
    /// this much further away, leaving it for hits that can only go there (e.g. Cascading Blitz swallows share the
    /// Frost Swallows ID).
    /// </summary>
    public double DefaultTravelSeconds { get; set; } = 0.5;
    /// <summary>
    /// A group dealing under this fraction of the previous hits (median) looks like a partial tick; a partial tick
    /// matching a full-looking group (or the reverse) counts this many seconds further away.
    /// </summary>
    public double PartialTickRatio { get; set; } = 0.6;
    public double PartialMismatchPenaltySeconds { get; set; } = 0.05;
    /// <summary>
    /// Cast everything the log casts, 1:1: when the engine would refuse (cooldown, resources, global cooldown, still
    /// casting) the cast is made anyway and listed as forced, without failing the step.
    /// </summary>
    public bool ForceCasts { get; set; } = true;
    /// <summary>A cast may come this much before the engine's global cooldown ends (server frames and latency).</summary>
    public double GlobalCooldownToleranceSeconds { get; set; } = 0.1;
    /// <summary>Enemies join this long before their first event in the log (their arrival is only known from it).</summary>
    public double EnemyAriseLeadSeconds { get; set; } = 0.1;
    public double AvoidedTargetPenaltySeconds { get; set; } = 0.2;
    /// <summary>Absolute slack for integer rounding of small hits.</summary>
    public double DamageAbsoluteTolerance { get; set; } = 1.0;

    /// <summary>A log hit may land this long before the engine hit (server tick quantization).</summary>
    public double HitEarlySeconds { get; set; } = 0.15;
    /// <summary>A log hit may land this long after the engine hit (projectile travel).</summary>
    public double HitLateSeconds { get; set; } = 2.0;
    /// <summary>Log hits with the same damage ID landing within this window belong to one multi-target hit.</summary>
    /// <remarks>
    /// Combat logs (Y4wpb2f3gc8LqJDX fight 25, Gunde): a pulse on 20-27 enemies is logged over up to 9ms; separate pulses
    /// land at least a server frame (33ms) apart.
    /// </remarks>
    public double HitGroupSeconds { get; set; } = 0.010;

    /// <summary>
    /// Damage IDs whose multi-target hits land over a longer window than <see cref="HitGroupSeconds"/>, with that window.
    /// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23): a Multishot's arrows land over up to 70ms.
    /// </summary>
    public Dictionary<int, double> HitGroupSecondsById { get; set; } = new() { [1318] = 0.1 };

    /// <summary>The window of one multi-target hit with damage ID <paramref name="logId"/>.</summary>
    public double GroupSecondsOf(int logId) => HitGroupSecondsById.TryGetValue(logId, out double seconds) ? seconds : HitGroupSeconds;
    /// <summary>A log hit still unmatched this long after it landed is reported as missing from the engine.</summary>
    public double SettleSeconds { get; set; } = 2.1;

    public double ProcEarlySeconds { get; set; } = 0.05;
    /// <summary>An aura a proc applies may appear in the log this long after the roll.</summary>
    public double ProcAuraWindowSeconds { get; set; } = 0.1;
    /// <summary>Events in the same server tick can be logged this far apart.</summary>
    public double SameTickSeconds { get; set; } = 0.005;
    public double CastTimeToleranceSeconds { get; set; } = 0.1;
    /// <summary>Aura state may differ from the log by event ordering within this window.</summary>
    public double AuraToleranceSeconds { get; set; } = 0.1;
    public double ResourceSnapshotWindowSeconds { get; set; } = 0.002;
}

public enum IssueCategory
{
    Casts,
    Hits,
    Damage,
    Resources,
    Auras,
    Procs
}

/// <param name="Subject">What the issue is about (ability or aura name), for grouping.</param>
public sealed record ReplayIssue(double Time, int Step, IssueCategory Category, string Message, string? Subject = null)
{
    public override string ToString() => $"[{Category}] step {Step} @ {Time:F3}s: {Message}";
}

public sealed class ReplayStep
{
    public int Index { get; init; }
    public double Time { get; init; }
    public string Kind { get; init; } = string.Empty;
    public int AbilityId { get; init; }
    public string AbilityName { get; init; } = string.Empty;
    public string? Target { get; init; }
    /// <summary>False when the engine has no model of the ability; the step is skipped.</summary>
    public bool Modeled { get; set; } = true;
    public string? Note { get; set; }

    public List<ResourceCheck> Resources { get; } = new();
    public List<AuraCheck> Auras { get; } = new();
    public List<HitPair> Hits { get; } = new();
    public List<ReplayIssue> Issues { get; } = new();
    public List<string> UntrackedLogAuras { get; set; } = new();
    public List<string> ExternalAurasOnPlayer { get; set; } = new();
    public int ActiveEnemies { get; set; }
    /// <summary>The engine's haste before the step, and the auras contributing to it.</summary>
    public double EngineHaste { get; set; }
    public List<string> HasteSources { get; set; } = new();

    public bool Passed => Issues.Count == 0;
}

public sealed record ResourceCheck(string Resource, double Engine, double Log, double Tolerance)
{
    public bool Matches => Math.Abs(Engine - Log) <= Tolerance + 1e-9;
}

public sealed record AuraCheck(string Aura, int EngineStacks, int LogStacks)
{
    public bool Matches => EngineStacks == LogStacks;
}

public sealed class ReplayResult
{
    /// <summary>All damage the engine dealt.</summary>
    public double EngineTotalDamage { get; set; }

    public string ReportCode { get; init; } = string.Empty;
    public int FightId { get; init; }
    public string FightName { get; init; } = string.Empty;
    public string PlayerName { get; init; } = string.Empty;
    public string Hero { get; init; } = string.Empty;
    public int TotalSteps { get; init; }

    public List<ReplayStep> Steps { get; } = new();
    public ReplayStep? HaltedAt { get; set; }
    public List<ReplayIssue> HaltIssues { get; } = new();
    public Dictionary<string, int> UnmodeledAbilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> UntrackedLogAuras { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> UnmodeledExternalAuras { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ProcRollStats> ProcStats { get; } = new();
    public List<HitPair> AllHits { get; } = new();
    /// <summary>Talents, augmentations, glyphs, gem nodes and relic passive of the character the engine does not model.</summary>
    public List<string> UnmodeledCustomizations { get; } = new();
    public int LogHits { get; set; }
    /// <summary>Casts the engine would have refused but made to follow the log (with the reason).</summary>
    public List<string> ForcedCasts { get; } = new();
    /// <summary>Fight length in seconds.</summary>
    public double DurationSeconds { get; set; }
    /// <summary>Every damage hit the player dealt according to the log (for the damage overview).</summary>
    public List<LogDamage> LogDamage { get; } = new();
    /// <summary>Completed casts per ability name: in the log, and in the engine.</summary>
    public Dictionary<string, int> LogCasts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> EngineCasts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int MatchedLogHits { get; set; }

    public int PassedSteps => Steps.Count(s => s.Passed);
    /// <summary>The whole fight replayed and every step passed.</summary>
    public bool Completed => HaltedAt == null && Steps.All(s => s.Passed);
}
