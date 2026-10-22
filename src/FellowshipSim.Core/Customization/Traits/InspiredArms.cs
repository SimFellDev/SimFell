namespace FellowshipSim.Core.Customization.Traits;

/// <summary>
/// Trait 3: "Upon using your Weapon Ability, you gain 10% Cooldown Acceleration for a duration equal to
/// 10% / 12% / 14% / 16% of your Weapon Ability's default cooldown duration." UNVERIFIED: no log has this trait, and the
/// game data names no effect for it (so a replay cannot compare it).
/// </summary>
public class InspiredArms : Trait
{
    public const int TraitId = 3;
    public const string TraitName = "Inspired Arms";
    public const double CooldownAcceleration = 0.10;
    public static readonly double[] WeaponCooldownFractionByRank = [0.10, 0.12, 0.14, 0.16];

    public static readonly AuraDef Buff = AuraDef.Internal("Inspired Arms", 0, 1);

    public override int Id => TraitId;
    public override string Name => TraitName;

    public override void Bind(Hero hero)
    {
        double fraction = ForRank(WeaponCooldownFractionByRank);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability == hero.WeaponAbility) hero.Auras.Apply(Buff.WithDuration(fraction * ability.Cooldown.TotalSeconds), now);
        };
        hero.CooldownAccelerations.Add((_, now) => hero.Auras.HasAura(Buff, now) ? CooldownAcceleration : 0.0);
        hero.CooldownAccelerationAuraKeys.Add(Buff.Key);
    }
}
