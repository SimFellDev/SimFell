namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1116: "Interrupt the target's spellcasting and prevent it from casting for 4 sec. Must interrupt a spell, or it
/// will have no effect. Can be used during Global Cooldown". 12 second cooldown.
/// </summary>
public partial class Stagger : MeikoAbility
{
    public Stagger(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
