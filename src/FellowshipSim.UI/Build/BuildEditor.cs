using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.ProtectiveEnchants;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Customization.StatEnchants;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using CoreCatalog = FellowshipSim.Core.Catalog;

namespace FellowshipSim.UI.Build;

/// <summary>
/// The changes the build editor makes to a <see cref="CharacterBuild"/>. A rating set by hand replaces the log's
/// character-sheet percentage of its stat (<see cref="CharacterBuild.SheetPercents"/>).
/// </summary>
public sealed class BuildEditor(CharacterBuild build)
{
    public CharacterBuild Build { get; } = build;

    public BuildCatalog Catalog => BuildCatalog.For(Build.Hero);

    /// <summary>The main stat every hero has before gear (per the user); the editor takes the gear's bonus over it.</summary>
    public const double BaseMainStat = 100;

    /// <summary>The rated stats in the in-game character sheet's order.</summary>
    public static IReadOnlyList<RatedStat> InGameOrder { get; } = [RatedStat.Crit, RatedStat.Expertise, RatedStat.Haste, RatedStat.Spirit];

    /// <summary>A new build of <paramref name="hero"/> with nothing picked.</summary>
    public static CharacterBuild NewBuild(string hero) => new() { Hero = hero, Name = "New build", PrimaryStat = BaseMainStat };

    /// <summary>The main stat over <see cref="BaseMainStat"/>: what the gear adds.</summary>
    public double MainStatBonus
    {
        get => Math.Max(0, Build.PrimaryStat - BaseMainStat);
        set => Build.PrimaryStat = BaseMainStat + Math.Max(0, value);
    }

    public int PointsLeft => Catalog.PointsLeft(Build.Talents);

    public bool HasTalent(int id) => Build.Talents.Contains(id);

    /// <summary>Picks the talent, or drops it when picked. A pick that costs more than the points left is refused: false.</summary>
    public bool ToggleTalent(TalentOption talent)
    {
        if (Build.Talents.Remove(talent.Slot.TalentId)) return true;
        if (talent.Slot.Cost > PointsLeft) return false;
        Build.Talents.Add(talent.Slot.TalentId);
        return true;
    }

    public void ClearTalents() => Build.Talents.Clear();

    public static string Key(RatedStat stat) => stat.ToString().ToLowerInvariant();

    /// <summary>The percentage (a fraction) a stat has with no rating: 5% crit.</summary>
    public static double BasePercent(RatedStat stat) => stat switch
    {
        RatedStat.Crit => StatBlock.BaseCritChance,
        RatedStat.Spirit => StatBlock.BaseSpirit,
        _ => 0.0,
    };

    public double Rating(RatedStat stat) => stat switch
    {
        RatedStat.Crit => Build.CritRating,
        RatedStat.Haste => Build.HasteRating,
        RatedStat.Expertise => Build.ExpertiseRating,
        _ => Build.SpiritRating,
    };

    /// <summary>The stat's rating with the stat enchants' rating: what the sim and the character sheet count.</summary>
    public double TotalRating(RatedStat stat) => Rating(stat) + Build.EnchantRating(RatingType(stat));

    public void SetRating(RatedStat stat, double rating)
    {
        rating = Math.Max(0, rating);
        switch (stat)
        {
            case RatedStat.Crit: Build.CritRating = rating; break;
            case RatedStat.Haste: Build.HasteRating = rating; break;
            case RatedStat.Expertise: Build.ExpertiseRating = rating; break;
            default: Build.SpiritRating = rating; break;
        }
        Build.SheetPercents.Remove(Key(stat));
    }

    /// <summary>
    /// The stat's character-sheet percentage (a fraction): the log's when the build came from one, otherwise its rating's
    /// with the stat enchants' rating; then the gem nodes' bonus to it, as the sim adds it.
    /// </summary>
    public double Percent(RatedStat stat) =>
        (Build.SheetPercents.TryGetValue(Key(stat), out double sheet) ? sheet
            : BasePercent(stat) + StatDiminishingReturns.FromRating(Rating(stat) + Build.EnchantRating(RatingType(stat))))
        + GemBonus(stat);

    private string? _gemBonusesFor;
    private StatBlock? _gemBonuses;

