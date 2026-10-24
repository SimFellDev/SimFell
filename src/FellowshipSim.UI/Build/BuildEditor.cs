using FellowshipSim.Core.Customization.Gems;
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

    /// <summary>The stat's character-sheet percentage (a fraction): the log's when the build came from one, otherwise its rating's.</summary>
    public double Percent(RatedStat stat) =>
        Build.SheetPercents.TryGetValue(Key(stat), out double sheet) ? sheet : BasePercent(stat) + StatDiminishingReturns.FromRating(Rating(stat));

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
        set => Build.Relic1ItemId = value ?? 0;
    }

    /// <summary>The item ID of the relic in slot 2, or null for no relic.</summary>
    public int? Relic2
    {
        get => Build.Relic2ItemId == 0 ? null : Build.Relic2ItemId;
        set => Build.Relic2ItemId = value ?? 0;
    }

    public double Gem(GemColor color) => Build.GemPower.GetValueOrDefault(color);

    public void SetGem(GemColor color, double power)
    {
        if (power > 0) Build.GemPower[color] = power;
        else Build.GemPower.Remove(color);
    }

    public int AugmentationRank(int id) => Build.Augmentations.FirstOrDefault(a => a.Id == id)?.Rank ?? 0;

    public void SetAugmentationRank(BuildOption augmentation, int rank) => SetRank(Build.Augmentations, augmentation, rank);

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
