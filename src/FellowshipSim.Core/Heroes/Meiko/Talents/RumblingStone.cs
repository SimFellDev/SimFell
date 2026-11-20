using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 95: "Your Earth abilities deal 20% more damage" (Meiko.Earth.Talent.IncreasedDamage.DamageMultiplier 1.2). The
/// game data's changes list names Earth Fist, Rising Earth, Stone Stomp, Earthfist Barrage, Shatter Earth's slam (effect
/// 1557) and Stone Shield (effect 1572), not the quake (effect 1556).
/// </summary>
public partial class RumblingStone : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.GetAbility<EarthFist>()?.AddDamageMultiplier(Constants.EarthTalentIncreasedDamageDamageMultiplier);
        hero.GetAbility<RisingEarth>()?.AddDamageMultiplier(Constants.EarthTalentIncreasedDamageDamageMultiplier);
        hero.GetAbility<StoneStomp>()?.AddDamageMultiplier(Constants.EarthTalentIncreasedDamageDamageMultiplier);
        hero.GetAbility<EarthfistBarrage>()?.AddDamageMultiplier(Constants.EarthTalentIncreasedDamageDamageMultiplier);
        hero.GetAbility<ShatterEarth>()?.AddDamageMultiplier(Constants.EarthTalentIncreasedDamageDamageMultiplier);
        hero.GetAbility<StoneShield>()?.AddDamageMultiplier(Constants.EarthTalentIncreasedDamageDamageMultiplier);
    }
}
