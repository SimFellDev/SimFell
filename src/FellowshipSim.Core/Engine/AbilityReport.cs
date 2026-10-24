namespace FellowshipSim.Core.Engine;

/// <summary>
/// One source of damage over a sim's fights: <see cref="Damage"/>, <see cref="Casts"/> and <see cref="Hits"/> are means
/// per fight. <see cref="Casts"/> counts the casts of an ability of that name (0 for procs and other hits no cast of their
/// own starts). A sim deals each hit's expected damage and rolls no crit, so <see cref="CritChance"/> is its hits' mean
/// chance to crit and <see cref="AverageHit"/> its damage per hit, crits included.
/// </summary>
public sealed record AbilityReport(
    string Name,
    double Damage,
    double Dps,
    double Casts,
    double Hits,
    double CritChance,
    double AverageHit);
