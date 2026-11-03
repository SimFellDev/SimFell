namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>One damage over time effect on one enemy. <see cref="DotTracker"/> runs its ticks.</summary>
public sealed class DotInstance
{
    internal DotInstance(DamageOverTime effect, EnemyTarget target, SimTime now)
    {
        Effect = effect;
        Target = target;
        AppliedAt = now;
        LastUpdate = now;
        LastTickAt = now;
    }

    public DamageOverTime Effect { get; }
    public EnemyTarget Target { get; }
    public SimTime AppliedAt { get; }
    public SimTime ExpiresAt { get; internal set; }
    public bool IsActive { get; internal set; } = true;

    /// <summary>Stacks of a <see cref="DotStacking.Stacking"/> effect (1 otherwise).</summary>
    public int Stacks { get; internal set; } = 1;

    /// <summary>Ticks dealt since the effect was applied (applying it again keeps the count).</summary>
    public int Ticks { get; internal set; }

    /// <summary>Damage of a full tick of an <see cref="DotStacking.Accumulate"/> effect.</summary>
    public double TickAmount { get; internal set; }

    /// <summary>Multiplier on the Cinders of its ticks (Fire Ball's target falloff).</summary>
    public double CinderScale { get; internal set; } = 1.0;

    /// <summary>Base seconds of the tick period done at <see cref="LastUpdate"/>.</summary>
    internal double Progress;
    internal SimTime LastUpdate;
    internal SimTime LastTickAt;
    /// <summary>Base seconds of the tick period done per second (haste and tick rate bonuses).</summary>
    internal double Rate = 1.0;
    internal ScheduledEvent? Event;
    /// <summary>The tick and the end <see cref="Event"/> runs, made once.</summary>
    internal Action<SimulationContext>? TickAction, ExpireAction;

    public double RemainingSeconds(SimTime now) => Math.Max(0.0, (ExpiresAt - now).TotalSeconds);
}
