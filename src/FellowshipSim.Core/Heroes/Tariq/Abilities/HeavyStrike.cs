namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1929: "Instantly smash the target for physical damage and deal physical damage to all enemies near the
/// target. Thunder Call: Heavy Strike also charges the ground in front of you with lightning, dealing lightning damage to
/// the target and nearby enemies. Generates 12 Fury."
/// </summary>
public class HeavyStrike : TariqAbility
{
    public const int AbilityId = 1929;
    public const string AbilityName = "Heavy Strike";
    /// <summary>Placeholder HeavyStrikeDmg: 566% of Strength.</summary>
    public const double Coefficient = 5.66;
    /// <summary>Placeholder HeavyStrikeClvDmg: 169.8% of Strength.</summary>
    public const double CleaveCoefficient = 1.698;
    /// <summary>Placeholder HeavyStrikeLightnDmg: 198% of Strength.</summary>
    public const double LightningCoefficient = 1.98;
    /// <summary>Game data cooldown 8, its duration scaled by Haste.</summary>
    public const double CooldownSeconds = 8.0;
    /// <summary>Ink.AutoAttackBuff.ResourceGain: 12% of maximum Fury.</summary>
    public const double FuryGain = 12.0;
    /// <summary>Game data targetCountFalloff: effects 2755 and 1779, threshold 8.</summary>
    public const double TargetThreshold = 8;

    /// <summary>Combat logs: the hit on the target is effect 1776, the cleave on the other enemies 2755, the lightning 1779.</summary>
    public const int TargetEffectId = 1776;
    public const int CleaveEffectId = 2755;
    public const int LightningEffectId = 1779;
    public const string LightningName = "Thunder Call: Heavy Strike";

    /// <summary>A Heavy Strike was cast at an enemy (Square Hammer, Far Beyond Driven, Blood &amp; Thunder).</summary>
    public event Action<SimulationContext, EnemyTarget>? Struck;

    public HeavyStrike(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        AoeTargetThreshold = TargetThreshold;
        DamageLogId = LogIds.Effect(TargetEffectId);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Tariq;
        TriggerCooldown(ctx);
        hero.GainFury(FuryGain, ctx, target);

        // Combat logs (3 Tariq fights): the target's hit, then the cleave on the others, then the lightning on all of them.
        DealDamage(ctx, target);
        DealAoeDamage(ctx, ctx.ActiveEnemiesExcept(target), damageMultiplier: CleaveCoefficient / Coefficient,
            overrideId: LogIds.Effect(CleaveEffectId));
        if (hero.IsCharged(ctx.CurrentTime))
        {
            DealAoeDamage(ctx, WildSwing.AreaAround(ctx, target), damageMultiplier: LightningCoefficient / Coefficient,
                overrideName: LightningName, overrideId: LogIds.Effect(LightningEffectId), extraCritChance: hero.LightningCritChance);
        }
        Struck?.Invoke(ctx, target);
    }
}
