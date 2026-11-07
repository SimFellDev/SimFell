namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1843: "Taunt the target, forcing it to attack you for 6 sec. Can be used during Global Cooldown." Cooldown 8s,
/// 2 charges.
/// </summary>
public class Taunt : XavianAbility
{
    public const int AbilityId = 1843;
    public const string AbilityName = "Taunt";
    public const double CooldownSeconds = 8.0;
    public const int Charges = 2;

    public Taunt(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds, Charges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
