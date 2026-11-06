namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1129: Agility damage to the target enemy, with more damage when attacking from behind. Generates Combo Points,
/// more on a Critical Strike, and costs Energy. In Stealth the Stealth Backstab
/// (<see cref="StealthBackstab"/>) replaces it.
/// </summary>
public class Backstab : MaraAbility
{
    public const int AbilityId = 1129;
    public const string AbilityName = "Backstab";
    public const double Coefficient = 0.996;
    public const double Cost = 20;
    public const double ComboPointsGained = 2;
    public const double ComboPointsOnCrit = 3;

    /// <summary>The bonus damage when attacking from behind: a sim attacks from behind. UNVERIFIED.</summary>
    public const double BehindMultiplier = 1.4;

    /// <summary>Backstab hit an enemy (Hemotoxin, Caustic Wounds).</summary>
    public event Action<SimulationContext, EnemyTarget>? Struck;

    public Backstab(Mara owner) : this(owner, AbilityId, AbilityName)
    {
        UnusableInStealth = true;
    }

    /// <summary>For the Stealth Backstab: it is built on Backstab.</summary>
    protected Backstab(Mara owner, int id, string name) : base(owner, id, name, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        EnergyCost = Cost;
        BreaksStealth = true;
        Hit += (in hit) =>
        {
            if (hit.SimContext is { } ctx) GenerateComboPoints(ctx, hit.IsCrit ? ComboPointsOnCrit : ComboPointsGained, hit.Target);
        };
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Pay(ctx);
        DealDamage(ctx, target, damageMultiplier: BehindMultiplier);
        Struck?.Invoke(ctx, target);
    }
}
