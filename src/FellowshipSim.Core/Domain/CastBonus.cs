namespace FellowshipSim.Core.Domain;

/// <summary>
/// One cast about to resolve, which talents can add to (<see cref="Hero.OnCastResolving"/>): "your next Glacial Blast
/// critically strikes" checks <see cref="Ability"/>, uses up its buff and adds to <see cref="CritChance"/>. Every hit the
/// ability deals while the cast resolves gets what was added.
/// </summary>
public sealed class CastBonus(Ability ability, EnemyTarget? target, SimTime time, object? source, SimulationContext? ctx = null)
{
    public Ability Ability { get; } = ability;
    public EnemyTarget? Target { get; } = target;
    public SimTime Time { get; } = time;

    /// <summary>The fight the cast is in (for effects that keep state between casts and forget it in a new fight).</summary>
    public SimulationContext? SimContext { get; } = ctx;

    /// <summary>What cast it for free (e.g. the talent whose proc it is), or null for a cast the hero made.</summary>
    public object? Source { get; } = source;

    /// <summary>Extra crit chance for the cast's hits.</summary>
    public double CritChance { get; set; }

    /// <summary>Damage multiplier for the cast's hits.</summary>
    public double DamageMultiplier { get; set; } = 1.0;

    /// <summary>Seconds taken off the cast's delay before it strikes (e.g. a falling comet).</summary>
    public double DelayReductionSeconds { get; set; }
}
