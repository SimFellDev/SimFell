using FellowshipSim.Core.Engine;

namespace FellowshipSim.Engine;

/// <summary>
/// A Monte Carlo sim's DPS: mean, lowest and highest fight, standard deviation, and each ability's mean DPS; with its
/// damage over time, each damage source's casts, hits and crits, each buff's and debuff's uptime, one typical fight's
/// casts and hits in order, Spirit Points over the fight, the DPS to bosses over the time of boss fighting, and a dungeon's party DPS and the hero's share of the party's damage, and the fights by DPS. A result saved before those were recorded has them null.
/// <see cref="TypicalFight"/> is the typical fight's index in the sim: its seed is the sim's first seed plus the index.
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
    double? BossDps = null,
    IReadOnlyList<HitReport>? Hits = null,
    int? TypicalFight = null,
    double? PartyDps = null,
    double? GroupDamageShare = null,
    DpsDistribution? Distribution = null)
{
    public static SimResult From(MonteCarloResult result) => new(result.MeanDps, result.MinDps, result.MaxDps,
        result.StandardDeviation, result.AbilityDpsBreakdown, result.Iterations, result.DurationSeconds,
        result.Timeline, result.Abilities, result.Buffs, result.PrimaryTargetDps, result.Casts, result.Spirit, result.BossDps,
        result.Hits, result.TypicalFight, result.PartyDps, result.GroupDamageShare, result.Distribution);
}
