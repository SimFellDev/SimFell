namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

// Each ability lists its game values as constants at the top (IDs and numbers from the game data).

/// <summary>Base for Xavian's abilities.</summary>
public abstract class XavianAbility : Ability
{
    protected XavianAbility(Xavian owner, int id, string name, AbilityCategory category = AbilityCategory.None)
        : base(owner, id, name, category) { }

    protected Xavian Xavian => (Xavian)Owner;

    /// <summary>The ability's Mana cost (0 for none).</summary>
    public double ManaCost { get; protected set; }

    /// <summary>Needs <see cref="ManaCost"/> Mana, then the cooldown.</summary>
    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        double mana = Xavian.ManaHeld;
        if (mana < ManaCost)
        {
            reason = explain ? $"{Name} needs {ManaCost:0.#} Mana (have {mana:0.#})" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    /// <summary>Spends <see cref="ManaCost"/>.</summary>
    protected void PayMana(SimulationContext ctx)
    {
        if (ManaCost > 0) Xavian.SpendMana(ManaCost, ctx);
    }

    /// <summary>
    /// One hit on each of <paramref name="struck"/> (enemies already selected), each losing damage as sqrt(T/N) beyond
    /// <paramref name="threshold"/> enemies.
    /// </summary>
    protected void DealToEach(SimulationContext ctx, IReadOnlyList<EnemyTarget> struck, double threshold, double damageMultiplier = 1.0,
        int? overrideId = null, double extraCritChance = 0.0)
    {
        double falloff = Falloff(struck.Count, threshold);
        for (int i = 0; i < struck.Count; i++)
        {
            DealDamage(ctx, struck[i], damageMultiplier * falloff, overrideId: overrideId, extraCritChance: extraCritChance);
        }
    }

    /// <summary>sqrt(T/N) beyond <paramref name="threshold"/> enemies, 1 at or below it.</summary>
    protected static double Falloff(int count, double threshold) =>
        threshold > 0 && count > threshold ? Math.Sqrt(threshold / count) : 1.0;
}
