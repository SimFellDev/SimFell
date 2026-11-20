using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 416: "The final strike of Earthfist Barrage deals 35% more damage and explodes the damage onto all
/// nearby enemies. Damage is reduced beyond 1 target" (effect 2335's targetCountFalloff threshold 1). Combat logs
/// (3M2vWr8taxVfTQFY fight 53 @ 391.801, TipsyPenguin): the explosion is logged under 1112 on the other enemies, at the
/// uppercut's moment, each hit with a crit roll of its own: 1,821 non-crit on each of 4 enemies, half an uppercut
/// (sqrt(1/4)).
/// </summary>
public partial class TectonicImpact : Legendary
{
    /// <summary>Effect 2335's targetCountFalloff threshold.</summary>
    public const double TargetThreshold = 1;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<EarthfistBarrage>() is not { } barrage) return;
        barrage.UppercutMultiplier *= Constants.FinalHitSpendChargeDamageMultiplier;
        barrage.ExplosionTargetThreshold = TargetThreshold;
    }
}
