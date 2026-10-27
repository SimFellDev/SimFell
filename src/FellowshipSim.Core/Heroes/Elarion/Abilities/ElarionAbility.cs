namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

// Each ability lists its game values as constants at the top (IDs and numbers from the game data).

/// <summary>Base for Elarion's abilities: the Focus cost and the hits that land after the cast resolved.</summary>
public abstract class ElarionAbility : Ability
{
    protected ElarionAbility(Elarion owner, int id, string name, AbilityCategory category = AbilityCategory.None)
        : base(owner, id, name, category) { }

    protected Elarion Elarion => (Elarion)Owner;

    /// <summary>Focus the ability costs (game data "cost", resource Focus).</summary>
    public double FocusCost { get; protected set; }

    /// <summary>
    /// The Focus a cast costs now: the cost times Event Horizon's multiplier and the ability's own (an Empowered
    /// Multishot), or nothing for a free cast (Celestial Impetus, Resurgent Winds).
    /// </summary>
    public virtual double CostAt(SimTime now) => FocusCost > 0 ? Math.Ceiling(FocusCost * Elarion.FocusCostMultiplier(now) - 1e-9) : 0.0;

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        double cost = CostAt(ctx.CurrentTime);
        if (cost > 0)
        {
            double focus = Elarion.FocusAt(ctx.CurrentTime);
            if (focus < cost)
            {
                reason = explain ? $"{Name} needs {cost:0.#} Focus (have {focus:0.#})" : null;
                return false;
            }
        }
        return base.CanCast(ctx, explain, out reason);
    }

    /// <summary>Pays the cast's Focus (a Spirit Refund may give it back).</summary>
    protected void Pay(SimulationContext ctx, double cost)
    {
        if (cost > 0) Elarion.SpendFocus(cost, ctx);
    }

    /// <summary>
    /// What talents added to the cast resolving now, for hits dealt after it resolved (a ricochet, a channel tick): its
    /// damage multiplier and crit chance.
    /// </summary>
    protected (double Damage, double Crit) CastBonuses => (CastDamageMultiplier, CurrentCast?.CritChance ?? 0.0);

    /// <summary>Clears what the ability holds between fights.</summary>
    public virtual void ResetState() { }
}
