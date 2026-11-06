namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1024: "Quickly travel forward a short distance. Can be used during Global Cooldown." Charges recover
/// through cooldown recovery (game data). No damage. The game data gives it a short channel; combat logs show only a
/// cast event for it (no channel events), and it stops no channel.
/// </summary>
public class IceDash : RimeAbility
{
    public const int AbilityId = 1024;
    public const string AbilityName = "Ice Dash";
    public const double CooldownSeconds = 25.0;
    public const int Charges = 2;

    public IceDash(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Movement)
    {
        SetCooldown(CooldownSeconds, Charges);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
