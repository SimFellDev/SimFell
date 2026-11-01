namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1095: "Taunt the target, forcing it to attack you for 6 sec. Can be used during Global Cooldown". 8 second
/// cooldown, 2 charges. It deals no damage.
/// </summary>
public class Taunt : MeikoAbility
{
    public const int AbilityId = 1095;
    public const string AbilityName = "Taunt";
    public const double CooldownSeconds = 8.0;
    public const int Charges = 2;

    public Taunt(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds, Charges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
