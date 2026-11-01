namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 162 (weapon power 358): "Deal 607% magic damage every 0.5 seconds for 3 seconds to target enemy while
/// channeling. You are healed for 100% of the damage dealt. ..." A 0.5s cast shortened by Haste, then a 3s channel ticking
/// every 0.5s (hasted); cooldown 60s; a 1.5s global cooldown shortened by Haste. The damage is effect 84 (period 0.5).
/// UNVERIFIED: no combat log has this ability; the ticks' damage ID is assumed to be 162, the channel has no tick as it
/// starts and no partial final tick.
/// </summary>
public class ZeralethsHunger : Ability
{
    public const int AbilityId = 162;
    public const string AbilityName = "Zeraleth's Hunger";
    /// <summary>Per tick, of the primary stat.</summary>
    public const double Coefficient = 6.07;
    public const double CooldownSeconds = 60.0;
    public const double CastTimeSeconds = 0.5;
    public const double ChannelSeconds = 3.0;
    public const double TickPeriodSeconds = 0.5;

    public ZeralethsHunger(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        SetCooldown(CooldownSeconds);
        SetChannel(ChannelSeconds, TickPeriodSeconds);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);

    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        if (!target.IsSleeping) DealDamage(ctx, target, damageMultiplier: fraction);
    }
}
