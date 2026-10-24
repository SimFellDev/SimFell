using System.Collections.Concurrent;
using FellowshipSim.Core;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;

namespace FellowshipSim.UI.Build;

/// <summary>
/// Everything a build of one hero can have, from the engine's catalog, with the datamine's icons and highest ranks. A
/// choice the engine does not model is marked.
/// </summary>
public sealed class BuildCatalog
{
    /// <summary>The highest rank of an augmentation or glyph the datamine gives none for.</summary>
    public const int DefaultMaxRank = 4;

    private static readonly ConcurrentDictionary<string, BuildCatalog> Heroes = new(StringComparer.OrdinalIgnoreCase);

    private BuildCatalog(string hero)
    {
        Hero = hero;
        Talents = Catalog.CreateHero(hero).TalentTreeSlots
            .Select(slot => new TalentOption(slot, Catalog.CreateTalent(slot.TalentId) is { } talent
                ? new BuildOption(talent.Id, talent.Name, GameIcons.Talents.GetValueOrDefault(talent.Id))
                : new BuildOption(slot.TalentId, $"Talent {slot.TalentId}", GameIcons.Talents.GetValueOrDefault(slot.TalentId), Modeled: false)))
            .ToList();
        Weapons = Catalog.AllWeapons
            .Select(w => new BuildOption(w.WeaponAbilityId, w.Name, GameIcons.WeaponAbilities.GetValueOrDefault(w.WeaponAbilityId)))
            .OrderBy(o => o.Name).ToList();
        Legendaries = Catalog.GearTypes(hero).Where(t => typeof(Legendary).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (Legendary)Activator.CreateInstance(t)!)
            .Where(l => l.ItemIds.Count > 0)
            .Select(l => new BuildOption(l.ItemIds[0], l.Name, GameIcons.LegendaryPowers.GetValueOrDefault(l.LegendaryPowerId), l.IsModeled))
            .OrderBy(o => o.Name).ToList();
        Relics = Catalog.AllRelicPassives
            .SelectMany(r => r.Items.Select(i => new RelicOption(i.Id, i.Name, r.Name)))
            .OrderBy(o => o.Name).ToList();
        Augmentations = Catalog.AllAugmentations
            .Select(t => new BuildOption(t.Id, t.Name, GameIcons.Augmentations.GetValueOrDefault(t.Id), MaxRank: GameIcons.AugmentationMaxRanks.GetValueOrDefault(t.Id, DefaultMaxRank)))
            .OrderBy(o => o.Name).ToList();
        Glyphs = Catalog.AllGlyphs
            .Select(b => new BuildOption(b.Id, b.Name, GameIcons.Glyphs.GetValueOrDefault(b.Id), MaxRank: GameIcons.GlyphMaxRanks.GetValueOrDefault(b.Id, DefaultMaxRank)))
            .OrderBy(o => o.Name).ToList();
    }

    /// <summary>The catalog of <paramref name="hero"/> (a name in <see cref="Catalog.Heroes"/>).</summary>
    public static BuildCatalog For(string hero) => Heroes.GetOrAdd(hero, h => new BuildCatalog(h));

    public string Hero { get; }

    /// <summary>The talent tree, row by row and column by column.</summary>
    public IReadOnlyList<TalentOption> Talents { get; }

    public IReadOnlyList<BuildOption> Weapons { get; }

    public IReadOnlyList<BuildOption> Legendaries { get; }

    public IReadOnlyList<RelicOption> Relics { get; }

    public IReadOnlyList<BuildOption> Augmentations { get; }

    public IReadOnlyList<BuildOption> Glyphs { get; }

    public static IReadOnlyList<GemColor> GemColors { get; } = Enum.GetValues<GemColor>();

    /// <summary>The icon's image of gem colour <paramref name="color"/>, or null.</summary>
    public static string? GemIconUrl(GemColor color) => GameIcons.Gems.TryGetValue(color.ToString(), out var icon) ? GameIcons.Url(icon) : null;

    /// <summary>Every hero of the game (the datamine's), by name; <see cref="Catalog.Heroes"/> are the ones the engine has.</summary>
    public static IReadOnlyList<string> GameHeroes { get; } = GameIcons.Heroes.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The hero's portrait, or null.</summary>
    public static string? PortraitUrl(string hero) => GameIcons.Heroes.TryGetValue(hero, out var icon) ? GameIcons.Url(icon) : null;

    /// <summary>Talent points left to spend: <see cref="Domain.Hero.TalentPoints"/> less the cost of every pick.</summary>
    public int PointsLeft(IEnumerable<int> talents)
    {
        var picked = talents.ToHashSet();
        return FellowshipSim.Core.Domain.Hero.TalentPoints - Talents.Where(t => picked.Contains(t.Slot.TalentId)).Sum(t => t.Slot.Cost);
    }
}
