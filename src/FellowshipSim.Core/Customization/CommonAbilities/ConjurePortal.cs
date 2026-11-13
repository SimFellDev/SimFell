namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 1504 (a relic ability): instant, with a 1.5s global cooldown of its own that Haste shortens, and a 120s cooldown.
/// The cast applies no effect to the caster; Portal Sickness (effect 2176, 20s, no stat modifier) comes from using the portal
/// (<see cref="RelicTeleport"/>). No damage. Game data (build 26320967). Its padding cooldown, row
/// `Hero.AbilityPaddingCooldown.Default.Duration` (0.3s), ends inside its global cooldown.
/// </summary>
public class ConjurePortal : Ability
{
    public const int AbilityId = 1504;
    public const string AbilityName = "Conjure Portal";
    public const double CooldownSeconds = 120.0;
    public const double GlobalCooldown = 1.5;

    public ConjurePortal(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Relic)
    {
        SetCooldown(CooldownSeconds);
        GlobalCooldownSeconds = GlobalCooldown;
        GlobalCooldownScalesWithHaste = true;
        // Game data: no Haste and no Cooldown Recovery reaches a relic cooldown.
        IgnoresCooldownRecovery = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
