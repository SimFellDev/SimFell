namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1200: interrupts the target's spellcasting and prevents it from casting. It must interrupt a spell, or it
/// has no effect. It can be used during Global Cooldown. Game data: no cost, no
/// cast time, no global cooldown, no damage, no Combo Points, and no AbilityType.Skill.Secondary tag, so it keeps Stealth.
/// The cooldown starts before the interrupt is tried in game data.
/// Combat logs (23 Mara fights, 416 Kicks): consecutive Kicks are at least <see cref="CooldownSeconds"/> apart. Kicks with no
/// interrupt trigger the cooldown too (WcTBrQNX46m9axqP fight 12 @ 462.034 and 478.331).
/// <see cref="FinalStratagem"/> ends the cooldown: the cooldown reduction skips abilities with an ignored tag, Kick has
/// none, and the reduction reaches its static cooldown effect.
/// </summary>
public class Kick : MaraAbility
{
    public const int AbilityId = 1200;
    public const string AbilityName = "Kick";

    /// <summary>
    /// Game data ability 1200, cooldown row. The static cooldown effect has no Haste scaling:
    /// its magnitude calculation returns literal 1.
    /// </summary>
    public const double CooldownSeconds = 16.0;

    public Kick(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
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
