namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1082: "Split your soul into the Sibling of Wind and the Sibling of Earth for up to 20 seconds. You are in control
/// of the Sibling of Wind. SIBLING OF EARTH: While active, the Sibling of Earth forces all enemies within a 1000 radius to
/// attack it." 180 second cooldown.
/// </summary>
public partial class TwinSoulsBulwark : MeikoAbility
{
    public TwinSoulsBulwark(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        // Combat logs (7Zmfx9gq6HjvThzc fight 2 @ 14.326): Spirit Palm follows 0.57s after the cast.
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
