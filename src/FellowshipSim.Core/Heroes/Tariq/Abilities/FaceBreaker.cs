namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 2239: "Instantly attack target enemy dealing physical damage to the target. Face Breaker deals 50% of its
/// damage to enemies near your target. Generates 7 Fury."
/// </summary>
public class FaceBreaker : TariqAbility
{
    public const int AbilityId = 2239;
    public const string AbilityName = "Face Breaker";
    /// <summary>Placeholder FaceBreakDmg: 618.426% of Strength.</summary>
    public const double Coefficient = 6.18426;
    /// <summary>Game data cooldown 6, its duration scaled by Haste.</summary>
    public const double CooldownSeconds = 6.0;
    /// <summary>Ink.SingleTargetHeavyNonSpender.ResourceGain: 7% of maximum Fury.</summary>
    public const double FuryGain = 7.0;

    /// <summary>Ability 2241 ("Face Breaker"): the damage on enemies near the target.</summary>
    public const int SplashId = 2241;
    public const double SplashFraction = 0.5;
    /// <summary>Game data targetCountFalloff of ability 2241: effect 3162, threshold 12.</summary>
    public const double SplashTargetThreshold = 12;

    public FaceBreaker(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Tariq.GainFury(FuryGain, ctx, target);
        DealDamage(ctx, target, onLanded: (c, dealt) => Splash(c, target, dealt));
    }

    /// <summary>
    /// Combat logs (3 Tariq fights, 464 splashes): each nearby enemy takes exactly 50% of the hit dealt to the target, crit
    /// included; the splash never crits itself and loses damage as sqrt(12/N) beyond 12 enemies.
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
