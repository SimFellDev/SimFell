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
    // The hit's chance to crit (0 when it cannot): a sim's report counts crits by it, steadier than the crits rolled. -1
    // when unknown.
    double CritChance = -1,
    // The ability that dealt the hit (Ability.DealDamage), by name; null for a hit of no ability. A hit under another name
    // (Thunder Call: Hammer Storm, dealt by Hammer Storm) is part of that ability.
    string? AbilityGroup = null
);

/// <summary>Handler for a landed hit (<see cref="Hero.OnDamageDealt"/>, <see cref="Ability.Hit"/>).</summary>
public delegate void DamageDealtHandler(in DamageContext hit);
