using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 202: Highwind Arrow has a chance to have increased critical strike chance. GRIEVOUS CRITICAL
/// STRIKE: critical strike chance beyond the cap causes a grievous critical strike. Rolled once per cast; the
/// cast's ricochets have it too (Bowguy constants ChanceToHaveIncreasedCritChance ProcChance, AddedCritChance).
/// </summary>
public partial class LethalShots : Talent
{
    public const string ProcKey = "lethal_shots";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not HighwindArrow || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, Constants.ChanceToHaveIncreasedCritChanceProcChance, ctx)) cast.CritChance += Constants.ChanceToHaveIncreasedCritChanceAddedCritChance;
        };
    }
}
