namespace FellowshipSim.Core.Engine;

/// <summary>
/// Damage over a sim's fights, one value per second of the fight: the mean of every fight and, from a sample of the
/// fights, the 5th and 95th percentiles. <see cref="Damage"/> is the damage done so far; <see cref="Dps"/> is the DPS
/// over the last <see cref="RollingSeconds"/> seconds (fewer at the start). <see cref="PartyDps"/> is a dungeon's party DPS
/// (the health its enemies lost) over the same window, the mean of every fight; null for training dummies.
/// </summary>
public sealed record SimTimeline(
    IReadOnlyList<double> Damage,
    IReadOnlyList<double> DamageLow,
    IReadOnlyList<double> DamageHigh,
    IReadOnlyList<double> Dps,
    IReadOnlyList<double> DpsLow,
    IReadOnlyList<double> DpsHigh,
    double RollingSeconds,
    IReadOnlyList<double>? PartyDps = null);
