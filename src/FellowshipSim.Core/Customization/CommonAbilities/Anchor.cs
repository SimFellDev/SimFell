namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 597 "Anchor" (a shared ability): cast time 0.667s, not hasted, cooldown 0.667s (game data; combat logs: begincast to
/// cast 0.668-0.671s). No damage. UNVERIFIED: whether it starts the global cooldown.
/// </summary>
public class Anchor : Ability
{
    public const int AbilityId = 597;
    public const string AbilityName = "Anchor";
    public const double CastTimeSeconds = 0.667;
    public const double CooldownSeconds = 0.667;

    public Anchor(Hero owner) : base(owner, AbilityId, AbilityName)
    {
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        CastTimeScalesWithHaste = false;
        SetCooldown(CooldownSeconds);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
