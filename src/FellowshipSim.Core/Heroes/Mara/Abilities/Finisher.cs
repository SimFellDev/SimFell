namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Base for Mara's finishers (Queen's Fang, Arachnid Assault, Hemorrhaging Strike): each spends every Combo Point held
/// and grows with the points spent.
/// </summary>
public abstract class Finisher : MaraAbility
{
    /// <summary>Damage bonus for each Combo Point spent, tooltip text.</summary>
    public const double DamagePerComboPoint = 0.2;

    protected Finisher(Mara owner, int id, string name) : base(owner, id, name, AbilityCategory.Power)
    {
        BreaksStealth = true;
    }

    /// <summary>The Combo Points the cast resolving now spent.</summary>
    protected int PointsSpent { get; private set; }

    /// <summary>The finisher's cast resolved, with the Combo Points it spent (Malevolence).</summary>
    public event Action<SimulationContext, EnemyTarget, int>? Finished;

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        // UNVERIFIED: a finisher needs at least one Combo Point (every logged finisher spent a full set, or the full set a refund returned).
        if (Mara.ComboPointsHeld < 1)
        {
            reason = explain ? $"{Name} needs a Combo Point" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Pay(ctx);
        PointsSpent = Mara.SpendComboPoints(ctx, EnergyCost);
        Strike(ctx, target, PointsSpent);
        Finished?.Invoke(ctx, target, PointsSpent);
    }

    /// <summary>What the finisher does with <paramref name="points"/> Combo Points.</summary>
    protected abstract void Strike(SimulationContext ctx, EnemyTarget target, int points);

    public static double ComboPointMultiplier(int points) => 1.0 + DamagePerComboPoint * points;
}
