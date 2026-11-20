namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 966: "Taunt the target, forcing it to attack you for 6 sec. Can be used during Global Cooldown." 2 charges, 8s
/// each.
/// </summary>
public partial class Taunt : HelenaAbility
{
    public Taunt(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, AbilityCharges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
