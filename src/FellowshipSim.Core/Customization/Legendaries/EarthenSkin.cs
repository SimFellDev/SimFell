using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 665 (Meiko's Twinsoul Helm, item 5899): "Once the Sibling of Earth created by your Twin Souls:Bulwark
/// expires or is destroyed, you gain Earthen Skin for 60 seconds, granting you 80% more Armor. You deal 20% more Wind
/// Damage. Your Stone Shield and Shatter Earth deal 20% more damage." The game data's changes list puts the 20% on Wind
/// Kick, Rising Storm, Lashing Stormkick, the Sibling of Wind (effect 1548), Shatter Earth's slam and quake (effects 1557,
/// 1556), Spirited Vortex and Stone Shield (effect 1572), all the time.
/// </summary>
public class EarthenSkin : Legendary
{
    public const int PowerId = 665;
    public const int HelmItemId = 5899;
    public const double DamageMultiplier = 1.2;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Earthen Skin";
    public override string HeroName => "Meiko";
    public override IReadOnlyList<int> ItemIds => [HelmItemId];

    public override void Bind(Hero hero)
    {
        hero.GetAbility<WindKick>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<RisingStorm>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<LashingStormkick>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<TwinSoulsArmyOfOne>()?.Sibling.AddDamageMultiplier(DamageMultiplier);
        if (hero.GetAbility<ShatterEarth>() is { } shatterEarth)
        {
            shatterEarth.AddDamageMultiplier(DamageMultiplier);
            shatterEarth.Quake.AddDamageMultiplier(DamageMultiplier);
        }
        hero.GetAbility<SpiritedVortex>()?.AddDamageMultiplier(DamageMultiplier);
        hero.GetAbility<StoneShield>()?.AddDamageMultiplier(DamageMultiplier);
    }
}
