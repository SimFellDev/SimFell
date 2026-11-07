namespace FellowshipSim.Engine.Replay;

/// <summary>A damage ID's numbers in a <see cref="Finding"/>: its headline and facts, and the review's JSON, read them.</summary>
/// <param name="Share">The ID's share of the log's damage.</param>
/// <param name="Normal">Log/engine ratios of matched hits with no crit.</param>
/// <param name="Crit">Log/engine ratios of matched crits.</param>
/// <param name="EngineOnlyGroups">Engine-only hits counted once per multi-target hit.</param>
/// <param name="MissingGroups">Missing hits counted once per multi-target hit.</param>
/// <param name="RatioDeviation">Standard deviation of the matched hits' ratios.</param>
/// <param name="SpreadDeviation">Standard deviation the damage spread alone gives.</param>
/// <param name="ReleaseFits">Hits out of range with a log amount in the range of another engine hit of their release.</param>
/// <param name="Releases">Log over engine damage of each release with several matched hits.</param>
/// <param name="LagMedian">Seconds from release to the log hit, median.</param>
/// <param name="Lag90">Seconds from release to the log hit, 90th percentile.</param>
/// <param name="Ticks">Log ticks outside the engine's tick time; null with none.</param>
public sealed record DamageMetrics(
    double LogDamage,
    double Share,
    int LogHits,
    int Matched,
    RatioStats Normal,
    RatioStats Crit,
    int OutOfRange,
    int EngineOnly,
    int EngineOnlyGroups,
    int Missing,
    int MissingGroups,
    double RatioDeviation,
    double SpreadDeviation,
    int ReleaseFits,
    RatioStats Releases,
    double LagMedian,
    double Lag90,
    TickOffsets? Ticks,
    DamageGates Gates);
