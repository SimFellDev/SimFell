namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1125: "Create a Cyclone of strong winds that instantly pulls in all enemies within the radius to you. Can be used
/// during Global Cooldown". 60 second cooldown.
/// </summary>
public class Cyclone : MeikoAbility
{
    public const int AbilityId = 1125;
    public const string AbilityName = "Cyclone";
    public const double CooldownSeconds = 60.0;

    public Cyclone(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Control)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
