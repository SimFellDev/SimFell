namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1228: "Instantly deal physical damage to target enemy. Culling Strike spends up to a maximum of Fury, increasing the
/// damage it deals. The more Fury it consumes, the higher the damage it deals. Execution: Can only be used
/// against enemies that have low health."
/// </summary>
public partial class CullingStrike : TariqAbility
{
    public const double MaxFurySpent = 10.0;
    /// <summary>
    /// The tooltip's bonus at <see cref="MaxFurySpent"/> Fury is a share of damage per Fury spent. Combat logs
    /// (fXyzwGcQkTrLD34R fight 4): damage grows with the Fury spent, up to the cap.
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

    public CullingStrike(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.StrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        // Game data: no global cooldown effect. Combat logs (3 Tariq fights):
        // casts right after other casts.
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
