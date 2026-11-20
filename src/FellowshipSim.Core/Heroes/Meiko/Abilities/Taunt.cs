namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1095: "Taunt the target, forcing it to attack you for 6 sec. Can be used during Global Cooldown". 8 second
/// cooldown, 2 charges.
/// </summary>
public partial class Taunt : MeikoAbility
{
    public Taunt(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, AbilityCharges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
