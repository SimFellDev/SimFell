namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

// Each ability lists its game values as constants at the top (IDs and numbers from the game data).

/// <summary>Base for Tariq's abilities.</summary>
public abstract class TariqAbility : Ability
{
    protected TariqAbility(Tariq owner, int id, string name, AbilityCategory category = AbilityCategory.None)
        : base(owner, id, name, category) { }

    protected Tariq Tariq => (Tariq)Owner;

    /// <summary>Needs <paramref name="cost"/> Fury, then the cooldown.</summary>
    protected bool CanPay(SimulationContext ctx, double cost, bool explain, out string? reason)
    {
        double fury = Owner.ResourceAmount(Tariq.Fury);
        if (fury < cost)
        {
            reason = explain ? $"{Name} needs {cost:0.#} Fury (have {fury:0.#})" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    /// <summary>
    /// One hit on each of <paramref name="struck"/> (enemies already selected), each losing damage as sqrt(T/N) beyond
    /// <paramref name="threshold"/> enemies.
    /// </summary>
    protected void DealToEach(SimulationContext ctx, IReadOnlyList<EnemyTarget> struck, double threshold, double damageMultiplier = 1.0,
        string? overrideName = null, int? overrideId = null, double extraCritChance = 0.0)
    {
        double falloff = threshold > 0 && struck.Count > threshold ? Math.Sqrt(threshold / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++)
        {
            DealDamage(ctx, struck[i], damageMultiplier * falloff, overrideName: overrideName, overrideId: overrideId, extraCritChance: extraCritChance);
        }
    }
}
