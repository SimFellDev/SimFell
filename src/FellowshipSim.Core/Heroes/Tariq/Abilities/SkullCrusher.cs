namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1229: "Instantly deal physical damage to target enemy. Skull Crusher triggers a shortened Global Cooldown.
/// Thunder Call: Skull Crusher beckons a thunder bolt to strike the target from above, dealing lightning damage." Costs
/// 25 Fury.
/// </summary>
public class SkullCrusher : TariqAbility
{
    public const int AbilityId = 1229;
    public const string AbilityName = "Skull Crusher";
    /// <summary>Placeholder SkullCrushDmg: 1160% of Strength.</summary>
    public const double Coefficient = 11.6;
    /// <summary>Placeholder SkullCrushLightnDmg: 406% of Strength.</summary>
    public const double LightningCoefficient = 4.06;
    public const double FuryCost = 25.0;
    /// <summary>
    /// Game data cooldown 1. Combat logs (fXyzwGcQkTrLD34R fight 4): Skull Crushers 0.835s apart at 20% haste, so haste
    /// shortens it.
    /// </summary>
    public const double CooldownSeconds = 1.0;
    /// <summary>
    /// GE_GlobalCooldown_TariqHeavySingleTarget: coefficient 1 on the hasted global cooldown, against 1.5 for the shared
    /// one. Combat logs (3 Tariq fights): the next cast comes 2/3 of the usual global cooldown after a Skull Crusher.
    /// </summary>
    public const double GlobalCooldown = 1.0;
    /// <summary>Combat logs: the thunder bolt is logged under the ability's own ID, as the physical hit is.</summary>
    public const string LightningName = "Thunder Call: Skull Crusher";

    /// <summary>The physical hit landed on <paramref name="target"/>, with the damage it dealt (Sledgehammer).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? Landed;

    public SkullCrusher(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Power)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        GlobalCooldownSeconds = GlobalCooldown;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason) =>
        CanPay(ctx, FocusedWrath.Cost(Tariq, FuryCost, ctx.CurrentTime), explain, out reason);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Tariq;
        TriggerCooldown(ctx);
        double multiplier = FocusedWrath.Spend(hero, FuryCost, ctx);
        DealDamage(ctx, target, damageMultiplier: multiplier, onLanded: (c, dealt) => Landed?.Invoke(c, target, dealt));
        if (hero.IsCharged(ctx.CurrentTime))
        {
            DealDamage(ctx, target, damageMultiplier: multiplier * LightningCoefficient / Coefficient, overrideName: LightningName,
                extraCritChance: hero.LightningCritChance);
        }
    }
}
