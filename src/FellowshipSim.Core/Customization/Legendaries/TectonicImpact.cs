using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 416 (Meiko's Twinsoul Tunic, item 5898): "The final strike of Earthfist Barrage deals 35% more damage and
/// explodes the damage onto all nearby enemies. Damage is reduced beyond 1 target"
/// (Meiko.ChanneledHeavyDamage.Talent.FinalHitSpendCharge.DamageMultiplier 1.35; effect 2335's targetCountFalloff threshold
/// 1). Combat logs (3M2vWr8taxVfTQFY fight 53 @ 391.801, TipsyPenguin): the explosion is logged under 1112 on the other
/// enemies, at the uppercut's moment, each hit with a crit roll of its own: 1,821 non-crit on each of 4 enemies, half an
/// uppercut (sqrt(1/4)).
/// </summary>
public class TectonicImpact : Legendary
{
    public const int PowerId = 416;
    public const int TunicItemId = 5898;
    public const double UppercutMultiplier = 1.35;
    public const double TargetThreshold = 1;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Tectonic Impact";
    public override string HeroName => "Meiko";
    public override IReadOnlyList<int> ItemIds => [TunicItemId];

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<EarthfistBarrage>() is not { } barrage) return;
        barrage.UppercutMultiplier *= UppercutMultiplier;
        barrage.ExplosionTargetThreshold = TargetThreshold;
    }
}
