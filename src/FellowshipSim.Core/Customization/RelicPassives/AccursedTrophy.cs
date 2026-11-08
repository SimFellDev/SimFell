namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// Relic passive 777 (Orb of Inner Seas): "Your Spirit Ability grants you Drakheim's Absolution, increasing your Primary
/// Stat for a time." (ability 2314, effect 3269)
/// </summary>
public partial class AccursedTrophy : RelicPassive
{
    public const int EffectId = 3269;

    /// <summary>Tooltip: "for 20 seconds". The relic passive record has no tuning value.</summary>
    public const double DurationSeconds = 20.0;

    /// <summary>Tooltip: "increasing your Primary Stat by 20%". The relic passive record has no tuning value.</summary>
    public const double PrimaryStatBonus = 1.2;

    public static readonly AuraDef Buff = AuraDef.Effect("Drakheim's Absolution", EffectId, DurationSeconds,
        modifiers: StatModifier.More(StatType.PrimaryAttribute, PrimaryStatBonus));

    public override int Id => RelicPassiveId;
    public override string Name => RelicPassiveName;
    public override IReadOnlyList<RelicItem> Items => RelicPassiveItems;

    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability.Category == AbilityCategory.Spirit) hero.Auras.Apply(Buff, now);
        };
    }
}
