using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 516: Starfall Volley deals more damage to the main target and lasts longer, and each Multishot cast
/// while it is active extends it. Effect 1862 talent IncreasedDurationAndExtension MainTargetDamageBonus DamageScaler.
/// Combat logs (fXyzwGcQkTrLD34R fight 4): the volley's target takes more damage than the other enemies.
/// </summary>
public partial class AstronomersHail : Legendary
{
    public override void Bind(Hero hero)
    {
        if (hero is not Elarion || hero.GetAbility<StarfallVolley>() is not { } starfall) return;
        starfall.MainTargetMultiplier *= IncreasedDurationAndExtensionMainTargetDamageBonusDamageScaler;
        starfall.ExtraDurationSeconds += IncreasedDurationAndExtensionDurationIncreaseInSeconds;
        if (hero.GetAbility<Multishot>() is { } multishot)
        {
            multishot.Cast += (ctx, _) => starfall.Extend(ctx, DurationExtensionPerUse);
        }
    }
}
