namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1123: "Quickly travel forward a short distance. Can be used during Global Cooldown". 20 second cooldown, 2
/// charges.
/// </summary>
public partial class Gust : MeikoAbility
{
    public Gust(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, AbilityCharges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
