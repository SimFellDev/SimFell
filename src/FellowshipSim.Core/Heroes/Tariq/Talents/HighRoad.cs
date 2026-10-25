using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 441: "When your Leap Smash ability does not hit an enemy, its cooldown is reduced by 10 seconds. When your Leap
/// Smash ability hits at least 1 enemy, you gain 20 Fury." (Leap Smash Ink.MoveToLocationAoeDamage.ResourceGain: 20% of
/// maximum Fury.)
/// </summary>
public class HighRoad : Talent
{
    public const int TalentId = 441;
    public const string TalentName = "High Road";
    public const double FuryGain = 20.0;
    public const double CooldownReductionSeconds = 10.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Tariq tariq || hero.GetAbility<LeapSmash>() is not { } leapSmash) return;
        leapSmash.Smashed += (ctx, struck) =>
        {
            if (struck.Count > 0) tariq.GainFury(FuryGain, ctx, struck[0]);
            else hero.ReduceCooldown(leapSmash.Name, SimTime.FromSeconds(CooldownReductionSeconds), ctx.CurrentTime);
        };
    }
}
