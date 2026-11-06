using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Import;

/// <summary>An augmentation or glyph in a build: its game ID, its rank, and the name the log gave it (for reports).</summary>
public sealed record BuildEntry(int Id, int Rank = 1, string? Name = null);

/// <summary>
/// A hero's build by game IDs: the gear's raw stats, talents, augmentations, glyphs, gem powers, relic passive, legendary items
/// and weapon. FellowshipSim.Logs builds it from a combat log; it can also be written or edited by hand.
/// <see cref="HeroImporter"/> turns it into a hero ready to simulate.
/// </summary>
public class CharacterBuild
{
    public string Name { get; set; } = "";

    /// <summary>
    /// The FellowshipLogs link the build was read from: a report link with its fight and player, or a character's page.
    /// Empty for a build not read from a log.
    /// </summary>
    public string LogSource { get; set; } = "";

    /// <summary>The hero's name, e.g. "Rime" (its class in Heroes/).</summary>
    public string Hero { get; set; } = "";

    // ---- the gear's raw stats (the engine adds the base stats and every bonus)
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
    /// True when the stats came from a profile written before builds held the gear's raw stats: its percentages became
    /// ratings as they were, and its stats likely count bonuses the engine adds again. Editing a stat clears it.
    /// </summary>
    public bool StatsFromOldProfile { get; set; }

    // ---- build, by game ID
    /// <summary>Talent IDs, in the order they were picked.</summary>
    public List<int> Talents { get; set; } = new();
    /// <summary>Aspects, sigils and protective enchants.</summary>
    public List<BuildEntry> Augmentations { get; set; } = new();
    public List<BuildEntry> Glyphs { get; set; } = new();
    /// <summary>Each colour's gem power from the gear: its sockets and bonuses, before the relics' multiplier.</summary>
    public Dictionary<GemColor, double> GemPower { get; set; } = new();
    /// <summary>
    /// Where the gem power comes from, when known: the gem IDs in the gear's sockets (up to
    /// <see cref="Customization.Gems.GemItem.Sockets"/>) and the gear's bonuses per colour
    /// (<see cref="Customization.Gems.GemItem.GearBonusPower"/> each).
    /// </summary>
    public List<int> SocketedGemIds { get; set; } = new();
    public Dictionary<GemColor, int> GearGemBonuses { get; set; } = new();
    /// <summary>
    /// Stat enchants: the enchant's ID and the rating it adds to each of its two stats, summed over the items
    /// that have it. The ratings above are without them; the engine adds them.
    /// </summary>
    public List<BuildEntry> StatEnchants { get; set; } = new();
    /// <summary>
    /// How many slots each stat enchant fills, by ID, when it is known (a log: on each item that has it, one per fixed rating step
    /// rounded up; plus the weapon tree's points in it). A stat enchant not here counts as its
    /// rating over that step.
    /// </summary>
    public Dictionary<int, int> StatEnchantCounts { get; set; } = new();
    /// <summary>Item ID of the relic in slot 1 (e.g. 5926, Orb of Inner Seas, for Accursed Trophy); 0 for none.</summary>
    public int Relic1ItemId { get; set; }
    /// <summary>Item ID of the relic in slot 2; 0 for none.</summary>
    public int Relic2ItemId { get; set; }
    /// <summary>Item IDs of the legendary items worn.</summary>
    public List<int> LegendaryItemIds { get; set; } = new();
    /// <summary>The weapon's ability ID (the weapon tree's ability, e.g. 1558 for Asha's Chronoshift Spire).</summary>
    public int WeaponAbilityId { get; set; }

    /// <summary>
    /// The items worn, one piece per filled slot, with their tempers. Picking gear sets the raw stats above to the pieces'
    /// stats summed, and the relics, legendary and weapon to theirs; a log gives both. Empty for a build made before
    /// builds held their gear: its stats stand as they are.
    /// </summary>
    public List<GearPiece> Gear { get; set; } = new();

    /// <summary>The rating the stat enchants add to <paramref name="rating"/> (<see cref="StatType.CritRating"/> ...).</summary>
    public double EnchantRating(StatType rating) =>
        StatEnchants.Sum(e => Customization.StatEnchants.StatEnchant.Find(e.Id) is { } enchant
                              && (enchant.FirstRating == rating || enchant.SecondRating == rating) ? e.Rank : 0);
}
