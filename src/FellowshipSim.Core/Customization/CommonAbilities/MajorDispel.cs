namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 187 (a relic ability): "Instantly Dispel all harmful magic effects from you and your allies in a
/// large radius around you. Can be used during Global Cooldown." The cooldown is in the game data. No damage.
/// </summary>
public class MajorDispel : Ability
{
    public const int AbilityId = 187;
    public const string AbilityName = "Major Dispel";
    public const double CooldownSeconds = 120.0;

    public MajorDispel(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Relic)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
