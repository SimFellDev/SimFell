using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 202: "Your Highwind Arrow has a 40% chance to have +100% increased critical strike chance. GRIEVOUS CRITICAL
/// STRIKE: Any critical strike chance you have beyond 100% causes a grievous critical strike." Rolled once per cast; the
/// cast's ricochets have it too (Bowguy constants ChanceToHaveIncreasedCritChance ProcChance 0.4, AddedCritChance 1).
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
