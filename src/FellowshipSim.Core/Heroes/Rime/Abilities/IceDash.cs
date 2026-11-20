namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1024: "Quickly travel forward a short distance. Can be used during Global Cooldown." Charges recover
/// through cooldown recovery (game data). No damage. The game data gives it a short channel; combat logs show only a
/// cast event for it (no channel events), and it stops no channel.
/// </summary>
public partial class IceDash : RimeAbility
{
    public IceDash(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, AbilityCharges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
