namespace FellowshipSim.Core.Domain;

/// <summary>One landed hit. Handlers take it by <c>in</c> (<see cref="DamageDealtHandler"/>), so a hit allocates nothing.</summary>
public readonly record struct DamageContext(
    Hero Source,
    EnemyTarget? Target,
    int AbilityId,
    string AbilityName,
    double Amount,
    bool IsCrit,
    SimTime Timestamp,
    SimulationContext? SimContext = null,
    // The hit's chance to crit (0 when it cannot); a sim that deals each hit's expected damage rolls no crit, so this is
    // what its crits are counted by. -1 when unknown.
    double CritChance = -1
);

/// <summary>Handler for a landed hit (<see cref="Hero.OnDamageDealt"/>, <see cref="Ability.Hit"/>).</summary>
public delegate void DamageDealtHandler(in DamageContext hit);
