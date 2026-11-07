namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1033: "Rime commands Frost Swallows to attack her target." The swallows every Winter Orb sends, and Cascading
/// Blitz's. Each hit scales with Spirit. Flight of the Navir's circling swallows are the same attack logged under their
/// own ID (<see cref="FlightOfTheNavir.CirclingSwallows"/>).
/// </summary>
public class FrostSwallows : RimeAbility
{
    public const int AbilityId = 1033;
    public const string AbilityName = "Frost Swallows";
    public const double Coefficient = 0.469;
    /// <summary>Tooltip: each percent of Spirit raises Frost Swallows' damage by this many percent.</summary>
    public const double DamagePerSpiritPercent = 1.5;

    public FrostSwallows(Rime owner) : this(owner, AbilityId, Coefficient) { }

    /// <summary>Frost Swallows whose damage combat logs report under <paramref name="damageLogId"/>.</summary>
    protected FrostSwallows(Rime owner, int damageLogId, double coefficient) : base(owner, damageLogId, AbilityName)
    {
        BaseCoefficient = coefficient;
    }

    public override double BaseDamage(SimulationContext ctx, EnemyTarget? target, double damageMultiplier = 1.0) =>
        base.BaseDamage(ctx, target, damageMultiplier) * (1.0 + DamagePerSpiritPercent * Owner.SpiritPercent(ctx.CurrentTime));

    public void Attack(SimulationContext ctx, EnemyTarget target, int count)
    {
        if (target == null) return;
        for (int i = 0; i < count; i++) DealDamage(ctx, target);
    }

    /// <summary>Swallows that pick their own targets among the enemies in combat.</summary>
    public void AttackEnemies(SimulationContext ctx, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var target = ctx.Outcomes.ChooseTarget(DamageLogId, ctx.ActiveEnemies, ctx);
            if (target != null) Attack(ctx, target, 1);
        }
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => Attack(ctx, target, 1);
}
