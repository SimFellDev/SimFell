namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Base for Meiko's Combo Builders (Wind Kick, Earth Fist, Spirit Palm): each strikes, then joins the combo with its
/// technique. Each is cast on the global cooldown alone, with no resource cost: combat logs have builders every global
/// cooldown in runs of up to 46.
/// </summary>
public abstract class ComboBuilder : MeikoAbility
{
    protected ComboBuilder(Meiko owner, int id, string name, Technique technique)
        : base(owner, id, name, AbilityCategory.Basic)
    {
        Technique = technique;
    }

    /// <summary>The technique this builder adds to the combo.</summary>
    public Technique Technique { get; }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Strike(ctx, target);
        Meiko.AddBuilder(Technique, ctx);
    }

    /// <summary>The builder's damage.</summary>
    protected abstract void Strike(SimulationContext ctx, EnemyTarget target);
}
