namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1844: "Interrupt the target's spellcasting and prevent it from casting for 4 sec. ... Can be used during Global
/// Cooldown." Cooldown 12s.
/// </summary>
public class Interrupt : XavianAbility
{
    public const int AbilityId = 1844;
    public const string AbilityName = "Interrupt";
    public const double CooldownSeconds = 12.0;

    public Interrupt(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
