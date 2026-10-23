namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 160 (weapon power 354): "Strike target enemy, dealing 739% damage to it and cleaving for 287.4% damage
/// to nearby enemies. Fated Strike applies Glorious Purpose to you for 6 seconds, granting you +200% increased Cooldown
/// Acceleration for the duration." Cooldown 60s. The cleave (effect 79) loses damage as sqrt(3/N) beyond 3 enemies.
/// Combat logs (Y4wpb2f3gc8LqJDX fight 25, Gunde): the logs record the cast as instant (the game data's 0.15s cast is
/// the strike's travel; the hits land 0.11-0.14s after the cast), the cleave hits are logged as 160 too, and each
/// cleave hit on 26 other enemies is 0.13x the hit on the target: 2.874 / 7.39 x sqrt(3/26).
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
    public const double GloriousPurposeAcceleration = 2.0;

    /// <summary>Glorious Purpose: +200% Cooldown Acceleration (the hero's other abilities; a weapon ability has its own).</summary>
    public static readonly AuraDef GloriousPurpose = AuraDef.Effect("Glorious Purpose", GloriousPurposeEffectId, GloriousPurposeSeconds);

    public FatedStrike(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        owner.CooldownAccelerations.Add((_, now) => owner.Auras.HasAura(GloriousPurpose, now) ? GloriousPurposeAcceleration : 0.0);
        owner.CooldownAccelerationAuraKeys.Add(GloriousPurpose.Key);
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
