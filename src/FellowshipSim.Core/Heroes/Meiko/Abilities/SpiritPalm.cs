namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1101: "Deals 140.4% of Agility damage to target enemy. Grants access to Palm Technique finishers. Combo
/// Builder". Logged under 1101.
/// </summary>
public class SpiritPalm : ComboBuilder
{
    public const int AbilityId = 1101;
    public const string AbilityName = "Spirit Palm";
    /// <summary>Placeholder SpiritPalmDmg: 140.4% of Agility.</summary>
    public const double Coefficient = 1.404;

    public SpiritPalm(Meiko owner) : base(owner, AbilityId, AbilityName, Technique.Palm)
    {
        BaseCoefficient = Coefficient;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target) => DealDamage(ctx, target);
}
