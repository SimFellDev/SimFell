namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1200: "Interrupt the target's spellcasting and prevent it from casting for 4 sec. Must interrupt a spell, or it
/// will have no effect. Can be used during Global Cooldown." GA_Mara_MeleeInterrupt, game data build 26320967: no cost, no
/// cast time, no global cooldown, no damage, no Combo Points, and no AbilityType.Skill.Secondary tag, so it keeps Stealth.
/// The cooldown starts before the interrupt is tried (FellowshipCommitHeroAbility at ubergraph offset 10).
/// Combat logs (23 Mara fights, 416 Kicks): 16.062s apart at the least. Kicks with no interrupt trigger the cooldown too
/// (WcTBrQNX46m9axqP fight 12 @ 462.034 and 478.331: the next Kick 16.297s and 16.062s later).
/// <see cref="FinalStratagem"/> ends the cooldown: ReduceCooldownForAllAbilities skips abilities with an ignored tag, Kick has
/// none, and the function reaches GE_Shared_Cooldown_Static (exe 0x1452417A0).
/// </summary>
public class Kick : MaraAbility
{
    public const int AbilityId = 1200;
    public const string AbilityName = "Kick";

    /// <summary>
    /// Game data ability 1200, row Mara.MeleeInterrupt.Cooldown. GE_Shared_Cooldown_Static has no Haste scaling:
    /// GEC_CooldownStatic.CalculateBaseMagnitude returns literal 1.
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
