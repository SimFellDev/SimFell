using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 80: "When your Leap Smash ability deals damage, you are granted 1 stack of Focused Wrath." One stack per cast
/// that hits an enemy.
/// </summary>
public class MouthForWar : Talent
{
    public const int TalentId = 80;
    public const string TalentName = "Mouth for War";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<LeapSmash>() is not { } leapSmash) return;
        leapSmash.Smashed += (ctx, struck) =>
        {
            if (struck.Count > 0) hero.Auras.Apply(FocusedWrath.Buff, ctx.CurrentTime);
        };
    }
}
