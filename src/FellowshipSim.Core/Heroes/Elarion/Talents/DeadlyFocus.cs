using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 679: Focused Shot and Celestial Shot deal more damage but Focused Shot generates less Focus.
/// Effects 1891 and 1864 talent Enhanced DamageScaler. UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class DeadlyFocus : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<FocusedShot>() is { } focusedShot)
        {
            focusedShot.AddDamageMultiplier(EnhancedDamageScaler);
            focusedShot.FocusGainMultiplier *= EnhancedResourceGenerationScaler;
        }
        hero.GetAbility<CelestialShot>()?.AddDamageMultiplier(EnhancedDamageScaler);
    }
}
