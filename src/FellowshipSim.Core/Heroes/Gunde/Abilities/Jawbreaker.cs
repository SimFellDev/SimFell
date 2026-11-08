namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 1244: "Interrupt the target's spellcasting ... Can be used during Global Cooldown." No damage. The cooldown is
/// game data. Combat logs (180 intervals): no interval is shorter than the cooldown.
/// </summary>
public partial class Jawbreaker : GundeAbility
{
    public const double CooldownSeconds = 16.0;

    public Jawbreaker(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
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
