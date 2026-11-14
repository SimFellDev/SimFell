namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1318: "Fire multiple arrows at your enemies, each dealing physical damage to them." Instant, costs Focus; damage
/// falls off beyond a target count (targetCountFalloff, <see cref="Constants.TargetCountThreshold"/>). An Empowered Multishot (Skystrider's
/// Supremacy, Focused Expanse) always fires a minimum number of arrows regardless of how many targets are in range, costs less
/// Focus, and deals magic damage.
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23, 496 casts): one arrow on each enemy around the
/// target, logged under 1318 empowered or not. An Empowered Multishot with fewer enemies around than its minimum arrows fires its other
/// arrows at them again (67 casts with 1 enemy; 12 casts with 2 enemies). The tooltip's line that a Highwind Arrow Ricochet
/// to enough enemies grants a charge of Multishot holds no limit: no log shows a Multishot charge (effect 1898), and Multishot is cast
/// with none.
/// </summary>
public partial class Multishot : ElarionAbility
{
    public const double EmpoweredCostMultiplier = 0.5;
    public const int EmpoweredMinimumArrows = 3;

    /// <summary>
    /// The buffs that make the next Multishot Empowered, in the order they are used: Skystrider's Supremacy, then Focused
    /// Expanse. Each returns true when it empowers the cast at <c>now</c>, and uses up what it uses when told to.
    /// </summary>
    public List<EmpoweringBuff> EmpoweringBuffs { get; } = new();

    /// <summary>Multipliers on every Multishot's damage, read at the cast (Fervent Supremacy, Focused Expanse's on Empowered ones).</summary>
    public List<Func<SimTime, bool, double>> DamageMultipliers { get; } = new();

    /// <summary>A Multishot was cast; the flag is true when it was Empowered (Repeating Stars, Astronomer's Hail).</summary>
    public event Action<SimulationContext, bool>? Cast;

    public Multishot(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.AgilityCoefficient;
        FocusCost = AbilityCost;
        AoeTargetThreshold = Constants.TargetCountThreshold;
    }

    /// <summary>A buff that empowers Multishot: whether it is up, and using it up for one cast.</summary>
    public sealed record EmpoweringBuff(Func<SimTime, bool> IsUp, Action<SimTime> Use);

    public bool IsEmpowered(SimTime now) => EmpoweringBuffs.Any(b => b.IsUp(now));

    public override double CostAt(SimTime now) => base.CostAt(now) * (IsEmpowered(now) ? EmpoweredCostMultiplier : 1.0);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        Pay(ctx, CostAt(now));
        var empowering = EmpoweringBuffs.FirstOrDefault(b => b.IsUp(now));
        bool empowered = empowering != null;
        double multiplier = 1.0;
        foreach (var m in DamageMultipliers) multiplier *= m(now, empowered);
        empowering?.Use(now);
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        double falloff = struck.Count > Constants.TargetCountThreshold ? Math.Sqrt((double)Constants.TargetCountThreshold / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++) DealDamage(ctx, struck[i], damageMultiplier: multiplier * falloff);
        // The arrows past one per enemy go to the enemies struck, in turn.
        if (empowered && struck.Count > 0)
        {
            for (int i = struck.Count; i < EmpoweredMinimumArrows; i++) DealDamage(ctx, struck[i % struck.Count], damageMultiplier: multiplier);
        }
        Cast?.Invoke(ctx, empowered);
    }
}
