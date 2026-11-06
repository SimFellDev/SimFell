using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 75: "Your Skull Crusher has a chance to have increased critical strike chance. Any critical strike
/// chance you have beyond the maximum causes a grievous critical strike." Rolled once per cast; every hit of the cast has it.
/// Combat logs (3 Tariq fights): grievous Skull Crusher hits come with the thunder bolt as well as the physical hit.
/// </summary>
public class ThemBones : Talent
{
    public const int TalentId = 75;
    public const string TalentName = "Them Bones";
    public const double ProcChance = 0.30;
    public const double CritChance = 1.0;
    public const string ProcKey = "them_bones";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not SkullCrusher || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx, cast.Target)) cast.CritChance += CritChance;
        };
    }
}
