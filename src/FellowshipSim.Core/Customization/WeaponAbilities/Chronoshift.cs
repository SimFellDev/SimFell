namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability of Asha's Chronoshift Spire (ability 1558): a cast, then a channel pulsing on a hasted period
/// at all nearby enemies. While channeling, Asha's Arcane Embrace (effect 2279) gives increased cooldown recovery.
/// </summary>
public class Chronoshift : Ability
{
    public const int AbilityId = 1558;
    public const string AbilityName = "Chronoshift";
    /// <summary>Per pulse, of the primary stat.</summary>
    public const double Coefficient = 5.769;
    public const double CooldownSeconds = 180.0;
    public const double CastTimeSeconds = 1.0;
    public const double ChannelSeconds = 3.0;
    public const double PulsePeriodSeconds = 1.5;
    /// <summary>Combat logs: pulses lose damage with the number of targets beyond the target cap.</summary>
    public const double TargetThreshold = 5;
    public const int BarrierEffectId = 2279;
    public const double BarrierDamageTaken = 0.5;
    /// <summary>Increased cooldown recovery while channeling.</summary>
    public const double BarrierCooldownRecovery = 8.0;

    /// <summary>Asha's Arcane Embrace: the temporal barrier while channeling.</summary>
    public static readonly AuraDef Barrier = AuraDef.Effect("Asha's Arcane Embrace", BarrierEffectId,
        modifiers: [StatModifier.More(StatType.IncomingDamageMultiplier, BarrierDamageTaken), StatModifier.Increased(StatType.CooldownRecovery, BarrierCooldownRecovery)]);

    private readonly AuraDef _barrier = Barrier;

    public Chronoshift(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        SetCooldown(CooldownSeconds);
        SetChannel(ChannelSeconds, PulsePeriodSeconds);
        // Logs: a pulse lands as the channel starts, then every hasted period, plus a partial final pulse.
        ChannelTicksOnStart = true;
        ChannelPartialFinalTick = true;
        AoeTargetThreshold = TargetThreshold;
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
