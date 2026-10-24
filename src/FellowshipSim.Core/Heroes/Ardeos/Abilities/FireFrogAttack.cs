namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// A Fire Frog's leap, logged under effect 1827: "dealing 86.3052% of Intellect fire damage and burning them for an
/// additional 100% of the initial damage dealt over 12 seconds" (<see cref="FireFrogsDot"/>).
/// </summary>
public sealed class FireFrogAttack : Ability
{
    public const int DamageEffectId = 1827;
    public const string AttackName = "Fire Frogs";
    public const double Coefficient = 0.863052;
    public const double DotFractionOfHit = 1.0;

    public FireFrogsDot Dot { get; }

    /// <summary>Share of each hit added to its damage over time (Frog Squad raises it).</summary>
    public double DotFraction { get; set; } = DotFractionOfHit;

    public FireFrogAttack(Ardeos owner) : base(owner, LogIds.Effect(DamageEffectId), AttackName)
    {
        BaseCoefficient = Coefficient;
        Dot = new FireFrogsDot(owner);
        Hit += (in DamageContext hit) =>
        {
            if (hit.Target != null && hit.SimContext != null && hit.Amount > 0)
            {
                owner.Dots.Apply(hit.SimContext, Dot, hit.Target, pool: hit.Amount * DotFraction);
            }
        };
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
