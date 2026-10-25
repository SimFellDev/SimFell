namespace FellowshipSim.Core.Domain;

/// <summary>
/// A hero that keeps its own effects on enemies (Ardeos's damage over time): the enemy's debuff shows their stacks but
/// not when they run out. Action lists read them as debuffs, <c>debuff.&lt;name&gt;</c>, <c>dot.&lt;name&gt;</c> and
/// <c>active_dot.&lt;name&gt;</c>, with their own expiry.
/// </summary>
public interface ITargetEffects
{
    /// <summary>The names of the effects, as the game names them ("Searing Blaze").</summary>
    IEnumerable<string> TargetEffectNames { get; }

    /// <summary>The effect whose action-list name is <paramref name="slug"/> on <paramref name="target"/> at <paramref name="now"/>, or null when it is not on it.</summary>
    TargetEffect? FindOn(EnemyTarget target, string slug, SimTime now);
}
