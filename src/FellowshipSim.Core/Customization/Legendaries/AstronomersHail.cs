using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 516 (Elarion's Skystrider Leggings, item 5900): Starfall Volley deals more damage to the main target and
/// lasts longer, and each Multishot cast while it is active extends it. Effect 1862
/// talent IncreasedDurationAndExtension MainTargetDamageBonus DamageScaler.
/// Combat logs (fXyzwGcQkTrLD34R fight 4): the volley's target takes more damage than the other enemies.
/// </summary>
public class AstronomersHail : Legendary
{
    public const int PowerId = 516;
    public const int LeggingsItemId = 5900;
    public const double MainTargetMultiplier = 2.0;
    public const double ExtraSeconds = 3.0;
    public const double ExtensionPerMultishotSeconds = 0.25;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Astronomer's Hail";
    public override string HeroName => "Elarion";
    public override IReadOnlyList<int> ItemIds => [LeggingsItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Elarion || hero.GetAbility<StarfallVolley>() is not { } starfall) return;
        starfall.MainTargetMultiplier *= MainTargetMultiplier;
        starfall.ExtraDurationSeconds += ExtraSeconds;
        if (hero.GetAbility<Multishot>() is { } multishot)
        {
            multishot.Cast += (ctx, _) => starfall.Extend(ctx, ExtensionPerMultishotSeconds);
        }
    }
}
