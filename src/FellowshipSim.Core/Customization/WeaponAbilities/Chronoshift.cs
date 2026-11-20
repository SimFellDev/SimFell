namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability of Asha's Chronoshift Spire (ability 1558): a cast, then a channel pulsing on a hasted period
/// at all nearby enemies. While channeling, Asha's Arcane Embrace (effect 2279) gives increased cooldown recovery.
/// </summary>
public partial class Chronoshift : Ability
{
    public const int BarrierEffectId = 2279;

    /// <summary>Asha's Arcane Embrace: the temporal barrier while channeling.</summary>
    public static readonly AuraDef Barrier = AuraDef.Effect("Asha's Arcane Embrace", BarrierEffectId,
        modifiers: [StatModifier.More(StatType.IncomingDamageMultiplier, Constants.BuffIncomingDamageMultiplier), StatModifier.Increased(StatType.CooldownRecovery, Constants.ChannelWhileActiveAddedCooldownRecovery)]);

    private readonly AuraDef _barrier = Barrier;

    public Chronoshift(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamagePrimaryAttributeCoefficient;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
        SetCooldown(AbilityCooldown);
        SetChannel(AbilityChannelTime, AbilityChannelTick);
        // Logs: a pulse lands as the channel starts, then every hasted period, plus a partial final pulse.
        ChannelTicksOnStart = true;
        ChannelPartialFinalTick = true;
        AoeTargetThreshold = Constants.DamageTargetCountThreshold;
        // The barrier's CooldownRecovery joins the hero's recovery pool while channeling and leaves it when
        // the channel ends or is stopped. Chronoshift's own cooldown is fixed (a weapon ability's cooldown runs at normal speed).
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(_barrier, ctx.CurrentTime);
    }

    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target != null ? [target] : ctx.ActiveEnemies;
        DealAoeDamage(ctx, enemies, damageMultiplier: fraction);
    }

    public override void OnChannelEnd(SimulationContext ctx, EnemyTarget target, bool clipped)
    {
        Owner.Auras.Remove(_barrier, ctx.CurrentTime);
    }
}
