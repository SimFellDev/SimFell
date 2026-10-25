using FellowshipSim.Core.Customization.Augmentations;

namespace FellowshipSim.Core.Customization.StatEnchants;

/// <summary>
/// A stat enchant (ItemTrait.TraitCategory.StatEnchant, augmentations 65-70): "+{X} {stat} +{X} {stat}", one rating point
/// of each of its two stats per rank (enchant records: stats coefficient 1, placeholders perRank, maxRank 300). A
/// character sheet's ratings already include them (combat logs: the engine's crit chance from the logged ratings matches
/// the log's crit rate within a point for players with up to six enchants), so they add rating only to a build without
/// sheet values.
/// </summary>
public abstract class StatEnchant : Augmentation
{
    /// <summary>The two ratings the enchant raises.</summary>
    protected abstract (StatType First, StatType Second) Ratings { get; }

    public override void Bind(Hero hero)
    {
        if (hero.Stats.UsesSheetValues) return;
        AddRating(hero.Stats, Ratings.First);
        AddRating(hero.Stats, Ratings.Second);
    }

    private void AddRating(StatBlock stats, StatType rating)
    {
        switch (rating)
        {
            case StatType.CritRating: stats.CritRating += Rank; break;
            case StatType.HasteRating: stats.HasteRating += Rank; break;
            case StatType.ExpertiseRating: stats.ExpertiseRating += Rank; break;
            case StatType.SpiritRating: stats.SpiritRating += Rank; break;
        }
    }
}
