using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 132: Each Celestial Shot or Multishot cast reduces the cooldown of Highwind Arrow and Heartseeker Barrage. UNVERIFIED: no combat log has this talent.
/// </summary>
public class SkywardMunitions : Talent
{
    public const int TalentId = 132;
    public const string TalentName = "Skyward Munitions";
    public const double ReductionSeconds = 1.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var reduction = SimTime.FromSeconds(ReductionSeconds);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not (CelestialShot or Multishot)) return;
            hero.ReduceCooldown<HighwindArrow>(reduction, now);
            hero.ReduceCooldown<HeartseekerBarrage>(reduction, now);
        };
    }
}
