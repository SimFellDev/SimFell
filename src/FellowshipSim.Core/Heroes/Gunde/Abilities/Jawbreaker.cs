namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 1244: "Interrupt the target's spellcasting ... Can be used during Global Cooldown." No damage. Cooldown 16s: row
/// Gunde.SingleTargetInterrupt.Cooldown of AC_Hero_Gunde.csv (extractor 36f0a5a5, build 26320967). Combat logs (180 intervals): 16.067s at the least.
/// </summary>
public class Jawbreaker : GundeAbility
{
    public const int AbilityId = 1244;
    public const string AbilityName = "Jawbreaker";
    public const double CooldownSeconds = 16.0;

    public Jawbreaker(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
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
