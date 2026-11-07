namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 157 (weapon power 351): "Instantly deal fire damage to all enemies within a radius. You gain
/// a stack of Sundering Wrath for each enemy hit up to a maximum, granting you additional critical strike
/// chance per stack for a time. Can be cast during Global Cooldown." Damage falls off with the number of enemies
/// beyond the target cap (game data targetCountFalloff). Sundering Wrath is effect 75 (CritChance, stacking).
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, Mara): the hits are logged as 157, and 13 enemies hit give the maximum stacks.
/// </summary>
public class SahrilsWrath : Ability
{
    public const int AbilityId = 157;
    public const string AbilityName = "Sahril's Wrath";
    public const double Coefficient = 21.09;
    public const double CooldownSeconds = 120.0;
    public const double TargetThreshold = 1;
    public const int SunderingWrathEffectId = 75;
    public const double SunderingWrathSeconds = 20.0;
    public const int SunderingWrathMaxStacks = 5;
    public const double CritChancePerStack = 0.06;

    public static readonly AuraDef SunderingWrath = AuraDef.Effect("Sundering Wrath", SunderingWrathEffectId, SunderingWrathSeconds,
        SunderingWrathMaxStacks, StatModifier.Add(StatType.CritChance, CritChancePerStack));

    public SahrilsWrath(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        AoeTargetThreshold = TargetThreshold;
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        if (struck.Count == 0) return;
        double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++) DealDamage(ctx, struck[i], damageMultiplier: falloff);
        Owner.Auras.Apply(SunderingWrath, ctx.CurrentTime, stacks: Math.Min(struck.Count, SunderingWrathMaxStacks));
    }
}
