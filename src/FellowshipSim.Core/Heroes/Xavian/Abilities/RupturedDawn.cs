namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1857: "Rupture the ground in front of you, dealing magical damage to all enemies in a cone in front of you,
/// Stunning them for 2 seconds and Pulling them to you. Can be used during Global Cooldown." Cooldown 45s.
/// </summary>
public partial class RupturedDawn : XavianAbility
{
    public RupturedDawn(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamageStrengthCoefficient;
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        AoeTargetThreshold = Constants.DamageScalingTargetCountThreshold;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var enemies = Area(ctx, target);
        DealAoeDamage(ctx, enemies);
        Xavian.RaiseOffensiveAbilityUsed(ctx, this);
    }
}
