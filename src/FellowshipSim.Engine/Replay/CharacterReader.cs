using FellowshipSim.Core.Customization.StatEnchants;
using System.Text.Json.Nodes;
using FellowshipSim.Core;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Reads the build a fight was played with from the player's combatantinfo event: the gear's own stats, the items worn with
/// their tempers, and talents, augmentations, glyphs, gem sources, relic passive, legendary items and weapon by game ID.
/// <para>
/// The stats are the gear's raw ones, each item's listed attributes summed, before the hero's base stats, stat
/// enchants, gem nodes, sigils and relics, which the engine adds as it imports the build. The combatantinfo totals
/// (crit, strength, ...) include them all, so they are no input here.
/// </para>
/// </summary>
public static class CharacterReader
{
    public const int MaxAugmentationRank = 4;

    /// <summary>The names logs give the gear's stat attributes.</summary>
    private const string CritAttribute = "Critical Strike", HasteAttribute = "Haste", ExpertiseAttribute = "Expertise",
        SpiritAttribute = "Spirit", StaminaAttribute = "Stamina";
    private static readonly string[] PrimaryAttributes = ["Strength", "Agility", "Intellect"];

    public static CharacterBuild FromCombatantInfo(JsonObject combatant, string playerName, string hero)
    {
        // The gear's own stats: every attribute each item lists.
        var gear = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in combatant["gear"]?.AsArray() ?? [])
        {
            foreach (var attribute in item?["attributes"]?.AsArray() ?? [])
            {
                if (Str(attribute?["name"]) is { } name) gear[name] = gear.GetValueOrDefault(name) + Num(attribute?["value"]);
            }
        }

        var build = new CharacterBuild
        {
            Name = playerName,
            Hero = hero,
            // Only the hero's own primary stat is on their gear.
            PrimaryStat = PrimaryAttributes.Max(gear.GetValueOrDefault),
            Stamina = gear.GetValueOrDefault(StaminaAttribute),
            MaxHealth = Num(combatant["health"]),
            Armor = Num(combatant["armor"]),
            CritRating = gear.GetValueOrDefault(CritAttribute),
            HasteRating = gear.GetValueOrDefault(HasteAttribute),
            ExpertiseRating = gear.GetValueOrDefault(ExpertiseAttribute),
            SpiritRating = gear.GetValueOrDefault(SpiritAttribute),
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
        // How many slots each stat enchant fills: on an item one per `StatEnchantSlotRating`, rounded up
        // (combat logs: with it every non-legendary item holds the same number of slots), and one per weapon tree point.
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
            // The item in its slot with its tempers ("upgrades"); rings and relics fill their two slots in gear order.
            if (GearItem.Find(hero, itemId) is { } worn
                && GearSlots.Of(worn.Kind).Where(s => build.Gear.All(g => g.Slot != s)).ToList() is [var slot, ..])
            {
                build.Gear.Add(new GearPiece(slot, itemId, (int)Num(item["upgrades"])));
            }
        }

        // Weapon and neck trees: augmentation IDs + 3,000,000 with the points spent; the weapon ability is listed by its own ID.
        foreach (var t in (combatant["weaponTraits"]?.AsArray() ?? []).Concat(combatant["neckTraits"]?.AsArray() ?? []))
        {
            int id = (int)Num(t?["id"]);
            int points = (int)Num(t?["points"]);
            if (points <= 0) continue;
            // The weapon tree has a node per stat enchant; each point is one more slot of that enchant, `StatEnchantSlotRating`
            // to each of its stats (combat logs: the combatantinfo ratings are the gear's, the enchants' and the gem nodes'
            // with that rating per point; at 1 per point every build with a point fell short).
            if (id >= LogIds.AugmentationOffset && StatEnchant.Find(id - LogIds.AugmentationOffset) != null)
            {
                AddRank(statEnchants, id - LogIds.AugmentationOffset, points * (int)StatEnchantSlotRating, null);
                statEnchantCounts[id - LogIds.AugmentationOffset] = statEnchantCounts.GetValueOrDefault(id - LogIds.AugmentationOffset) + points;
            }
            else if (id >= LogIds.AugmentationOffset) AddRank(augmentations, id - LogIds.AugmentationOffset, points, null, cap: true);
            else if (build.WeaponAbilityId == 0) build.WeaponAbilityId = id;
        }

        // A log without the weapon's tree: the weapon item's own ability.
        if (build.WeaponAbilityId == 0 && build.Gear.FirstOrDefault(p => p.Slot == GearSlot.Weapon) is { } weapon)
        {
            build.WeaponAbilityId = GearItem.Find(hero, weapon.ItemId)?.WeaponAbilityId ?? 0;
        }

        build.Augmentations.AddRange(augmentations.Select(t => new BuildEntry(t.Id, Math.Min(MaxAugmentationRank, t.Rank), t.Name)));
        build.Glyphs.AddRange(glyphs.Select(b => new BuildEntry(b.Id, b.Rank, b.Name)));
        build.StatEnchants.AddRange(statEnchants.Select(e => new BuildEntry(e.Id, e.Rank, e.Name)));
        foreach (var (id, count) in statEnchantCounts) build.StatEnchantCounts[id] = count;
        // The gem power of the sockets and the gear's bonuses, before the relics' multiplier (Covetous Greed).
        foreach (var (color, power) in GemItem.PowerByColor(build.SocketedGemIds, build.GearGemBonuses, 1.0)) build.GemPower[color] = power;
        return build;
    }

    /// <summary>Adds ranks to an entry (or a new entry at the end); tree points cap the total at the maximum rank.</summary>
    /// <summary>The rating of one stat enchant slot on an item (combat logs).</summary>
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

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static double Num(JsonNode? n, double fallback = 0)
    {
        if (n is not JsonValue v) return fallback;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<long>(out var l)) return l;
        return fallback;
    }
}
