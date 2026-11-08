using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 132: Each Celestial Shot or Multishot cast reduces the cooldown of Highwind Arrow and Heartseeker Barrage. UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class SkywardMunitions : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var reduction = SimTime.FromSeconds(ReductionInSeconds);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not (CelestialShot or Multishot)) return;
            hero.ReduceCooldown<HighwindArrow>(reduction, now);
            hero.ReduceCooldown<HeartseekerBarrage>(reduction, now);
        };
    }
}
