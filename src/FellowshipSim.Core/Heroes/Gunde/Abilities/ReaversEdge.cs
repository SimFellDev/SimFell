namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2290: "Jump into a spinning strike, dealing damage to all nearby enemies. Applies Rend to the
/// targets." Damage falls off beyond a target threshold (game data effect 3235).
/// The cooldown is shortened by Haste at the cast. The game data lists no Critical Strike for it; combat logs show it crit.
/// </summary>
public partial class ReaversEdge : GundeAbility
{

    public ReaversEdge(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = DamageStrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        AoeTargetThreshold = DamageScalingTargetCountThreshold;
        RendShare = DamageToDotScaler;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        RendingPulse(ctx, BeginRend(ctx), AreaTargets(ctx, target));
    }
}
