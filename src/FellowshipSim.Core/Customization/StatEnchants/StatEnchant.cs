using FellowshipSim.Core.Domain;

namespace FellowshipSim.Core.Customization.StatEnchants;

/// <summary>
/// A stat enchant (new in 0.5.0.0; game data "enchant" records, ItemTrait.TraitCategory.StatEnchant): each rank adds rating
/// to each of two stats, up to <see cref="MaxRankPerItem"/> on one item. One ID per pair of stats, named after the
/// main stat of the item it is on (Strength "Might", Agility "Guile", Intellect "Cunning"). Combat logs list it among an
/// item's augmentations (ID + 3,000,000) with its rank. The log's combatantinfo ratings include them; a build's ratings
/// are the gear's without them.
/// </summary>
public sealed record StatEnchant(int Id, string StrengthName, string AgilityName, string IntellectName, StatType FirstRating, StatType SecondRating)
{
    /// <summary>The game data's maxRank of every stat enchant.</summary>
    public const int MaxRankPerItem = 300;

    /// <summary>Every stat enchant (game data, enchant records 65 to 70).</summary>
    public static IReadOnlyList<StatEnchant> All { get; } =
    [
        new(65, "Jotunn's Might", "Wyvern's Guile", "Chimera's Cunning", StatType.CritRating, StatType.ExpertiseRating),
        new(66, "Griffin's Might", "Strix's Guile", "Salamander's Cunning", StatType.CritRating, StatType.HasteRating),
        new(67, "Revenant's Might", "Wraith's Guile", "Banshee's Cunning", StatType.CritRating, StatType.SpiritRating),
        new(68, "Behemoth's Might", "Huldra's Guile", "Undine's Cunning", StatType.ExpertiseRating, StatType.HasteRating),
        new(69, "Ogre's Might", "Vampire's Guile", "Sphinx's Cunning", StatType.ExpertiseRating, StatType.SpiritRating),
        new(70, "Valkyrie's Might", "Centaur's Guile", "Unicorn's Cunning", StatType.HasteRating, StatType.SpiritRating),
    ];

    /// <summary>The stat enchant with game ID <paramref name="id"/>, or null.</summary>
    public static StatEnchant? Find(int id) => All.FirstOrDefault(e => e.Id == id);

    /// <summary>The name on an item of main stat <paramref name="mainStat"/>.</summary>
    public string NameFor(MainStat mainStat) => mainStat switch
    {
        MainStat.Strength => StrengthName,
        MainStat.Agility => AgilityName,
        _ => IntellectName,
    };

    /// <summary>Its key in a profile, from its two stats ("crit_expertise").</summary>
    public string Key => $"{KeyOf(FirstRating)}_{KeyOf(SecondRating)}";

    private static string KeyOf(StatType rating) => rating.ToString().Replace("Rating", "").ToLowerInvariant();
}
