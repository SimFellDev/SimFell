using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 441: "When your Leap Smash ability does not hit an enemy, its cooldown is reduced. When your Leap
/// Smash ability hits at least one enemy, you gain Fury." (Game data: a share of
/// maximum Fury.)
/// </summary>
public partial class HighRoad : Talent
{
    public const double FuryGain = 20.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Tariq tariq || hero.GetAbility<LeapSmash>() is not { } leapSmash) return;
        leapSmash.Smashed += (ctx, struck) =>
        {
            if (struck.Count > 0) tariq.GainFury(FuryGain, ctx, struck[0]);
            else hero.ReduceCooldown(leapSmash.Name, SimTime.FromSeconds(ReducedCooldownIfNoHitCooldownReductionInSeconds), ctx.CurrentTime);
        };
    }
}
