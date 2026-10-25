using FellowshipSim.Core.Customization.StatEnchants;
using System.Text.Json.Nodes;
using FellowshipSim.Core;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Reads the build a fight was played with from the player's combatantinfo event: character-sheet stats, and talents,
/// augmentations, glyphs, gem powers, relic passive, legendary items and weapon by game ID.
/// <para>
/// Base percentages are derived from the logged ratings: every hero has 5% base Critical Strike and no base Spirit, and
/// ratings convert at 0.16% per point through the diminishing-returns curve. Percentage bonuses from gem nodes and set
/// bonuses are added by their own classes.
/// </para>
/// </summary>
public static class CharacterReader
{
    public const double PercentPerRating = 0.0016;
    public const double BaseCrit = 0.05;
    public const double BaseSpirit = 0.0;
    public const int MaxAugmentationRank = 4;

    private static readonly double[] Breakpoints = [0.10, 0.15, 0.20, 0.25];
    private static readonly double[] Multipliers = [1.0, 0.98, 0.96 * 0.98, 0.94 * 0.96 * 0.98, 0.92 * 0.94 * 0.96 * 0.98];

    /// <summary>Gem colours in the order the build lists them.</summary>
    private static readonly GemColor[] GemOrder = [GemColor.Sapphire, GemColor.Amethyst, GemColor.Emerald, GemColor.Ruby, GemColor.Diamond, GemColor.Topaz];

    public static CharacterBuild FromCombatantInfo(JsonObject combatant, string playerName, string hero)
    {
        var gemPower = GemOrder.ToDictionary(c => c, c => Num(combatant[c.ToString().ToLowerInvariant()]));

        // ---- Base percentages from the ratings (gem and set percentage bonuses are added by their classes).
        double crit = BaseCrit + Diminishing(Num(combatant["crit"]) * PercentPerRating);
        double haste = Diminishing(Num(combatant["haste"]) * PercentPerRating);
        double expertise = Diminishing(Num(combatant["expertise"]) * PercentPerRating);
        double spirit = BaseSpirit + Diminishing(Num(combatant["spirit"]) * PercentPerRating);
        static double Sheet(double value) => Math.Round(value * 10000) / 10000.0;

        var build = new CharacterBuild
        {
            Name = playerName,
            Hero = hero,
            // Only the hero's own primary stat is non-zero.
            PrimaryStat = new[] { "strength", "agility", "intellect" }.Max(k => Num(combatant[k])),
            Stamina = Num(combatant["stamina"]),
            MaxHealth = Num(combatant["health"]),
            Armor = Num(combatant["armor"]),
            CritRating = Num(combatant["crit"]),
            HasteRating = Num(combatant["haste"]),
            ExpertiseRating = Num(combatant["expertise"]),
            SpiritRating = Num(combatant["spirit"]),
            SheetPercents = new(StringComparer.OrdinalIgnoreCase)
            {
                ["crit"] = Sheet(crit),
                ["haste"] = Sheet(haste),
                ["expertise"] = Sheet(expertise),
                ["spirit"] = Sheet(spirit),
            },
        };

        // Talents: talent ID + 2,000,000.
        foreach (var t in combatant["talents"]?.AsArray() ?? [])
        {
            int id = (int)Num(t?["id"]) - LogIds.TalentOffset;
            if (id > 0) build.Talents.Add(id);
        }

        // Gear: augmentations (augmentation ID + 3,000,000), glyphs, the relics and legendary items. Glyphs carry per-item
        // IDs in logs, so they are matched to the game's glyphs by name.
        var augmentations = new List<(int Id, int Rank, string? Name)>();
        var glyphs = new List<(int Id, int Rank, string? Name)>();
        var statEnchants = new List<(int Id, int Rank, string? Name)>();
        // How many slots each stat enchant fills: on an item one per 6 rating, rounded up (6 is one, 10 and 12 are two;
        // combat logs: with it every non-legendary item holds exactly 3 slots, and 40 of 46 players of patch 0.5.0.0 fill
        // the 44), and one per weapon tree point.
        var statEnchantCounts = new Dictionary<int, int>();
        foreach (var item in combatant["gear"]?.AsArray() ?? [])
        {
            if (item == null) continue;
            int itemId = (int)Num(item["id"]);
            if (Catalog.RelicItemOf(itemId) != null)
            {
                // Relic slots fill in gear order.
                if (build.Relic1ItemId == 0) build.Relic1ItemId = itemId;
                else build.Relic2ItemId = itemId;
            }
            foreach (var t in item["traits"]?.AsArray() ?? [])
            {
                int id = (int)Num(t?["id"]) - LogIds.AugmentationOffset;
                int rank = (int)Num(t?["rank"], 1);
                // A stat enchant's rank is its rating, added to the item's other enchants of the same ID.
                if (StatEnchant.Find(id) != null)
                {
                    AddRank(statEnchants, id, rank, Str(t?["name"]));
                    statEnchantCounts[id] = statEnchantCounts.GetValueOrDefault(id) + (int)Math.Ceiling(rank / StatEnchantSlotRating);
                }
                else AddRank(augmentations, id, rank, Str(t?["name"]));
            }
            foreach (var b in item["blessings"]?.AsArray() ?? [])
            {
                string name = Str(b?["name"]) ?? "";
                int i = glyphs.FindIndex(x => x.Name == name);
                if (i < 0) glyphs.Add((Catalog.GlyphIdByName(name), (int)Num(b?["level"], 1), name));
                else glyphs[i] = (glyphs[i].Id, glyphs[i].Rank + (int)Num(b?["level"], 1), name);
            }
            // The gem in the item's socket, and the item's gem power bonuses (each names its colour).
            if (GemItem.Find((int)Num(item["gem"]?["id"])) is { } gem) build.SocketedGemIds.Add(gem.Id);
            foreach (var bonus in item["gemPowers"]?.AsArray() ?? [])
            {
                if (Enum.TryParse(Str(bonus?["name"]), out GemColor color)) build.GearGemBonuses[color] = build.GearGemBonuses.GetValueOrDefault(color) + 1;
            }
            if (Num(item["quality"]) >= 6) build.LegendaryItemIds.Add(itemId);
        }

        // Weapon and neck trees: augmentation IDs + 3,000,000 with the points spent; the weapon ability is listed by its own ID.
        foreach (var t in (combatant["weaponTraits"]?.AsArray() ?? []).Concat(combatant["neckTraits"]?.AsArray() ?? []))
        {
            int id = (int)Num(t?["id"]);
            int points = (int)Num(t?["points"]);
            if (points <= 0) continue;
            // The weapon tree has a node per stat enchant (65 to 70); each point adds 1 to that enchant (per the user).
            if (id >= LogIds.AugmentationOffset && StatEnchant.Find(id - LogIds.AugmentationOffset) != null)
            {
                AddRank(statEnchants, id - LogIds.AugmentationOffset, points, null);
                statEnchantCounts[id - LogIds.AugmentationOffset] = statEnchantCounts.GetValueOrDefault(id - LogIds.AugmentationOffset) + points;
            }
            else if (id >= LogIds.AugmentationOffset) AddRank(augmentations, id - LogIds.AugmentationOffset, points, null, cap: true);
            else if (build.WeaponAbilityId == 0) build.WeaponAbilityId = id;
        }

        build.Augmentations.AddRange(augmentations.Select(t => new BuildEntry(t.Id, Math.Min(MaxAugmentationRank, t.Rank), t.Name)));
        build.Glyphs.AddRange(glyphs.Select(b => new BuildEntry(b.Id, b.Rank, b.Name)));
        build.StatEnchants.AddRange(statEnchants.Select(e => new BuildEntry(e.Id, e.Rank, e.Name)));
        foreach (var (id, count) in statEnchantCounts) build.StatEnchantCounts[id] = count;
        // A build's ratings are without its stat enchants. UNVERIFIED: the logged ratings are taken to count the enchants
        // (they are the gear's totals); no log has confirmed it.
        build.CritRating = Math.Max(0, build.CritRating - build.EnchantRating(StatType.CritRating));
        build.HasteRating = Math.Max(0, build.HasteRating - build.EnchantRating(StatType.HasteRating));
        build.ExpertiseRating = Math.Max(0, build.ExpertiseRating - build.EnchantRating(StatType.ExpertiseRating));
        build.SpiritRating = Math.Max(0, build.SpiritRating - build.EnchantRating(StatType.SpiritRating));
        foreach (var colour in GemOrder)
        {
            if (gemPower[colour] != 0) build.GemPower[colour] = gemPower[colour];
        }
        return build;
    }

