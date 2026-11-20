namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 162 (weapon power 358): "Deal 607% magic damage every 0.5 seconds for 3 seconds to target enemy while
/// channeling. ..." A 0.5s cast shortened by Haste, then a 3s channel ticking every 0.5s (hasted), six ticks from 0.5s
/// into the channel; cooldown 60s; a 1.5s global cooldown shortened by Haste. The damage is effect 84 (period 0.5).
/// UNVERIFIED: the ticks' damage ID is taken to be 162, and the tick count six; the downloaded combat logs have this
/// ability in none of their fights.
/// </summary>
public partial class ZeralethsHunger : Ability
{
    public ZeralethsHunger(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.PrimaryAttributeCoefficient;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
        SetCooldown(AbilityCooldown);
        SetChannel(AbilityChannelTime, AbilityChannelTick);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);

    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        if (!target.IsSleeping) DealDamage(ctx, target, damageMultiplier: fraction);
    }
}
