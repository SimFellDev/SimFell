namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 160 (weapon power 354): "Strike target enemy, dealing damage to it and cleaving for damage
/// to nearby enemies. Fated Strike applies Glorious Purpose to you, granting you increased Cooldown
/// Acceleration for the duration." The cleave (effect 79) loses damage with the number of enemies beyond the cleave cap.
/// Combat logs (Y4wpb2f3gc8LqJDX fight 25, Gunde): the logs record the cast as instant (the game data's cast time is
/// the strike's travel; the hits land shortly after the cast), the cleave hits are logged as 160 too, and each
/// cleave hit on 26 other enemies is a fraction of the hit on the target, as the coefficients and the falloff give.
/// </summary>
public partial class FatedStrike : Ability
{
    /// <summary>Game data: tuning <c>PrimaryAttributeCoefficient</c> of weapon power 354.</summary>
    public const double Coefficient = 7.39;
    /// <summary>Game data: tuning <c>Cleave.PrimaryAttributeCoefficient</c> of weapon power 354.</summary>
    public const double CleaveCoefficient = 2.874;
    public const int GloriousPurposeEffectId = 80;

    /// <summary>Glorious Purpose: increased Cooldown Acceleration (the hero's other abilities; a weapon ability has its own).</summary>
    public static readonly AuraDef GloriousPurpose = AuraDef.Effect("Glorious Purpose", GloriousPurposeEffectId, Duration,
        modifiers: StatModifier.Increased(StatType.CooldownRecovery, CooldownRecoveryMultiplier));

    public FatedStrike(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(AbilityCooldown);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        DealDamage(ctx, target);
        var nearby = ctx.Outcomes.SelectTargets(DamageLogId, ctx.ActiveEnemiesExcept(target), ctx);
        double falloff = nearby.Count > DamageFalloffTargetCountThreshold ? Math.Sqrt((double)DamageFalloffTargetCountThreshold / nearby.Count) : 1.0;
        for (int i = 0; i < nearby.Count; i++) DealDamage(ctx, nearby[i], damageMultiplier: CleaveCoefficient / Coefficient * falloff);
        Owner.Auras.Apply(GloriousPurpose, ctx.CurrentTime);
    }
}
