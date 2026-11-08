namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 42: "Your targeted offensive abilities grant you Haste rating for a time, stacking up to a maximum.
/// The Haste is lost if you use a targeted offensive ability on any other enemy." (effect 2141, "Trait : Focused Haste")
/// </summary>
public partial class HuntersFocus : Sigil
{
    public const int BuffEffectId = 2141;
    public const double DurationSeconds = 8.0;
    public const int MaxStacks = 5;

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var aura = hero.Register(AuraDef.Effect("Trait : Focused Haste", BuffEffectId, DurationSeconds, MaxStacks,
            StatModifier.Add(StatType.HasteRating, ForRank(AdditionalHasteRatingByRank))));
        EnemyTarget? focus = null;
        hero.OnCastCompleted += (ability, target, now) =>
        {
            if (target == null || ability.BaseCoefficient <= 0) return;
            if (focus != null && focus != target) hero.Auras.Remove(aura, now);
            focus = target;
            hero.Auras.Apply(aura, now);
        };
    }
}
