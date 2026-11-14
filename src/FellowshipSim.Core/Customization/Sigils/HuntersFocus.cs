namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 42: "Your targeted offensive abilities grant you Haste rating for a time, stacking up to a maximum.
/// The Haste is lost if you use a targeted offensive ability on any other enemy." (effect 2141, "Trait : Focused Haste")
/// Only abilities with <see cref="Ability.TargetedOffensive"/> count.
/// </summary>
public partial class HuntersFocus : Sigil
{
    public const int BuffEffectId = 2141;
    public const double DurationSeconds = 8.0;
    public const int MaxStacks = 5;

    /// <summary>The Haste buff; <see cref="Bind"/> gives it its rank's Haste rating.</summary>
    public static readonly AuraDef Buff = AuraDef.Effect("Trait : Focused Haste", BuffEffectId, DurationSeconds, MaxStacks);

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var aura = hero.Register(Buff.WithModifiers(StatModifier.Add(StatType.HasteRating, ForRank(Constants.AdditionalHasteRatingByRank))));
        EnemyTarget? focus = null;
        hero.OnCastCompleted += (ability, target, now) =>
        {
            if (target == null || !ability.TargetedOffensive) return;
            if (focus != null && focus != target) hero.Auras.Remove(aura, now);
            focus = target;
            hero.Auras.Apply(aura, now);
        };
    }
}
