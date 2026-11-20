namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 157 (weapon power 351): "Instantly deal fire damage to all enemies within a radius. You gain
/// a stack of Sundering Wrath for each enemy hit up to a maximum, granting you additional critical strike
/// chance per stack for a time. Can be cast during Global Cooldown." Damage falls off with the number of enemies
/// beyond the target cap (game data targetCountFalloff). Sundering Wrath is effect 75 (CritChance, stacking).
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, Mara): the hits are logged as 157, and 13 enemies hit give the maximum stacks.
/// </summary>
public partial class SahrilsWrath : Ability
{
    public const int SunderingWrathEffectId = 75;

    public static readonly AuraDef SunderingWrath = AuraDef.Effect("Sundering Wrath", SunderingWrathEffectId, Constants.BuffDuration,
        Constants.BuffMaxStacks, StatModifier.Add(StatType.CritChance, Constants.BuffCriticalStrikeBonusPerStack));

    public SahrilsWrath(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamagePrimaryAttributeCoefficient;
        SetCooldown(AbilityCooldown);
        AoeTargetThreshold = Constants.DamageDamageFalloffTargetCountThreshold;
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        if (struck.Count == 0) return;
        double falloff = struck.Count > Constants.DamageDamageFalloffTargetCountThreshold ? Math.Sqrt((double)Constants.DamageDamageFalloffTargetCountThreshold / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++) DealDamage(ctx, struck[i], damageMultiplier: falloff);
        Owner.Auras.Apply(SunderingWrath, ctx.CurrentTime, stacks: Math.Min(struck.Count, Constants.BuffMaxStacks));
    }
}
