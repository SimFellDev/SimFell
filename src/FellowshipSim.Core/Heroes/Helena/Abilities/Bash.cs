namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 976: "Interrupt the target's spellcasting and prevent it from casting for 4 sec. Must interrupt a spell, or it
/// will have no effect. Can be used during Global Cooldown." Cooldown 12s. No damage.
/// </summary>
public class Bash : HelenaAbility
{
    public const int AbilityId = 976;
    public const string AbilityName = "Bash";
    public const double CooldownSeconds = 12.0;

    public Bash(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
