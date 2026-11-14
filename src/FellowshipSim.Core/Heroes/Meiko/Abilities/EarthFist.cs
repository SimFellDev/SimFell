namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1100: "Deals 140.4% of Agility damage to target enemy. Grants access to Fist Technique finishers. Combo
/// Builder". Logged under 1100.
/// </summary>
public partial class EarthFist : ComboBuilder
{
    public EarthFist(Meiko owner) : base(owner, AbilityId, AbilityName, Technique.Fist)
    {
        TargetedOffensive = true;
        BaseCoefficient = Constants.AgilityCoefficient;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target) => DealDamage(ctx, target);
}
