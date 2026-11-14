namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1105, a Palm Technique finisher (Spirit Palm then Wind Kick): "Strike target enemy with both palms, dealing
/// 1253.2% of Agility damage. Grants Spirited Strikes for 30 seconds. SPIRITED STRIKES: +25% Magic Damage Reduction, +5%
/// Parry. Can be used during Global Cooldown". Logged under 1105.
/// </summary>
public class DoublePalmStrike : MeikoFinisher
{
    public const int AbilityId = 1105;
    public const string AbilityName = "Double Palm Strike";
    /// <summary>Placeholder DoublePalmDmg: 1253.2% of Agility.</summary>
    public const double Coefficient = 12.532;

    /// <summary>A Double Palm Strike struck its enemy (Forbidden Technique).</summary>
    public event Action<SimulationContext, EnemyTarget>? Struck;

    public DoublePalmStrike(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core, Technique.Palm, Technique.Kick)
    {
        TargetedOffensive = true;
        BaseCoefficient = Coefficient;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target)
    {
        DealDamage(ctx, target);
        Struck?.Invoke(ctx, target);
    }
}
