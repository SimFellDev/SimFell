namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 1506 "Relic Teleport" (a shared ability): the game data gives no cast time or cooldown, and combat logs show only a
/// cast event for it. No damage. UNVERIFIED: whether it starts the global cooldown.
/// </summary>
public class RelicTeleport : Ability
{
    public const int AbilityId = 1506;
    public const string AbilityName = "Relic Teleport";

    public RelicTeleport(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Relic) { }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
