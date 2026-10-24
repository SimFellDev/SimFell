using FellowshipSim.Core.Customization.Augmentations;
using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Customization.Weapons;

namespace FellowshipSim.Core;

/// <summary>
/// Every hero, talent, augmentation, glyph, relic passive, legendary and weapon the engine has, found by their class:
/// adding a class with its ID constant is all it takes for builds and combat logs to use it. Classes must have a
/// constructor without parameters. The constructors by ID are written at build time by FellowshipSim.Core.Generators'
/// CatalogGenerator.
/// </summary>
public static partial class Catalog
{
    /// <summary>Hero classes by name (the class name, e.g. "Rime").</summary>
    public static IReadOnlyDictionary<string, Type> Heroes { get; } = HeroClasses.ToDictionary(h => h.Name, h => h.Type, StringComparer.OrdinalIgnoreCase);

    public static bool HasHero(string name) => Heroes.ContainsKey(name);

    public static Hero CreateHero(string name) =>
        NewHero(name) ?? throw new NotSupportedException($"{name} is not supported yet (supported: {string.Join(", ", Heroes.Keys)}).");

    public static Talent? CreateTalent(int id) => NewTalent(id);
    public static Augmentation? CreateAugmentation(int id, int rank) => NewAugmentation(id, rank);
    public static Glyph? CreateGlyph(int id, int rank) => NewGlyph(id, rank);
    public static Weapon? CreateWeapon(int weaponAbilityId) => NewWeapon(weaponAbilityId);
    public static RelicPassive? CreateRelicPassive(int id) => NewRelicPassive(id);

    /// <summary>The passive the relic item <paramref name="itemId"/> grants, or null for an item that is no relic.</summary>
    public static RelicPassive? RelicPassiveOfItem(int itemId) => AllRelicPassives.FirstOrDefault(r => r.ItemIds.Contains(itemId));

    /// <summary>
    /// A glyph's game ID from its name (0 if unknown). Combat logs give glyphs per-item IDs, so they are matched by name.
    /// </summary>
    public static int GlyphIdByName(string name) => GlyphIdOf(name);

    public static Legendary? CreateLegendary(string hero, int itemId) => NewLegendary(hero.ToLowerInvariant(), itemId);

    /// <summary>One instance of every augmentation (aspects, sigils and protective enchants), at rank 1.</summary>
    public static IEnumerable<Augmentation> AllAugmentations => NewAugmentations();

    /// <summary>One instance of every glyph, at rank 1.</summary>
    public static IEnumerable<Glyph> AllGlyphs => NewGlyphs();

    /// <summary>One instance of every weapon.</summary>
    public static IEnumerable<Weapon> AllWeapons => NewWeapons();

    /// <summary>One instance of every relic passive.</summary>
    public static IEnumerable<RelicPassive> AllRelicPassives => NewRelicPassives();

    /// <summary>The augmentation, glyph, weapon, relic passive and legendary classes <paramref name="hero"/> can have.</summary>
    public static IEnumerable<Type> GearTypes(string hero) =>
        AugmentationClasses.Concat(GlyphClasses).Concat(WeaponClasses).Concat(RelicPassiveClasses)
            .Concat(LegendaryClasses.Where(l => l.Hero == hero.ToLowerInvariant()).Select(l => l.Type))
            .Distinct();
}
