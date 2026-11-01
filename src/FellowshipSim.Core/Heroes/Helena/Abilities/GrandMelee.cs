namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 1465: "For the next 10 seconds, Incite all enemies in a large radius around you, forcing them to only use Auto
/// Attacks and only attack you. All allies within the radius of Grand Melee take 20% reduced magic damage while it is
/// active. Can be used during Global Cooldown." Cooldown 150s. No damage.
/// </summary>
public class GrandMelee : HelenaAbility
{
    public const int AbilityId = 1465;
    public const string AbilityName = "Grand Melee";
    public const double CooldownSeconds = 150.0;

    public GrandMelee(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Control)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
