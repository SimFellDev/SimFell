namespace FellowshipSim.Core.Engine;

/// <summary>
/// One source of damage over a sim's fights: <see cref="Damage"/>, <see cref="Casts"/> and <see cref="Hits"/> are means
/// per fight. <see cref="Casts"/> counts the casts of an ability of that name (0 for procs and other hits no cast of their
/// own starts). <see cref="CritChance"/> is its hits' mean chance to crit (steadier than the crits rolled) and
/// <see cref="AverageHit"/> its damage per hit, crits included. <see cref="Id"/> is the damage log ID of
/// its first hit (an ability's ID, or an effect's + 1,000,000). <see cref="Group"/> is the ability the source is part of
/// when its hits have another name (Hammer Storm for Thunder Call: Hammer Storm).
/// </summary>
public sealed record AbilityReport(
    string Name,
    double Damage,
    double Dps,
    double Casts,
    double Hits,
    double CritChance,
    double AverageHit,
    int Id = 0,
    string? Group = null);
