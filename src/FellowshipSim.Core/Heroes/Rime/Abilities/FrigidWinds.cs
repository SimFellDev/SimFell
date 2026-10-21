namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1021 / effect 1385: "Blast enemies in front of you with arctic winds, Knocking Back enemies and Slowing them by
/// 50% for 12 sec. Can be used during Global Cooldown" (60s cooldown). It deals no damage; its cast still rolls the
/// procs that any ability rolls (g6zBdpvJCmh2HfrD fight 31 @ 118.911: Dark Prophecy on the Frigid Winds cast).
/// </summary>
public class FrigidWinds : RimeAbility
{
    public const int AbilityId = 1021;
    public const string AbilityName = "Frigid Winds";
    public const double CooldownSeconds = 60.0;

    public FrigidWinds(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
