namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 2239: "Instantly attack target enemy dealing physical damage to the target. Face Breaker deals part of its
/// damage to enemies near your target. Generates Fury."
/// </summary>
public partial class FaceBreaker : TariqAbility
{
    /// <summary>Game data: a share of maximum Fury.</summary>
    public const double FuryGain = 7.0;

    /// <summary>Ability 2241 ("Face Breaker"): the damage on enemies near the target.</summary>
    public const int SplashId = 2241;
    public const double SplashFraction = 0.5;
    /// <summary>Game data targetCountFalloff of ability 2241: effect 3162, with a threshold.</summary>
    public const double SplashTargetThreshold = 12;

    public FaceBreaker(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = StrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Tariq.GainFury(FuryGain, ctx, target);
        DealDamage(ctx, target, onLanded: (c, dealt) => Splash(c, target, dealt));
    }

    /// <summary>
    /// Combat logs (3 Tariq fights, 464 splashes): each nearby enemy takes <see cref="SplashFraction"/> of the hit dealt to the target, crit
    /// included; the splash never crits itself and loses damage beyond <see cref="SplashTargetThreshold"/> enemies.
    /// </summary>
    private void Splash(SimulationContext ctx, EnemyTarget target, double dealt)
    {
        if (dealt <= 0.0) return;
        var nearby = ctx.Outcomes.SelectTargets(SplashId, ctx.ActiveEnemiesExcept(target), ctx);
        if (nearby.Count == 0) return;
        double falloff = nearby.Count > SplashTargetThreshold ? Math.Sqrt(SplashTargetThreshold / nearby.Count) : 1.0;
        double damage = dealt * SplashFraction * falloff;
        for (int i = 0; i < nearby.Count; i++)
        {
            ctx.ApplyDamage(Owner, nearby[i], damage, SplashId, AbilityName, canCrit: false, 0.0, 1.0);
        }
    }
}
