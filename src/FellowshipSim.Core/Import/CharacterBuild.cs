using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Import;

/// <summary>An augmentation or glyph in a build: its game ID, its rank, and the name the log gave it (for reports).</summary>
public sealed record BuildEntry(int Id, int Rank = 1, string? Name = null);

/// <summary>
/// A hero's build by game IDs: character-sheet stats, talents, augmentations, glyphs, gem powers, relic passive, legendary items
/// and weapon. FellowshipSim.Logs builds it from a combat log; it can also be written or edited by hand.
/// <see cref="HeroImporter"/> turns it into a hero ready to simulate.
/// </summary>
public class CharacterBuild
{
    public string Name { get; set; } = "";

    /// <summary>The hero's name, e.g. "Rime" (its class in Heroes/).</summary>
    public string Hero { get; set; } = "";

    // ---- character sheet
    public double PrimaryStat { get; set; }
    public double Stamina { get; set; }
    /// <summary>Maximum health (the log's combatantinfo); 0 when unknown.</summary>
    public double MaxHealth { get; set; }
    public double Armor { get; set; }
    public double CritRating { get; set; }
    public double HasteRating { get; set; }
    public double ExpertiseRating { get; set; }
    public double SpiritRating { get; set; }

    /// <summary>
    /// Character-sheet percentages ("crit", "haste", "expertise", "spirit") as logged. When set, they are used as the
    /// hero's base percentages (they already include passive bonuses such as gem nodes).
    /// </summary>
    public Dictionary<string, double> SheetPercents { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ---- build, by game ID
    /// <summary>Talent IDs, in the order they were picked.</summary>
    public List<int> Talents { get; set; } = new();
    /// <summary>Aspects, sigils and protective enchants.</summary>
    public List<BuildEntry> Augmentations { get; set; } = new();
    public List<BuildEntry> Glyphs { get; set; } = new();
    /// <summary>Each colour's gem power as the character sheet shows it (the relics' multiplier included).</summary>
    public Dictionary<GemColor, double> GemPower { get; set; } = new();
    /// <summary>
    /// Where the gem power comes from, when known: the gem IDs in the gear's sockets (up to
    /// <see cref="Customization.Gems.GemItem.Sockets"/>) and the gear's bonuses per colour
    /// (<see cref="Customization.Gems.GemItem.GearBonusPower"/> each).
    /// </summary>
    public List<int> SocketedGemIds { get; set; } = new();
    public Dictionary<GemColor, int> GearGemBonuses { get; set; } = new();
    /// <summary>
    /// Stat enchants: the enchant's ID (65 to 70) and the rating it adds to each of its two stats, summed over the items
    /// that have it. The ratings above are without them; a character sheet's percentages include them.
    /// </summary>
    public List<BuildEntry> StatEnchants { get; set; } = new();
    /// <summary>Item ID of the relic in slot 1 (e.g. 5926, Orb of Inner Seas, for Accursed Trophy); 0 for none.</summary>
    public int Relic1ItemId { get; set; }
    /// <summary>Item ID of the relic in slot 2; 0 for none.</summary>
    public int Relic2ItemId { get; set; }
    /// <summary>Item IDs of the legendary items worn.</summary>
    public List<int> LegendaryItemIds { get; set; } = new();
    /// <summary>The weapon's ability ID (the weapon tree's ability, e.g. 1558 for Asha's Chronoshift Spire).</summary>
    public int WeaponAbilityId { get; set; }

    /// <summary>The rating the stat enchants add to <paramref name="rating"/> (<see cref="StatType.CritRating"/> ...).</summary>
    public double EnchantRating(StatType rating) =>
        StatEnchants.Sum(e => Customization.StatEnchants.StatEnchant.Find(e.Id) is { } enchant
                              && (enchant.FirstRating == rating || enchant.SecondRating == rating) ? e.Rank : 0);
}
