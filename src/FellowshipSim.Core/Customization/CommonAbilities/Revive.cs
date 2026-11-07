namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 22 (a relic ability): "Revive target dead ally. Can be used in combat." The cast time is hasted;
/// the cast time and cooldown are in the game data. No damage. UNVERIFIED: whether it starts the global cooldown.
/// </summary>
public class Revive : Ability
{
    public const int AbilityId = 22;
    public const string AbilityName = "Revive";
    public const double CastTimeSeconds = 2.0;
    public const double CooldownSeconds = 300.0;

    public Revive(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Relic)
    {
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        SetCooldown(CooldownSeconds);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
