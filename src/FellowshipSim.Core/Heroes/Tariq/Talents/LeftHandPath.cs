using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>Talent 72: "Your Face Breaker has increased Critical Strike chance." UNVERIFIED: no combat log has this talent.</summary>
public class LeftHandPath : Talent
{
    public const int TalentId = 72;
    public const string TalentName = "Left Hand Path";
    /// <summary>Face Breaker tooltip value.</summary>
    public const double CritChance = 0.40;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<FaceBreaker>() is { } faceBreaker) faceBreaker.CritChanceBonus += CritChance;
    }
}
