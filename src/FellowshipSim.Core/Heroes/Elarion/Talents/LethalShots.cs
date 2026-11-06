using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 202: Highwind Arrow has a chance to have increased critical strike chance. GRIEVOUS CRITICAL
/// STRIKE: critical strike chance beyond the cap causes a grievous critical strike. Rolled once per cast; the
/// cast's ricochets have it too (Bowguy constants ChanceToHaveIncreasedCritChance ProcChance, AddedCritChance).
/// </summary>
public class LethalShots : Talent
{
    public const int TalentId = 202;
    public const string TalentName = "Lethal Shots";
    public const double ProcChance = 0.4;
    public const double CritChance = 1.0;
    public const string ProcKey = "lethal_shots";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not HighwindArrow || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) cast.CritChance += CritChance;
        };
    }
}
