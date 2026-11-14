namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1125: "Create a Cyclone of strong winds that instantly pulls in all enemies within the radius to you. Can be used
/// during Global Cooldown". 60 second cooldown.
/// </summary>
public partial class Cyclone : MeikoAbility
{
    public Cyclone(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
