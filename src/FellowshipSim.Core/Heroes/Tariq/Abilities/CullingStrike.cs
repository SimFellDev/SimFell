namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1228: "Instantly deal physical damage to target enemy. Culling Strike spends up to 10 Fury, increasing the
/// damage it deals by up to 400%. The more Fury it consumes, the higher the damage it deals. Execution: Can only be used
/// against enemies that have 30% or less health."
/// </summary>
public class CullingStrike : TariqAbility
{
    public const int AbilityId = 1228;
    public const string AbilityName = "Culling Strike";
    /// <summary>Placeholder CullStrikeDmg: 318.2025% of Strength.</summary>
    public const double Coefficient = 3.182025;
    /// <summary>Game data cooldown 6, its duration scaled by Haste.</summary>
    public const double CooldownSeconds = 6.0;
    public const double MaxFurySpent = 10.0;
    /// <summary>
    /// "+400%" at 10 Fury: 40% more damage per Fury spent. Combat logs (fXyzwGcQkTrLD34R fight 4): casts with 10 Fury or
    /// more deal about 5 times a no-Fury strike, and a cast with 8.5 Fury about 4.4 times.
    /// </summary>
    public const double DamagePerFury = 0.40;
    public const double ExecuteHealthFraction = 0.30;

    /// <summary>A Culling Strike was cast (Pneuma).</summary>
    public event Action<SimulationContext>? Struck;

    /// <summary>
    /// While this buff holds, the next Culling Strike works on any target, costs nothing and deals damage as if it spent
    /// <see cref="FreeStrikeFury"/> Fury, using up a stack (Executioner's Grin).
    /// </summary>
    public AuraDef? FreeStrike { get; internal set; }
    public double FreeStrikeFury { get; internal set; }

    public CullingStrike(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Power)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        // GA_Ink_LowHealthSingleTargetResourceDamage has no GlobalCooldownGameplayEffectClass. Combat logs (3 Tariq fights):
        // casts 5ms after other casts.
        IgnoresGcd = true;
    }

    private bool HasFreeStrike(SimTime now) => FreeStrike != null && Owner.Auras.HasAura(FreeStrike, now);

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        var target = Owner.CurrentTarget;
        if (target != null && target.HealthFraction > ExecuteHealthFraction && !HasFreeStrike(ctx.CurrentTime))
        {
            reason = explain ? $"{Name} needs a target at 30% health or less (target at {target.HealthPercent:0}%)" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Tariq;
        TriggerCooldown(ctx);
        double spent;
        if (HasFreeStrike(ctx.CurrentTime))
        {
            hero.Auras.ConsumeStack(FreeStrike!, ctx.CurrentTime);
            spent = FreeStrikeFury;
        }
        else
        {
            spent = Math.Min(MaxFurySpent, hero.ResourceAmount(Tariq.Fury));
            if (spent > 0) hero.SpendFury(spent, ctx);
        }
        DealDamage(ctx, target, damageMultiplier: 1.0 + DamagePerFury * spent);
        Struck?.Invoke(ctx);
    }
}
