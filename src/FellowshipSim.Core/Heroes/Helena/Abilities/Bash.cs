namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 976: "Interrupt the target's spellcasting and prevent it from casting for 4 sec. Must interrupt a spell, or it
/// will have no effect. Can be used during Global Cooldown." Cooldown 12s.
/// </summary>
public partial class Bash : HelenaAbility
{
    public Bash(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
