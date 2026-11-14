using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 153: each tick of Searing Blaze and Engulfing Flames has a chance to gain critical strike chance, and the chance
/// rises with the hero's critical strike chance.
/// A tick it empowers is a grievous crit. A replay takes that grievous crit as the proof of the proc (one proc for each
/// effect).
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, YC861NTRgLbmw4Qa fight 31): the share of grievous ticks matches the base chance
/// at both fights' crit ratings. The model uses the base chance with no crit term.
/// </summary>
public partial class SpontaneousCombustion : Talent
{
    public const string SearingBlazeProcKey = "spontaneous_combustion_searing_blaze";
    public const string EngulfingFlamesProcKey = "spontaneous_combustion_engulfing_flames";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        Empower(hero.GetAbility<SearingBlaze>()?.Dot, SearingBlazeProcKey);
        Empower(hero.GetAbility<EngulfingFlames>()?.Dot, EngulfingFlamesProcKey);
    }

    private static void Empower(DamageOverTime? dot, string procKey) =>
        dot?.TickCritChanceBonuses.Add((ctx, instance) => ctx.Outcomes.RollProc(procKey, Constants.Chance, ctx, instance.Target) ? Constants.CritChanceAdded : 0.0);
}
