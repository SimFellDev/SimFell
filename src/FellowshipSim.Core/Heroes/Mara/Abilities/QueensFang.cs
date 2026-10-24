namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1201: "Deals 357.2% of Agility to target enemy. Damage increased for each combo point spent." Costs 40
/// Energy. During Matriarch Macabre two shadowy clones repeat it (<see cref="MatriarchMacabre"/>).
/// </summary>
public class QueensFang : Finisher
{
    public const int AbilityId = 1201;
    public const string AbilityName = "Queen's Fang";
    public const double Coefficient = 3.572;
    public const double Cost = 40;

    public QueensFang(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        EnergyCost = Cost;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target, int points)
    {
        double multiplier = ComboPointMultiplier(points);
        DealDamage(ctx, target, damageMultiplier: multiplier);
        double castDamage = CastDamageMultiplier, castCrit = CurrentCast?.CritChance ?? 0.0;
        Mara.Matriarch.Imitate(ctx, 1, (c, power) =>
        {
            if (!target.IsSleeping) DealDamage(c, target, damageMultiplier: multiplier * castDamage * power, overrideName: MatriarchMacabre.CloneHitName(Name), extraCritChance: castCrit);
        });
    }

    /// <summary>A Queen's Fang struck by a shadowy clone of From the Shadows, with its own Combo Points.</summary>
    public void CloneStrike(SimulationContext ctx, EnemyTarget target, int points, double power) =>
        DealDamage(ctx, target, damageMultiplier: ComboPointMultiplier(points) * power, overrideName: MatriarchMacabre.CloneHitName(Name));
}
