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

    /// <summary>A new build of <paramref name="hero"/> with nothing picked.</summary>
    public static CharacterBuild NewBuild(string hero) => new() { Hero = hero, Name = "New build", PrimaryStat = 100 };

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

    public void SetGearBonuses(GemColor color, int count)
    {
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

    public void SetAugmentationRank(BuildOption augmentation, int rank) => SetRank(Build.Augmentations, augmentation, rank);

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
        if (ProtectiveEnchantCount >= ProtectiveEnchant.MaxPerBuild) return false;
        SetAugmentationRank(enchant, 1);
        return true;
    }

    public int GlyphRank(int id) => Build.Glyphs.FirstOrDefault(g => g.Id == id)?.Rank ?? 0;

    public void SetGlyphRank(BuildOption glyph, int rank) => SetRank(Build.Glyphs, glyph, rank);

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
