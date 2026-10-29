using FellowshipSim.Core.Engine;

namespace FellowshipSim.Engine;

/// <summary>
/// A Monte Carlo sim's DPS: mean, lowest and highest fight, standard deviation, and each ability's mean DPS; with its
/// damage over time, each damage source's casts, hits and crits, each buff's and debuff's uptime, one typical fight's
/// casts in order, Spirit Points over the fight, and the DPS to bosses over the time of boss fighting. A result saved before those were recorded has them null.
/// </summary>
public sealed record SimResult(
    double MeanDps,
    double MinDps,
    double MaxDps,
    double StandardDeviation,
    IReadOnlyDictionary<string, double> AbilityDps,
    int Iterations,
    double DurationSeconds,
    SimTimeline? Timeline = null,
    IReadOnlyList<AbilityReport>? Abilities = null,
    IReadOnlyList<BuffReport>? Buffs = null,
    double? PrimaryTargetDps = null,
    IReadOnlyList<CastReport>? Casts = null,
    SpiritTimeline? Spirit = null,
    double? BossDps = null)
{
    public static SimResult From(MonteCarloResult result) => new(result.MeanDps, result.MinDps, result.MaxDps,
        result.StandardDeviation, result.AbilityDpsBreakdown, result.Iterations, result.DurationSeconds,
        result.Timeline, result.Abilities, result.Buffs, result.PrimaryTargetDps, result.Casts, result.Spirit, result.BossDps);
}
