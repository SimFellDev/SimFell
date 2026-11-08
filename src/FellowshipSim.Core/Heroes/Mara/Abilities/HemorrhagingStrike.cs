namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1208: strikes the target for Agility damage and applies a bleed. The bleed's duration is increased per combo point
/// spent (<see cref="DurationIncreasePerComboPoint"/>). Costs Energy. The bleed is <see cref="Hemorrhage"/>.
/// No cooldown, so the global cooldown is the limit on a recast (T2qLPpmMkrAtJ9B1 fight 18 @ 1062.844 and 1063.878). In
/// game data the cooldown duration has a row with no value, table or
/// registry, and the ability has no cooldown effect class. Both take their defaults, a value of zero and null.
/// The evaluator returns that value and leaves the row's own value unread, and the cooldown application makes no spec.
/// </summary>
public partial class HemorrhagingStrike : Finisher
{
    public const double Coefficient = 2.8665;

    /// <summary>Enemies besides the target that the cast's bleed goes on (Gushing Blood during Maiden of Death).</summary>
    public List<Func<SimulationContext, EnemyTarget, IEnumerable<EnemyTarget>>> ExtraBleedTargets { get; } = new();

    /// <summary>The strike hit its target, before the bleed goes on, with the Combo Points spent (Hemotoxin).</summary>
    public event Action<SimulationContext, EnemyTarget, int>? Struck;

    public HemorrhagingStrike(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        EnergyCost = AbilityCost;
    }

    public static double BleedSeconds(int points) => Hemorrhage.BaseDurationSeconds + DurationIncreasePerComboPoint * points;

    protected override void Strike(SimulationContext ctx, EnemyTarget target, int points)
    {
        DealDamage(ctx, target);
        Struck?.Invoke(ctx, target, points);
        double seconds = BleedSeconds(points);
        Mara.Hemorrhage.ApplyTo(ctx, target, seconds);
        foreach (var extra in ExtraBleedTargets)
        {
            foreach (var enemy in extra(ctx, target)) Mara.Hemorrhage.ApplyTo(ctx, enemy, seconds);
        }
    }
}
