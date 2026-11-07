namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1102: "Deals 87.75% of Agility damage to your target and other enemies near your target. Grants access to Kick
/// Technique finishers. Combo Builder". Logged under 1102.
/// </summary>
public class WindKick : ComboBuilder
{
    public const int AbilityId = 1102;
    public const string AbilityName = "Wind Kick";
    /// <summary>Placeholder WindKickDmg: 87.75% of Agility.</summary>
    public const double Coefficient = 0.8775;
    /// <summary>Game data targetCountFalloff: effect 1528, threshold 3.</summary>
    public const double TargetThreshold = 3;

    public WindKick(Meiko owner) : base(owner, AbilityId, AbilityName, Technique.Kick)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target)
    {
        DealAoeDamage(ctx, ctx.ActiveEnemies.Count == 0 ? [target] : ctx.ActiveEnemies);
    }
}
