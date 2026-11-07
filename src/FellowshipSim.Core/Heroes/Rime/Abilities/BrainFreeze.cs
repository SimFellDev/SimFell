namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1019: interrupts the target's spellcasting and prevents it from casting for a time. Can be used during Global
/// Cooldown. No damage.
/// </summary>
public class BrainFreeze : RimeAbility
{
    public const int AbilityId = 1019;
    public const string AbilityName = "Brain Freeze";
    public const double CooldownSeconds = 20.0;

    public BrainFreeze(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds);
        IgnoresCooldownRecovery = true;
        IgnoresGcd = true;
        Interrupts = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        if (target is { IsCasting: true }) ctx.Interrupt(target);
        TriggerCooldown(ctx);
    }
}
