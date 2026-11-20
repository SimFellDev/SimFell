namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1843: "Taunt the target, forcing it to attack you for 6 sec. Can be used during Global Cooldown." Cooldown 8s,
/// 2 charges.
/// </summary>
public partial class Taunt : XavianAbility
{
    public Taunt(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, AbilityCharges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
