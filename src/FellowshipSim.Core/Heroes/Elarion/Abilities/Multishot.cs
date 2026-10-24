namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1318: "Fire multiple arrows at your enemies, each dealing physical damage to them." Instant, 20 Focus; damage
/// falls off as sqrt(12/N) beyond 12 enemies (targetCountFalloff threshold 12). An Empowered Multishot (Skystrider's
/// Supremacy, Focused Expanse) "always fire at least 3 arrows regardless of how many targets are in range, costs 50% less
/// Focus, and deals magic damage".
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23, 496 casts): one arrow on each enemy around the
/// target, logged under 1318 empowered or not; an Empowered Multishot strikes 3 enemies where a plain one would strike 1
/// or 2, so its three arrows go to three enemies. The tooltip's "When your Highwind Arrow Ricochets to at least 3 enemies,
/// you gain 1 charge of Multishot" holds no limit: no log shows a Multishot charge (effect 1898), and Multishot is cast
/// with none.
/// </summary>
public class Multishot : ElarionAbility
{
    public const int AbilityId = 1318;
    public const string AbilityName = "Multishot";
    /// <summary>Placeholder Multishot: 371.91% of Agility.</summary>
    public const double Coefficient = 3.7191;
    public const double Cost = 20.0;
    public const double TargetThreshold = 12;
    public const double EmpoweredCostMultiplier = 0.5;

    /// <summary>
    /// The buffs that make the next Multishot Empowered, in the order they are used: Skystrider's Supremacy, then Focused
    /// Expanse. Each returns true when it empowers the cast at <c>now</c>, and uses up what it uses when told to.
    /// </summary>
    public List<EmpoweringBuff> EmpoweringBuffs { get; } = new();

    /// <summary>Multipliers on every Multishot's damage, read at the cast (Fervent Supremacy, Focused Expanse's on Empowered ones).</summary>
    public List<Func<SimTime, bool, double>> DamageMultipliers { get; } = new();

    /// <summary>A Multishot was cast; the flag is true when it was Empowered (Repeating Stars, Astronomer's Hail).</summary>
    public event Action<SimulationContext, bool>? Cast;

    public Multishot(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        FocusCost = Cost;
        AoeTargetThreshold = TargetThreshold;
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
        DealAoeDamage(ctx, enemies, damageMultiplier: multiplier);
        Cast?.Invoke(ctx, empowered);
    }
}
