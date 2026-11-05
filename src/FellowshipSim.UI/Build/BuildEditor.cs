using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.ProtectiveEnchants;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Customization.StatEnchants;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using CoreCatalog = FellowshipSim.Core.Catalog;
using Hero = FellowshipSim.Core.Domain.Hero;

namespace FellowshipSim.UI.Build;

/// <summary>
/// The changes the build editor makes to a <see cref="CharacterBuild"/>: the gear's raw stats, with the totals a sim
/// starts from beside them. Editing a stat clears an old profile's mark (<see cref="CharacterBuild.StatsFromOldProfile"/>).
/// </summary>
public sealed class BuildEditor(CharacterBuild build)
{
    public CharacterBuild Build { get; } = build;

    public BuildCatalog Catalog => BuildCatalog.For(Build.Hero);

    /// <summary>The rated stats in the in-game character sheet's order.</summary>
    public static IReadOnlyList<RatedStat> InGameOrder { get; } = [RatedStat.Crit, RatedStat.Expertise, RatedStat.Haste, RatedStat.Spirit];

    /// <summary>A new build of <paramref name="hero"/> with nothing picked.</summary>
    public static CharacterBuild NewBuild(string hero) => new() { Hero = hero, Name = "New build" };

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

    /// <summary>The stat's rating as a sim starts with it: the gear's, the stat enchants', the gem nodes' and the sigils'.</summary>
    public double TotalRating(RatedStat stat)
    {
        var hero = Totals;
        var type = RatingType(stat);
        double rating = stat switch
        {
            RatedStat.Crit => hero.Stats.CritRating,
            RatedStat.Haste => hero.Stats.HasteRating,
            RatedStat.Expertise => hero.Stats.ExpertiseRating,
            _ => hero.Stats.SpiritRating,
        };
        return rating + hero.Auras.Fold(type, 0.0, SimTime.Zero);
    }

    /// <summary>The main stat as a sim starts with it: the gear's, the base, and every bonus.</summary>
    public double TotalMainStat => Totals.PrimaryAttribute(SimTime.Zero);

