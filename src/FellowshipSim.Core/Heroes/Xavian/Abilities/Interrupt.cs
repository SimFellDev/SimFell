namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1844: "Interrupt the target's spellcasting and prevent it from casting for 4 sec. ... Can be used during Global
/// Cooldown." Cooldown 12s.
/// </summary>
public partial class Interrupt : XavianAbility
{
    public Interrupt(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
