namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1208: "Strike the target dealing 286.65% of Agility damage and apply a bleed dealing 98.41% of Agility every
/// 3 seconds for 12 seconds. The bleed's duration is increased per combo point spent" (3 seconds per point, 30 at 6).
/// Costs 20 Energy; 2s cooldown. The bleed is <see cref="Hemorrhage"/>.
/// </summary>
public class HemorrhagingStrike : Finisher
{
    public const int AbilityId = 1208;
    public const string AbilityName = "Hemorrhaging Strike";
    public const double Coefficient = 2.8665;
    public const double Cost = 20;
    public const double CooldownSeconds = 2.0;
    public const double BleedSecondsPerComboPoint = 3.0;

    /// <summary>Enemies besides the target that the cast's bleed goes on (Gushing Blood during Maiden of Death).</summary>
    public List<Func<SimulationContext, EnemyTarget, IEnumerable<EnemyTarget>>> ExtraBleedTargets { get; } = new();

    /// <summary>The strike hit its target, before the bleed goes on, with the Combo Points spent (Hemotoxin).</summary>
    public event Action<SimulationContext, EnemyTarget, int>? Struck;

    public HemorrhagingStrike(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        EnergyCost = Cost;
        SetCooldown(CooldownSeconds);
    }

    public static double BleedSeconds(int points) => Hemorrhage.BaseDurationSeconds + BleedSecondsPerComboPoint * points;

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
