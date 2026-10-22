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
    SimulationContext? SimContext = null
);

/// <summary>Handler for a landed hit (<see cref="Hero.OnDamageDealt"/>, <see cref="Ability.Hit"/>).</summary>
public delegate void DamageDealtHandler(in DamageContext hit);
