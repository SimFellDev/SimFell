using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 75: "Your Skull Crusher has a chance to have increased critical strike chance. Any critical strike
/// chance you have beyond the maximum causes a grievous critical strike." Rolled once per cast; every hit of the cast has it.
/// Combat logs (3 Tariq fights): grievous Skull Crusher hits come with the thunder bolt as well as the physical hit.
/// </summary>
public partial class ThemBones : Talent
{
    public const string ProcKey = "them_bones";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not SkullCrusher || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, ProcAddedCritChanceChance, ctx, cast.Target)) cast.CritChance += ProcAddedCritChanceAddedCritChance;
        };
    }
}