    /// <summary>
    /// The percentage the build's gems add to <paramref name="stat"/>: its gems bound to a hero as <see cref="HeroImporter"/>
    /// binds them. The gem power is the character sheet's (Covetous Greed included), as a sim's profile reads it. Worked
    /// out again only when the gems change.
    /// </summary>
    private double GemBonus(RatedStat stat)
    {
        string key = $"{Build.Hero}|{string.Join(",", Build.GemPower.OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Value}"))}";
        if (key != _gemBonusesFor)
        {
            var hero = CoreCatalog.CreateHero(Build.Hero);
            foreach (var (color, power) in Build.GemPower)
            {
                var gem = FellowshipSim.Core.Customization.Gems.Gem.Create(color);
                gem.Power = power;
                gem.Bind(hero);
            }
            _gemBonuses = hero.Stats;
            _gemBonusesFor = key;
        }
        return stat switch
        {
            RatedStat.Crit => _gemBonuses!.BonusCritChance,
            RatedStat.Haste => _gemBonuses!.BonusHaste,
            RatedStat.Expertise => _gemBonuses!.BonusExpertise,
            _ => _gemBonuses!.BonusSpirit,
        };
    }

    private static StatType RatingType(RatedStat stat) => stat switch
    {
        RatedStat.Crit => StatType.CritRating,
        RatedStat.Haste => StatType.HasteRating,
        RatedStat.Expertise => StatType.ExpertiseRating,
        _ => StatType.SpiritRating,
    };

    private static RatedStat StatOf(StatType rating) => rating switch
    {
        StatType.CritRating => RatedStat.Crit,
        StatType.HasteRating => RatedStat.Haste,
        StatType.ExpertiseRating => RatedStat.Expertise,
        _ => RatedStat.Spirit,
    };

    /// <summary>The rating the build's stat enchant <paramref name="id"/> adds to each of its stats; 0 when it has none.</summary>
    public double StatEnchantRating(int id) => Build.StatEnchants.FirstOrDefault(e => e.Id == id)?.Rank ?? 0;

    /// <summary>
    /// Sets the rating a stat enchant adds. Its two stats leave the log's character sheet (which counted the old enchant)
    /// for their ratings, which are without enchants, plus the enchants.
    /// </summary>
    public void SetStatEnchantRating(StatEnchant enchant, double rating)
    {
        int value = (int)Math.Max(0, Math.Round(rating));
        // A rating set by hand leaves the log's count of the enchant: it counts as its rating over 6.
        Build.StatEnchantCounts.Remove(enchant.Id);
        Build.SheetPercents.Remove(Key(StatOf(enchant.FirstRating)));
        Build.SheetPercents.Remove(Key(StatOf(enchant.SecondRating)));
        int at = Build.StatEnchants.FindIndex(e => e.Id == enchant.Id);
        if (value == 0)
        {
            if (at >= 0) Build.StatEnchants.RemoveAt(at);
        }
        else if (at >= 0) Build.StatEnchants[at] = Build.StatEnchants[at] with { Rank = value };
        else Build.StatEnchants.Add(new BuildEntry(enchant.Id, value));
    }

    /// <summary>True when <see cref="Percent"/> is the log's character sheet, which counts gem nodes too.</summary>
    public bool PercentFromLog(RatedStat stat) => Build.SheetPercents.ContainsKey(Key(stat));

    /// <summary>The weapon's ability ID, or null for no weapon.</summary>
    public int? Weapon
    {
        get => Build.WeaponAbilityId == 0 ? null : Build.WeaponAbilityId;
        set => Build.WeaponAbilityId = value ?? 0;
    }

    /// <summary>The item ID of the legendary worn, or null. A build wears one legendary.</summary>
    public int? Legendary
    {
        get => Build.LegendaryItemIds.Count == 0 ? null : Build.LegendaryItemIds[0];
        set
        {
            Build.LegendaryItemIds.Clear();
            if (value is { } id) Build.LegendaryItemIds.Add(id);
        }
    }

    /// <summary>The item ID of the relic in slot 1, or null for no relic.</summary>
    public int? Relic1
    {
        get => Build.Relic1ItemId == 0 ? null : Build.Relic1ItemId;
        set
        {
            Build.Relic1ItemId = value ?? 0;
            RecountGemPower();
        }
    }

    /// <summary>The item ID of the relic in slot 2, or null for no relic.</summary>
    public int? Relic2
    {
        get => Build.Relic2ItemId == 0 ? null : Build.Relic2ItemId;
        set
        {
            Build.Relic2ItemId = value ?? 0;
            RecountGemPower();
        }
    }

    /// <summary>Relics a build wears: <see cref="Relic1"/> and <see cref="Relic2"/>.</summary>
    public const int RelicSlots = 2;

    public int RelicCount => (Build.Relic1ItemId != 0 ? 1 : 0) + (Build.Relic2ItemId != 0 ? 1 : 0);

    /// <summary>True when the build wears a weapon, which adds slots (<see cref="EnchantSlots"/>).</summary>
    public bool HasWeapon => Build.WeaponAbilityId != 0;

    /// <summary>The enchants of <paramref name="slot"/> the build wears: ranks of aspects, sigils and glyphs, stat enchants, gem power bonuses, protective enchants.</summary>
    public int Used(EnchantSlot slot) => slot switch
    {
        EnchantSlot.StatEnchants => Build.StatEnchants.Sum(e => StatEnchantCount(e.Id)),
        EnchantSlot.Gems => Build.GearGemBonuses.Values.Sum(),
        EnchantSlot.Aspects => Catalog.Aspects.Sum(o => AugmentationRank(o.Id)),
        EnchantSlot.Sigils => Catalog.Sigils.Sum(o => AugmentationRank(o.Id)),
        EnchantSlot.Glyphs => Catalog.Glyphs.Sum(o => GlyphRank(o.Id)),
        _ => ProtectiveEnchantCount,
    };

    /// <summary>The limit of <paramref name="slot"/> for this build (a weapon raises some).</summary>
    public int Limit(EnchantSlot slot) => EnchantSlots.Limit(slot, HasWeapon);

    /// <summary>Enchants the build wears in all, toward <see cref="TotalLimit"/>: every kind but the protective enchants.</summary>
    public int UsedTotal => Enum.GetValues<EnchantSlot>().Where(s => s != EnchantSlot.Protective).Sum(Used);

    /// <summary>Enchants the build can wear in all.</summary>
    public int TotalLimit => EnchantSlots.TotalLimit(HasWeapon);

    /// <summary>How many more of <paramref name="slot"/> the build can take: its own limit and the total's.</summary>
    public int Room(EnchantSlot slot) => Math.Max(0, slot == EnchantSlot.Protective
        ? Limit(slot) - Used(slot)
        : Math.Min(Limit(slot) - Used(slot), TotalLimit - UsedTotal));

    /// <summary>Stat enchants that give <paramref name="rating"/> (<see cref="EnchantSlots.RatingPerStatEnchant"/> each, rounded up).</summary>
    public static int StatEnchantsFor(double rating) => (int)Math.Ceiling(rating / EnchantSlots.RatingPerStatEnchant - 1e-9);

    /// <summary>
    /// How many of stat enchant <paramref name="id"/> the build wears: as a log counted them, otherwise its rating over 6.
    /// </summary>
    public int StatEnchantCount(int id) =>
        Build.StatEnchantCounts.TryGetValue(id, out int count) && StatEnchantRating(id) > 0 ? count : StatEnchantsFor(StatEnchantRating(id));

    /// <summary>
    /// Wears <paramref name="count"/> of a stat enchant (<see cref="EnchantSlots.RatingPerStatEnchant"/> rating each), as
    /// many as there is room for. The same count keeps the rating it has (a log's exact rating).
    /// </summary>
    public void SetStatEnchantCount(StatEnchant enchant, int count)
    {
        int now = StatEnchantCount(enchant.Id);
        count = Math.Clamp(count, 0, now + Room(EnchantSlot.StatEnchants));
        if (count == now) return;
        SetStatEnchantRating(enchant, count * EnchantSlots.RatingPerStatEnchant);
        if (count > 0) Build.StatEnchantCounts[enchant.Id] = count;
    }

    // The slot an augmentation or glyph fills.
    private EnchantSlot SlotOf(BuildOption option) =>
        Catalog.Aspects.Any(o => o.Id == option.Id) ? EnchantSlot.Aspects
        : Catalog.Sigils.Any(o => o.Id == option.Id) ? EnchantSlot.Sigils
        : Catalog.Glyphs.Any(o => o.Id == option.Id) ? EnchantSlot.Glyphs
        : EnchantSlot.Protective;

    // A rank raised no further than the room its slot has.
    private int Allowed(BuildOption option, int current, int rank) =>
        rank <= current ? rank : Math.Min(rank, current + Room(SlotOf(option)));

    public bool HasRelic(int itemId) => itemId != 0 && (Build.Relic1ItemId == itemId || Build.Relic2ItemId == itemId);

    /// <summary>
    /// Wears relic item <paramref name="itemId"/> in the first empty slot, or takes it off when worn. With both slots
    /// full, a new relic is refused: false.
    /// </summary>
    public bool ToggleRelic(int itemId)
    {
        if (Build.Relic1ItemId == itemId) Relic1 = null;
        else if (Build.Relic2ItemId == itemId) Relic2 = null;
        else if (Build.Relic1ItemId == 0) Relic1 = itemId;
        else if (Build.Relic2ItemId == 0) Relic2 = itemId;
        else return false;
        return true;
    }

    /// <summary>Puts gem <paramref name="gemId"/> in the next empty socket. With every socket filled it is refused: false.</summary>
    public bool AddGem(int gemId)
    {
        if (Build.SocketedGemIds.Count >= GemItem.Sockets) return false;
        SetSocket(Build.SocketedGemIds.Count, gemId);
        return true;
    }

    /// <summary>How many of gem <paramref name="gemId"/> are socketed.</summary>
    public int SocketedCount(int gemId) => Build.SocketedGemIds.Count(id => id == gemId);

    /// <summary>A colour's gem power as the character sheet shows it.</summary>
    public double Gem(GemColor color) => Build.GemPower.GetValueOrDefault(color);

    public void SetGem(GemColor color, double power)
    {
        if (power > 0) Build.GemPower[color] = power;
        else Build.GemPower.Remove(color);
    }

    /// <summary>The gem in socket <paramref name="socket"/> (0 to <see cref="GemItem.Sockets"/> - 1), or null when empty.</summary>
    public GemItem? Socket(int socket) => socket < Build.SocketedGemIds.Count ? GemItem.Find(Build.SocketedGemIds[socket]) : null;

    /// <summary>Puts gem <paramref name="gemId"/> in socket <paramref name="socket"/>, or empties it for null.</summary>
    public void SetSocket(int socket, int? gemId)
    {
        var sockets = Enumerable.Range(0, GemItem.Sockets).Select(i => Socket(i)?.Id).ToArray();
        sockets[socket] = gemId is { } id && GemItem.Find(id) != null ? id : null;
        Build.SocketedGemIds = sockets.OfType<int>().ToList();
        RecountGemPower(always: true);
    }

    /// <summary>The gear's gem power bonuses of <paramref name="color"/> (<see cref="GemItem.GearBonusPower"/> each).</summary>
    public int GearBonuses(GemColor color) => Build.GearGemBonuses.GetValueOrDefault(color);

    /// <summary>Sets the gear's gem power bonuses of a colour, raised no further than the gems' room.</summary>
    public void SetGearBonuses(GemColor color, int count)
    {
        int current = GearBonuses(color);
        if (count > current) count = Math.Min(count, current + Room(EnchantSlot.Gems));
        if (count > 0) Build.GearGemBonuses[color] = count;
        else Build.GearGemBonuses.Remove(color);
        RecountGemPower(always: true);
    }

    /// <summary>True when the build says where its gem power comes from (sockets or gear bonuses).</summary>
    public bool HasGemSources => Build.SocketedGemIds.Count > 0 || Build.GearGemBonuses.Count > 0;

    /// <summary>
    /// Works out each colour's gem power from the sockets and gear bonuses, times the relics' multiplier (Covetous Greed).
    /// A relic change on a build that does not say where its gem power comes from keeps the totals it has.
    /// </summary>
    private void RecountGemPower(bool always = false)
    {
        if (!always && !HasGemSources) return;
        double multiplier = new[] { Build.Relic1ItemId, Build.Relic2ItemId }
            .Select(CoreCatalog.RelicPassiveOfItem).OfType<RelicPassive>().Aggregate(1.0, (m, r) => m * r.GemPowerMultiplier);
        Build.GemPower = GemItem.PowerByColor(Build.SocketedGemIds, Build.GearGemBonuses, multiplier);
    }

    public int AugmentationRank(int id) => Build.Augmentations.FirstOrDefault(a => a.Id == id)?.Rank ?? 0;

    /// <summary>Sets an augmentation's rank, raised no further than its slot's room.</summary>
    public void SetAugmentationRank(BuildOption augmentation, int rank) =>
        SetRank(Build.Augmentations, augmentation, Allowed(augmentation, AugmentationRank(augmentation.Id), rank));

    /// <summary>Protective enchants the build has, of the catalog's <see cref="BuildCatalog.ProtectiveEnchants"/>.</summary>
    public int ProtectiveEnchantCount => Catalog.ProtectiveEnchants.Count(p => AugmentationRank(p.Id) > 0);

    /// <summary>
    /// Takes the protective enchant, or drops it when taken. Taking one more than <see cref="ProtectiveEnchant.MaxPerBuild"/>
    /// is refused: false.
    /// </summary>
    public bool ToggleProtectiveEnchant(BuildOption enchant)
    {
        if (AugmentationRank(enchant.Id) > 0)
        {
            SetAugmentationRank(enchant, 0);
            return true;
        }
        if (Room(EnchantSlot.Protective) == 0) return false;
        SetAugmentationRank(enchant, 1);
        return true;
    }

    public int GlyphRank(int id) => Build.Glyphs.FirstOrDefault(g => g.Id == id)?.Rank ?? 0;

    /// <summary>Sets a glyph's rank, raised no further than the glyphs' room.</summary>
    public void SetGlyphRank(BuildOption glyph, int rank) => SetRank(Build.Glyphs, glyph, Allowed(glyph, GlyphRank(glyph.Id), rank));

    private static void SetRank(List<BuildEntry> entries, BuildOption option, int rank)
    {
        rank = Math.Clamp(rank, 0, option.MaxRank);
        int at = entries.FindIndex(e => e.Id == option.Id);
        if (rank == 0)
        {
            if (at >= 0) entries.RemoveAt(at);
        }
        else if (at >= 0) entries[at] = entries[at] with { Rank = rank };
        else entries.Add(new BuildEntry(option.Id, rank, option.Name));
    }

    /// <summary>
    /// Removes the parts of an imported build the catalog has no class for, so the build writes as a profile, and returns
    /// their names.
    /// </summary>
    public static IReadOnlyList<string> RemoveUnknown(CharacterBuild build)
    {
        var removed = new List<string>();
        var tree = CoreCatalog.CreateHero(build.Hero).TalentTreeSlots.Select(s => s.TalentId).ToHashSet();
        foreach (int id in build.Talents.Where(id => !tree.Contains(id) || CoreCatalog.CreateTalent(id) == null).ToList())
        {
            build.Talents.Remove(id);
            removed.Add($"talent {id}");
        }
        foreach (var a in build.Augmentations.Where(a => CoreCatalog.CreateAugmentation(a.Id, a.Rank) == null).ToList())
        {
            build.Augmentations.Remove(a);
            removed.Add($"augmentation {a.Name ?? a.Id.ToString()}");
        }
        foreach (var g in build.Glyphs.Where(g => CoreCatalog.CreateGlyph(g.Id, g.Rank) == null).ToList())
        {
            build.Glyphs.Remove(g);
            removed.Add($"glyph {g.Name ?? g.Id.ToString()}");
        }
        if (build.Relic1ItemId != 0 && CoreCatalog.RelicPassiveOfItem(build.Relic1ItemId) == null)
        {
            removed.Add($"relic item {build.Relic1ItemId}");
            build.Relic1ItemId = 0;
        }
        if (build.Relic2ItemId != 0 && CoreCatalog.RelicPassiveOfItem(build.Relic2ItemId) == null)
        {
            removed.Add($"relic item {build.Relic2ItemId}");
            build.Relic2ItemId = 0;
        }
        foreach (int id in build.LegendaryItemIds.Where(id => CoreCatalog.CreateLegendary(build.Hero, id) == null).ToList())
        {
            build.LegendaryItemIds.Remove(id);
            removed.Add($"legendary item {id}");
        }
        if (build.WeaponAbilityId != 0 && CoreCatalog.CreateWeapon(build.WeaponAbilityId) == null)
        {
            removed.Add($"weapon with ability {build.WeaponAbilityId}");
            build.WeaponAbilityId = 0;
        }
        return removed;
    }
}
