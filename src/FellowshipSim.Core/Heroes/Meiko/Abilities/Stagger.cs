namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1116: "Interrupt the target's spellcasting and prevent it from casting for 4 sec. Must interrupt a spell, or it
/// will have no effect. Can be used during Global Cooldown". 12 second cooldown. It deals no damage.
/// </summary>
public class Stagger : MeikoAbility
{
    public const int AbilityId = 1116;
    public const string AbilityName = "Stagger";
    public const double CooldownSeconds = 12.0;

    public Stagger(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
