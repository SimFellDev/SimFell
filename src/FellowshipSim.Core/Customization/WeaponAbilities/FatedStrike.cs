namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 160 (weapon power 354): "Strike target enemy, dealing damage to it and cleaving for damage
/// to nearby enemies. Fated Strike applies Glorious Purpose to you, granting you increased Cooldown
/// Acceleration for the duration." The cleave (effect 79) loses damage with the number of enemies beyond the cleave cap.
/// Combat logs (Y4wpb2f3gc8LqJDX fight 25, Gunde): the logs record the cast as instant (the game data's cast time is
/// the strike's travel; the hits land shortly after the cast), the cleave hits are logged as 160 too, and each
/// cleave hit on 26 other enemies is a fraction of the hit on the target, as the coefficients and the falloff give.
/// </summary>
public class FatedStrike : Ability
{
    public const int AbilityId = 160;
    public const string AbilityName = "Fated Strike";
    public const double Coefficient = 7.39;
    public const double CleaveCoefficient = 2.874;
    public const double CooldownSeconds = 60.0;
    public const double CleaveTargetThreshold = 3;
    public const int GloriousPurposeEffectId = 80;
    public const double GloriousPurposeSeconds = 6.0;
    /// <summary>Effect 80: CooldownRecovery Multiplicitive.</summary>
    public const double GloriousPurposeRecovery = 3.0;

    /// <summary>Glorious Purpose: increased Cooldown Acceleration (the hero's other abilities; a weapon ability has its own).</summary>
    public static readonly AuraDef GloriousPurpose = AuraDef.Effect("Glorious Purpose", GloriousPurposeEffectId, GloriousPurposeSeconds,
        modifiers: StatModifier.Increased(StatType.CooldownRecovery, GloriousPurposeRecovery));

    public FatedStrike(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        DealDamage(ctx, target);
        var nearby = ctx.Outcomes.SelectTargets(DamageLogId, ctx.ActiveEnemiesExcept(target), ctx);
        double falloff = nearby.Count > CleaveTargetThreshold ? Math.Sqrt(CleaveTargetThreshold / nearby.Count) : 1.0;
        for (int i = 0; i < nearby.Count; i++) DealDamage(ctx, nearby[i], damageMultiplier: CleaveCoefficient / Coefficient * falloff);
        Owner.Auras.Apply(GloriousPurpose, ctx.CurrentTime);
    }
}
