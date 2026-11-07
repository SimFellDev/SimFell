using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 95: "Your Earth abilities deal 20% more damage" (Meiko.Earth.Talent.IncreasedDamage.DamageMultiplier 1.2). The
/// game data's changes list names Earth Fist, Rising Earth, Stone Stomp, Earthfist Barrage, Shatter Earth's slam (effect
/// 1557) and Stone Shield (effect 1572), not the quake (effect 1556).
/// </summary>
public class RumblingStone : Talent
{
    public const int TalentId = 95;
    public const string TalentName = "Rumbling Stone";
    public const double DamageMultiplier = 1.2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.GetAbility<EarthFist>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<RisingEarth>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<StoneStomp>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<EarthfistBarrage>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<ShatterEarth>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<StoneShield>()?.AddDamageMultiplier(DamageMultiplier);
    }
}
