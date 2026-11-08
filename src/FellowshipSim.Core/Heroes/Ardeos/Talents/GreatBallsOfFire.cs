using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 160: Fire Ball deals more damage, and an additional share of the damage dealt is added to its damage over time
/// effect. Combat logs (1p7AXmHfwV9CYMxR fight 30, dBgbn8wD1RxL7Kma fight 9): replays with it match the logged Fire Ball
/// hits and the logged effect, and fall well short of the effect without the additional share.
/// </summary>
public partial class GreatBallsOfFire : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var fireBall = hero.GetAbility<FireBall>();
        if (fireBall == null) return;
        fireBall.AddDamageMultiplier(PlacedProjectileAoeDamageBonusDirectDamageScale);
        fireBall.DotFraction += PlacedProjectileAoeDamageBonusDamageToDotTransferIncrease;
    }
}
