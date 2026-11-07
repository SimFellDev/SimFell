namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1100: "Deals 140.4% of Agility damage to target enemy. Grants access to Fist Technique finishers. Combo
/// Builder". Logged under 1100.
/// </summary>
public class EarthFist : ComboBuilder
{
    public const int AbilityId = 1100;
    public const string AbilityName = "Earth Fist";
    /// <summary>Placeholder EarthFistDmg: 140.4% of Agility.</summary>
    public const double Coefficient = 1.404;

    public EarthFist(Meiko owner) : base(owner, AbilityId, AbilityName, Technique.Fist)
    {
        BaseCoefficient = Coefficient;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target) => DealDamage(ctx, target);
}
