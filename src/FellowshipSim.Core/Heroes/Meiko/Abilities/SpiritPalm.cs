namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1101: "Deals 140.4% of Agility damage to target enemy. Grants access to Palm Technique finishers. Combo
/// Builder". Logged under 1101.
/// </summary>
public partial class SpiritPalm : ComboBuilder
{
    public SpiritPalm(Meiko owner) : base(owner, AbilityId, AbilityName, Technique.Palm)
    {
        BaseCoefficient = AgilityCoefficient;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target) => DealDamage(ctx, target);
}
