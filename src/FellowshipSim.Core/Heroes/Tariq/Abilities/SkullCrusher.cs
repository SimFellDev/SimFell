namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1229: "Instantly deal physical damage to target enemy. Skull Crusher triggers a shortened Global Cooldown.
/// Thunder Call: Skull Crusher beckons a thunder bolt to strike the target from above, dealing lightning damage." Costs
/// Fury.
/// </summary>
public partial class SkullCrusher : TariqAbility
{
    /// <summary>
    /// Game data cooldown. Combat logs (fXyzwGcQkTrLD34R fight 4): Skull Crushers come closer together at higher haste,
    /// so haste shortens it.
    /// </summary>
    public const double CooldownSeconds = 1.0;
    /// <summary>
    /// <summary>Combat logs: the thunder bolt is logged under the ability's own ID, as the physical hit is.</summary>
    public const string LightningName = "Thunder Call: Skull Crusher";

    /// <summary>The physical hit landed on <paramref name="target"/>, with the damage it dealt (Sledgehammer).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? Landed;

    public SkullCrusher(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        BaseCoefficient = Constants.StrengthCoefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        // Game data: a shorter coefficient on the hasted global cooldown than the shared one. Combat logs (3 Tariq fights):
        // the next cast comes a shorter global cooldown after a Skull Crusher.
        GlobalCooldownSeconds = AbilityGlobalCooldown;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason) =>
        CanPay(ctx, FocusedWrath.Cost(Tariq, AbilityCost, ctx.CurrentTime), explain, out reason);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Tariq;
        TriggerCooldown(ctx);
        double multiplier = FocusedWrath.Spend(hero, AbilityCost, ctx);
        DealDamage(ctx, target, damageMultiplier: multiplier, onLanded: (c, dealt) => Landed?.Invoke(c, target, dealt));
        if (hero.IsCharged(ctx.CurrentTime))
        {
            DealDamage(ctx, target, damageMultiplier: multiplier * Constants.LightningStrengthCoefficient / Constants.StrengthCoefficient, overrideName: LightningName,
                extraCritChance: hero.LightningCritChance);
        }
    }
}
