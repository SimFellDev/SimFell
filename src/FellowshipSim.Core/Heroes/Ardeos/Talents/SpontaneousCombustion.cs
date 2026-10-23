using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 153: "Each tick of Searing Blaze &amp; Engulfing Flames has a 10% chance to have +100% critical strike chance.
/// The chance for Spontaneous Combustion to trigger is increased by +1% for every 5% critical strike chance you have."
/// Ability 1255 has 4% in its text; the talent's 10% is used. UNVERIFIED: no combat log has this talent.
/// </summary>
public class SpontaneousCombustion : Talent
{
    public const int TalentId = 153;
    public const string TalentName = "Spontaneous Combustion";
    public const double BaseChance = 0.10;
    /// <summary>+1% chance for every 5% critical strike chance.</summary>
    public const double ChancePerCritStep = 0.01;
    public const double CritStep = 0.05;
    public const double CritChance = 1.0;
    public const string ProcKey = "spontaneous_combustion";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        DamageOverTime?[] dots = [hero.GetAbility<SearingBlaze>()?.Dot, hero.GetAbility<EngulfingFlames>()?.Dot];
        foreach (var dot in dots.OfType<DamageOverTime>())
        {
            dot.TickCritChanceBonuses.Add((ctx, instance) =>
            {
                double chance = BaseChance + ChancePerCritStep * Math.Floor(hero.CritChance(ctx.CurrentTime) / CritStep + 1e-9);
                return ctx.Outcomes.RollProc(ProcKey, chance, ctx, instance.Target) ? CritChance : 0.0;
            });
        }
    }
}