    /// <summary>Adds ranks to an entry (or a new entry at the end); tree points cap the total at the maximum rank.</summary>
    /// <summary>The rating of one stat enchant slot on an item (combat logs: 6 at the current item level).</summary>
    private const double StatEnchantSlotRating = 6.0;

    private static void AddRank(List<(int Id, int Rank, string? Name)> list, int id, int rank, string? name, bool cap = false)
    {
        if (id <= 0) return;
        int i = list.FindIndex(e => e.Id == id);
        if (i < 0)
        {
            list.Add((id, cap ? Math.Min(MaxAugmentationRank, rank) : rank, name));
            return;
        }
        int total = list[i].Rank + rank;
        list[i] = (id, cap ? Math.Min(MaxAugmentationRank, total) : total, list[i].Name ?? name);
    }

    /// <summary>A rating percentage after diminishing returns (the same curve as the engine's StatDiminishingReturns).</summary>
    public static double Diminishing(double unmitigated)
    {
        if (unmitigated <= Breakpoints[0]) return unmitigated;
        double result = Breakpoints[0], remaining = unmitigated - result, current = result;
        for (int i = 1; i < Breakpoints.Length; i++)
        {
            double previous = current;
            current += (Breakpoints[i] - Breakpoints[i - 1]) / Multipliers[i];
            double segment = current - previous;
            if (remaining <= segment) return result + remaining * Multipliers[i];
            result += segment * Multipliers[i];
            remaining -= segment;
        }
        return result + remaining * Multipliers[^1];
    }

    /// <summary>The bonus of the highest unlocked node of a gem whose name starts with <paramref name="namePrefix"/>.</summary>

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static double Num(JsonNode? n, double fallback = 0)
    {
        if (n is not JsonValue v) return fallback;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<long>(out var l)) return l;
        return fallback;
    }
}
