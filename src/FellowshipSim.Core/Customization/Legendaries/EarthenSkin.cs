using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 665: "Once the Sibling of Earth created by your Twin Souls:Bulwark expires or is destroyed, you gain
/// Earthen Skin for 60 seconds, granting you 80% more Armor. You deal 20% more Wind Damage. Your Stone Shield and Shatter
/// Earth deal 20% more damage." The game data's changes list puts the 20% on Wind Kick, Rising Storm, Lashing Stormkick,
/// the Sibling of Wind (effect 1548), Shatter Earth's slam and quake (effects 1557, 1556), Spirited Vortex and Stone
/// Shield (effect 1572), all the time.
/// </summary>
public partial class EarthenSkin : Legendary
{
    public override void Bind(Hero hero)
    {
        hero.GetAbility<WindKick>()?.AddDamageMultiplier(WindDamageIncrease);
        hero.GetAbility<RisingStorm>()?.AddDamageMultiplier(WindDamageIncrease);
        hero.GetAbility<LashingStormkick>()?.AddDamageMultiplier(WindDamageIncrease);
        hero.GetAbility<TwinSoulsArmyOfOne>()?.Sibling.AddDamageMultiplier(WindDamageIncrease);
        hero.GetAbility<SpiritedVortex>()?.AddDamageMultiplier(WindDamageIncrease);
        if (hero.GetAbility<ShatterEarth>() is { } shatterEarth)
        {
            shatterEarth.AddDamageMultiplier(AbilityDamageIncrease);
            shatterEarth.Quake.AddDamageMultiplier(AbilityDamageIncrease);
        }
        hero.GetAbility<StoneShield>()?.AddDamageMultiplier(AbilityDamageIncrease);
    }
}
