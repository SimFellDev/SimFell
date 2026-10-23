using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 160: "Your Fire Ball deals 50% more damage and an additional 10% of the damage dealt is added to its damage
/// over time effect." UNVERIFIED: no combat log has this talent.
/// </summary>
public class GreatBallsOfFire : Talent
{
    public const int TalentId = 160;
    public const string TalentName = "Great Balls of Fire";
    public const double DamageMultiplier = 1.5;
    public const double ExtraDotFraction = 0.10;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var fireBall = hero.GetAbility<FireBall>();
        if (fireBall == null) return;
        fireBall.AddDamageMultiplier(DamageMultiplier);
        fireBall.DotFraction += ExtraDotFraction;
    }
}
