using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 160: "Your Fire Ball deals 50% more damage and an additional 10% of the damage dealt is added to its damage
/// over time effect." Combat logs (1p7AXmHfwV9CYMxR fight 30, dBgbn8wD1RxL7Kma fight 9): replays with it deal 1.02 and 1.08
/// of the logged Fire Ball hits, and 1.04 and 1.13 of the logged effect (without the extra 10% the effect would deal 0.69
/// and 0.75).
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
