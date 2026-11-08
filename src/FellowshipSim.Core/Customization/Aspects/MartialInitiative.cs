namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 51: "Your Weapon Ability grants you increased Main Stat and Damage Reduction for a duration equal to
/// a percentage by rank of your Weapon Ability's default cooldown."
/// </summary>
public partial class MartialInitiative : Aspect
{
    public const int BuffEffectId = 2174;
    public const double BuffPrimaryStat = 1.1;
    public const double BuffDamageTaken = 0.85;

    public static readonly AuraDef Buff = AuraDef.Effect("Martial Initiative", BuffEffectId,
        modifiers: [StatModifier.More(StatType.IncomingDamageMultiplier, BuffDamageTaken), StatModifier.Increased(StatType.PrimaryAttributeMultiplier, BuffPrimaryStat)]);

    public override int Id => AspectId;
    public override string Name => AspectName;

    public override void Bind(Hero hero)
    {
        double fraction = ForRank(BuffWeaponCooldownToDurationByRank);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability != hero.WeaponAbility) return;
            hero.Auras.Apply(Buff.WithDuration(fraction * ability.Cooldown.TotalSeconds), now);
        };
    }
}
