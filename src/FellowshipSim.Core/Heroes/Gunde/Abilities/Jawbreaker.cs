namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>Ability 1244: "Interrupt the target's spellcasting ... Can be used during Global Cooldown." Cooldown 12s; no damage.</summary>
public class Jawbreaker : GundeAbility
{
    public const int AbilityId = 1244;
    public const string AbilityName = "Jawbreaker";
    public const double CooldownSeconds = 12.0;

    public Jawbreaker(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        Interrupts = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        if (target is { IsCasting: true }) ctx.Interrupt(target);
        TriggerCooldown(ctx);
    }
}