    /// <summary>Stamina as a sim starts with it.</summary>
    public double TotalStamina => Totals.Stats.TotalStamina;

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
        Build.StatsFromOldProfile = false;
    }

    /// <summary>Sets the gear's main stat.</summary>
    public void SetPrimaryStat(double value)
    {
        Build.PrimaryStat = Math.Max(0, value);
        Build.StatsFromOldProfile = false;
    }

    /// <summary>Sets the gear's Stamina.</summary>
    public void SetStamina(double value)
    {
        Build.Stamina = Math.Max(0, value);
        Build.StatsFromOldProfile = false;
    }

    /// <summary>The stat's character-sheet percentage (a fraction), as a sim starts with it.</summary>
    public double Percent(RatedStat stat)
    {
        var hero = Totals;
        return stat switch
        {
            RatedStat.Crit => hero.CritChance(SimTime.Zero),
            RatedStat.Haste => hero.GetHaste(SimTime.Zero),
            RatedStat.Expertise => hero.Expertise(SimTime.Zero),
            _ => hero.SpiritPercent(SimTime.Zero),
        };
    }

    private string? _totalsFor;
    private Hero? _totals;

    /// <summary>
    /// The build imported as a sim imports it (<see cref="HeroImporter"/>), at the start of a fight: the one place its
    /// totals come from. Worked out again only when the build changes.
    /// </summary>
    private Hero Totals
    {
        get
        {
            string key = System.Text.Json.JsonSerializer.Serialize(Build);
            if (key != _totalsFor || _totals == null)
            {
                _totals = HeroImporter.ImportForSheet(Build);
                _totals.ResetForIteration();
                _totalsFor = key;
            }
            return _totals;
        }
    }

    private static StatType RatingType(RatedStat stat) => stat switch
    {
        RatedStat.Crit => StatType.CritRating,
        RatedStat.Haste => StatType.HasteRating,
        RatedStat.Expertise => StatType.ExpertiseRating,
        _ => StatType.SpiritRating,
    };

    /// <summary>The rating the build's stat enchant <paramref name="id"/> adds to each of its stats; 0 when it has none.</summary>
    public double StatEnchantRating(int id) => Build.StatEnchants.FirstOrDefault(e => e.Id == id)?.Rank ?? 0;

    /// <summary>
    /// Sets the rating a stat enchant adds to each of its two stats (the gear's ratings are without it).
    /// </summary>
    public void SetStatEnchantRating(StatEnchant enchant, double rating)
    {
        int value = (int)Math.Max(0, Math.Round(rating));
        // A rating set by hand leaves the log's count of the enchant: it counts as its rating over 6.
        Build.StatEnchantCounts.Remove(enchant.Id);
        int at = Build.StatEnchants.FindIndex(e => e.Id == enchant.Id);
        if (value == 0)
        {
            if (at >= 0) Build.StatEnchants.RemoveAt(at);
        }
        else if (at >= 0) Build.StatEnchants[at] = Build.StatEnchants[at] with { Rank = value };
        else Build.StatEnchants.Add(new BuildEntry(enchant.Id, value));
    }

    /// <summary>True when the stats came from an old profile and likely count bonuses twice.</summary>
    public bool StatsFromOldProfile => Build.StatsFromOldProfile;

    /// <summary>The most tempers an item takes, and the tempers a slot starts with.</summary>
    public const int DefaultTempers = 8;

    // The tempers picked for each slot in the picker, for the items shown there; a slot not here has its piece's, else 8.
    private readonly Dictionary<GearSlot, int> _slotTempers = new();

    /// <summary>The items the hero can wear in <paramref name="slot"/>.</summary>
    public IEnumerable<GearItem> ItemsFor(GearSlot slot) => GearItem.Of(Build.Hero).Where(i => i.Kind == GearSlots.Kind(slot));

    /// <summary>
    /// The piece worn in <paramref name="slot"/>, or null. A build made before builds held their gear shows its relics,
    /// legendary and weapon in their slots, at their most tempers.
    /// </summary>
    public GearPiece? Piece(GearSlot slot) => Build.Gear.Count > 0 ? Build.Gear.FirstOrDefault(p => p.Slot == slot) : Implied().FirstOrDefault(p => p.Slot == slot);

    /// <summary>The item worn in <paramref name="slot"/>, or null.</summary>
    public GearItem? Item(GearSlot slot) => Piece(slot) is { } piece ? GearItem.Find(Build.Hero, piece.ItemId) : null;

    /// <summary>The slots with an item worn.</summary>
    public int GearWorn => GearSlots.All.Count(s => Item(s) != null);

    /// <summary>
    /// The tempers of <paramref name="slot"/>: the ones picked for it, else its piece's (a legendary, which takes none,
    /// leaves the slot at <see cref="DefaultTempers"/>).
    /// </summary>
    public int Tempers(GearSlot slot) =>
        _slotTempers.TryGetValue(slot, out int tempers) ? tempers
        : Item(slot) is { MaxTempers: > 0 } && Piece(slot) is { } piece ? piece.Tempers
        : DefaultTempers;

    /// <summary>Wears item <paramref name="itemId"/> in <paramref name="slot"/> at the slot's tempers, or empties it for null.</summary>
    public void Wear(GearSlot slot, int? itemId)
    {
        var gear = GearSlots.All.Select(Piece).OfType<GearPiece>().Where(p => p.Slot != slot).ToList();
        if (itemId is { } id && GearItem.Find(Build.Hero, id) is { } item && item.Kind == GearSlots.Kind(slot))
        {
            gear.Add(new GearPiece(slot, id, Math.Min(Tempers(slot), item.MaxTempers)));
        }
        SetGear(gear);
    }

    /// <summary>Sets the tempers of <paramref name="slot"/> (0 to <see cref="DefaultTempers"/>), and of its piece when it takes them.</summary>
    public void SetTempers(GearSlot slot, int tempers)
    {
        _slotTempers[slot] = Math.Clamp(tempers, 0, DefaultTempers);
        if (Piece(slot) is { } piece && Item(slot) is { MaxTempers: > 0 } item)
        {
            var gear = GearSlots.All.Select(Piece).OfType<GearPiece>().Where(p => p.Slot != slot).ToList();
            gear.Add(piece with { Tempers = Math.Min(_slotTempers[slot], item.MaxTempers) });
            SetGear(gear);
        }
    }

    /// <summary>
    /// Wears <paramref name="gear"/>: the raw stats become its pieces' stats summed, and the relics, legendary and weapon
    /// its own. A log's maximum health no longer holds.
    /// </summary>
    private void SetGear(List<GearPiece> gear)
    {
        Build.Gear = gear.OrderBy(p => p.Slot).ToList();
        var worn = Build.Gear.Select(p => (Piece: p, Item: GearItem.Find(Build.Hero, p.ItemId))).Where(w => w.Item != null).ToList();
        var stats = worn.Aggregate(new GearStats(), (sum, w) => sum + w.Item!.StatsAt(w.Piece.Tempers));
        Build.Stamina = stats.Stamina;
        Build.PrimaryStat = stats.Primary;
        Build.CritRating = stats.Crit;
        Build.ExpertiseRating = stats.Expertise;
        Build.HasteRating = stats.Haste;
        Build.SpiritRating = stats.Spirit;
        Build.MaxHealth = 0;
        Build.StatsFromOldProfile = false;
        int RelicOf(GearSlot slot) => worn.FirstOrDefault(w => w.Piece.Slot == slot).Piece is { } p && CoreCatalog.RelicItemOf(p.ItemId) != null ? p.ItemId : 0;
        Build.Relic1ItemId = RelicOf(GearSlot.Relic1);
        Build.Relic2ItemId = RelicOf(GearSlot.Relic2);
        Build.WeaponAbilityId = worn.FirstOrDefault(w => w.Piece.Slot == GearSlot.Weapon).Item?.WeaponAbilityId ?? 0;
        Build.LegendaryItemIds = worn.Where(w => w.Item!.Legendary && BuildHeroes.LegendaryName(Build.Hero, w.Item.Id) != null)
            .Select(w => w.Item!.Id).ToList();
    }

    // A build made before builds held their gear: its relics, legendaries and weapon as pieces at their most tempers.
    private IEnumerable<GearPiece> Implied()
    {
        var items = GearItem.Of(Build.Hero);
        if (items.FirstOrDefault(i => i.Id == Build.Relic1ItemId && i.Kind == GearKind.Relic) is { } relic1) yield return new GearPiece(GearSlot.Relic1, relic1.Id, relic1.MaxTempers);
        if (items.FirstOrDefault(i => i.Id == Build.Relic2ItemId && i.Kind == GearKind.Relic) is { } relic2) yield return new GearPiece(GearSlot.Relic2, relic2.Id, relic2.MaxTempers);
        if (Build.WeaponAbilityId != 0 && items.FirstOrDefault(i => i.Kind == GearKind.Weapon && i.WeaponAbilityId == Build.WeaponAbilityId) is { } weapon)
        {
            yield return new GearPiece(GearSlot.Weapon, weapon.Id, weapon.MaxTempers);
        }
        foreach (var legendary in items.Where(i => i.Legendary && Build.LegendaryItemIds.Contains(i.Id)))
        {
            yield return new GearPiece(GearSlots.Of(legendary.Kind).First(), legendary.Id, legendary.MaxTempers);
        }
    }

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
        set => Build.Relic1ItemId = value ?? 0;
    }

    /// <summary>The item ID of the relic in slot 2, or null for no relic.</summary>
    public int? Relic2
    {
        get => Build.Relic2ItemId == 0 ? null : Build.Relic2ItemId;
        set => Build.Relic2ItemId = value ?? 0;
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

    /// <summary>
    /// How many more of <paramref name="slot"/> the build can take: as many as still fit on its gear with the rest of its
    /// enchants (<see cref="GearFit"/>); protective enchants, up to their limit. With <paramref name="optionId"/>, more of
    /// that one: the weapon's tree takes one of each at most, so one the build has already may fit fewer times.
    /// </summary>
    public int Room(EnchantSlot slot, int? optionId = null) => slot == EnchantSlot.Protective
        ? Math.Max(0, Limit(slot) - Used(slot))
        : GearFit.Room(slot, Used, HasWeapon, Distinct, another: optionId is not { } id || Worn(slot, id) == 0);

    /// <summary>True when every enchant the build has can be worn on its gear (an import can have more).</summary>
    public bool FitsGear => GearFit.Fits(Used, HasWeapon, Distinct);

    /// <summary>
    /// The part of <paramref name="slot"/>'s count the weapon's tree holds: it fills first, with one of each different
    /// one at most.
    /// </summary>
    public int WeaponUsed(EnchantSlot slot) => Math.Min(Math.Min(Used(slot), Distinct(slot)), GearFit.WeaponLimit(slot, HasWeapon));

    /// <summary>How many different ones of <paramref name="slot"/> the build has (stat enchants, aspects, sigils, glyphs).</summary>
    public int Distinct(EnchantSlot slot) => slot switch
    {
        EnchantSlot.StatEnchants => Build.StatEnchants.Count(e => StatEnchantCount(e.Id) > 0),
        EnchantSlot.Aspects => Catalog.Aspects.Count(o => AugmentationRank(o.Id) > 0),
        EnchantSlot.Sigils => Catalog.Sigils.Count(o => AugmentationRank(o.Id) > 0),
        EnchantSlot.Glyphs => Catalog.Glyphs.Count(o => GlyphRank(o.Id) > 0),
        _ => Build.GearGemBonuses.Count(g => g.Value > 0),
    };

    // How many of one stat enchant, aspect, sigil or glyph the build has.
    private int Worn(EnchantSlot slot, int id) => slot switch
    {
        EnchantSlot.StatEnchants => StatEnchantCount(id),
        EnchantSlot.Glyphs => GlyphRank(id),
        _ => AugmentationRank(id),
    };

    /// <summary>The part of <paramref name="slot"/>'s count on the armor and other pieces.</summary>
    public int GearUsed(EnchantSlot slot) => Used(slot) - WeaponUsed(slot);

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
        count = Math.Clamp(count, 0, now + Room(EnchantSlot.StatEnchants, enchant.Id));
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
        rank <= current ? rank : Math.Min(rank, current + Room(SlotOf(option), option.Id));

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

    /// <summary>A colour's gem power from the gear, before the relics' multiplier.</summary>
    public double Gem(GemColor color) => Build.GemPower.GetValueOrDefault(color);

    /// <summary>
    /// A colour's gem power on the character sheet: the gear's times the relics' multiplier (Covetous Greed), rounded
    /// down as the engine does. The build and its profile keep <see cref="Gem"/>.
    /// </summary>
    public double SheetGem(GemColor color)
    {
        double multiplier = new[] { Build.Relic1ItemId, Build.Relic2ItemId }
            .Select(CoreCatalog.RelicPassiveOfItem).OfType<RelicPassive>().Aggregate(1.0, (m, r) => m * r.GemPowerMultiplier);
        return Math.Floor(Gem(color) * multiplier);
    }

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
        RecountGemPower();
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
        RecountGemPower();
    }

    /// <summary>True when the build says where its gem power comes from (sockets or gear bonuses).</summary>
    public bool HasGemSources => Build.SocketedGemIds.Count > 0 || Build.GearGemBonuses.Count > 0;

    /// <summary>
    /// Works out each colour's gem power from the sockets and gear bonuses, before the relics' multiplier (the engine
    /// applies Covetous Greed).
    /// </summary>
    private void RecountGemPower()
    {
        Build.GemPower = GemItem.PowerByColor(Build.SocketedGemIds, Build.GearGemBonuses, 1.0);
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
        // A hero the engine does not have yet keeps every talent of its tree.
        var tree = BuildHeroes.Create(build.Hero).TalentTreeSlots.Select(s => s.TalentId).ToHashSet();
        foreach (int id in build.Talents.Where(id => !tree.Contains(id) || BuildHeroes.TalentName(build.Hero, id) == null).ToList())
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
        foreach (var piece in build.Gear.Where(p => GearItem.Find(build.Hero, p.ItemId)?.Kind != GearSlots.Kind(p.Slot)).ToList())
        {
            build.Gear.Remove(piece);
            removed.Add($"{GearSlots.Label(piece.Slot).ToLowerInvariant()} item {piece.ItemId}");
        }
        // A legendary of the hero's the engine does not model stays: a sim lists it as not modeled.
        foreach (int id in build.LegendaryItemIds.Where(id => BuildHeroes.LegendaryName(build.Hero, id) == null).ToList())
        {
            build.LegendaryItemIds.Remove(id);
            removed.Add($"legendary item {id}");
        }
        // A weapon of the hero's the engine does not model stays: a sim lists it as not modeled.
        if (build.WeaponAbilityId != 0 && CoreCatalog.CreateWeapon(build.WeaponAbilityId) == null
            && GameHeroData.Weapons.GetValueOrDefault(build.Hero)?.ContainsKey(build.WeaponAbilityId) != true)
        {
            removed.Add($"weapon with ability {build.WeaponAbilityId}");
            build.WeaponAbilityId = 0;
        }
        return removed;
    }
}
