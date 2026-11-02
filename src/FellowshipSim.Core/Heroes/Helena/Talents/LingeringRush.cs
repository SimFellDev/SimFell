using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 649: "Your Sweeping Strike's Blood Rally lasts an additional 6 seconds, but its healing is reduced to 4% of your
/// maximum health every 1 second."
/// </summary>
public class LingeringRush : Talent
{
    public const int TalentId = 649;
    public const string TalentName = "Lingering Rush";
    public const double BloodRallyBonusSeconds = 6.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<SweepingStrike>() is { } sweepingStrike)
        {
            sweepingStrike.BloodRallyBuff = SweepingStrike.BloodRally.WithDuration(SweepingStrike.BloodRallySeconds + BloodRallyBonusSeconds);
        }
    }
}
