namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 47: "Your Weapon Ability's Cooldown is reduced by a percentage by rank when you activate your Spirit Ability."
/// (a fraction of its default cooldown), and "Your Weapon Ability generates Spirit Points multiplied by a value equal to
/// your Weapon Ability's default cooldown, divided by a constant" (<see cref="SpiritPointsPer30sOfCooldown"/>).
/// </summary>
public partial class VisionsOfGrandeur : Aspect
{
    public const double SpiritPointsPer30sOfCooldown = 2.5;

    public override int Id => AspectId;
    public override string Name => AspectName;

    public override void Bind(Hero hero)
    {
        double fraction = ForRank(SpiritPointsGainWeaponCooldownFractionByRank);
        hero.OnCastCompleted += (ability, _, _) =>
        {
            if (ability == hero.WeaponAbility && hero.SimContext is { } ctx)
            {
                hero.GainResource(Hero.SpiritPoints, SpiritPointsPer30sOfCooldown * ability.Cooldown.TotalSeconds / 30.0, ctx);
            }
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability.Category != AbilityCategory.Spirit || hero.WeaponAbility == null) return;
            hero.ReduceCooldown(hero.WeaponAbility.Name, SimTime.FromSeconds(fraction * hero.WeaponAbility.Cooldown.TotalSeconds), now);
        };
    }
}
