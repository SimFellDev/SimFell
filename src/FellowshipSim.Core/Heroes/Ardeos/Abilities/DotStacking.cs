namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>What applying a damage over time effect to an enemy that already has it does.</summary>
public enum DotStacking
{
    /// <summary>The effect starts its duration again (Searing Blaze).</summary>
    Refresh,
    /// <summary>Each application is an instance of its own (Engulfing Flames).</summary>
    Concurrent,
    /// <summary>The damage not yet dealt joins the new damage, over the full duration again (Fire Ball, Fire Frogs).</summary>
    Accumulate,
    /// <summary>The effect gains a stack and starts its duration again (Incinerate).</summary>
    Stacking,
}
