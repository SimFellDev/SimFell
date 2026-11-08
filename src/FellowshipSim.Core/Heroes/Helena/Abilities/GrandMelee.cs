namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 1465: "For the next 10 seconds, Incite all enemies in a large radius around you, forcing them to only use Auto
/// Attacks and only attack you. All allies within the radius of Grand Melee take 20% reduced magic damage while it is
/// active. Can be used during Global Cooldown." Cooldown 150s.
/// </summary>
public partial class GrandMelee : HelenaAbility
{
    public GrandMelee(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
