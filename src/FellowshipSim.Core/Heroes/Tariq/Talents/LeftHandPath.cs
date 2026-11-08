using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>Talent 72: "Your Face Breaker has increased Critical Strike chance." UNVERIFIED: no combat log has this talent.</summary>
public partial class LeftHandPath : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<FaceBreaker>() is { } faceBreaker) faceBreaker.CritChanceBonus += IncreasedCritChanceIncrease;
    }
}
