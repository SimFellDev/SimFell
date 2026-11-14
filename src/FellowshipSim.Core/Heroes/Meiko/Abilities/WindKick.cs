namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1102: "Deals 87.75% of Agility damage to your target and other enemies near your target. Grants access to Kick
/// Technique finishers. Combo Builder". Logged under 1102.
/// </summary>
public partial class WindKick : ComboBuilder
{
    public WindKick(Meiko owner) : base(owner, AbilityId, AbilityName, Technique.Kick)
    {
        BaseCoefficient = Constants.AgilityCoefficient;
        AoeTargetThreshold = Constants.TargetThresholdForDamageScale;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target)
    {
        DealAoeDamage(ctx, ctx.ActiveEnemies.Count == 0 ? [target] : ctx.ActiveEnemies);
    }
}
