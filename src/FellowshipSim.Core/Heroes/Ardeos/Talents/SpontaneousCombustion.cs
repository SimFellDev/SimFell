using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 153: "Each tick of Searing Blaze &amp; Engulfing Flames has a 10% chance to have +100% critical strike chance.
/// The chance for Spontaneous Combustion to trigger is increased by +1% for every 5% critical strike chance you have."
/// A tick it empowers is a grievous crit. A replay takes that grievous crit as the proof of the proc (one proc for each
/// effect).
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, YC861NTRgLbmw4Qa fight 31): 10.9% and 10.8% of the ticks are grievous, with crit
/// ratings of 106 and 153. The model uses a 10% chance with no crit term.
/// </summary>
public class SpontaneousCombustion : Talent
{
    public const int TalentId = 153;
    public const string TalentName = "Spontaneous Combustion";
    public const double Chance = 0.10;
    public const double CritChance = 1.0;
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
        dot?.TickCritChanceBonuses.Add((ctx, instance) => ctx.Outcomes.RollProc(procKey, Chance, ctx, instance.Target) ? CritChance : 0.0);
}
