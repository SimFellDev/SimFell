namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 959: "Interact with the campfire to update the spawn point of your party!" The cast time is not hasted (game data;
/// combat logs: begincast to cast matches the cast time). No damage. UNVERIFIED: whether it starts the global cooldown.
/// </summary>
public class MakingCamp : Ability
{
    public const int AbilityId = 959;
    public const string AbilityName = "Making Camp";
    public const double CastTimeSeconds = 2.0;

    public MakingCamp(Hero owner) : base(owner, AbilityId, AbilityName)
    {
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        CastTimeScalesWithHaste = false;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
