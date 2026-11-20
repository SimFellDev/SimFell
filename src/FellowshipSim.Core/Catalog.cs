using FellowshipSim.Core.Customization.Augmentations;
using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Customization.Weapons;
using FellowshipSim.Core.Dungeons.Affixes;

namespace FellowshipSim.Core;

/// <summary>
/// Every hero, talent, augmentation, glyph, relic passive, legendary, weapon and dungeon affix the engine has, found by their class:
/// adding a class with its ID constant is all it takes for builds and combat logs to use it. Classes must have a
/// constructor without parameters. Every hero class, and every talent, augmentation, glyph, relic passive, legendary,
/// weapon and dungeon affix class's constructor, are written at build time by FellowshipSim.Core.Generators' CatalogGenerator.
/// </summary>
public static partial class Catalog
{
    /// <summary>Hero classes by name (the class name, e.g. "Rime").</summary>
    public static IReadOnlyDictionary<string, Type> Heroes { get; } = HeroClasses.ToDictionary(h => h.Name, h => h.Type, StringComparer.OrdinalIgnoreCase);

    public static bool HasHero(string name) => Heroes.ContainsKey(name);

    /// <summary>Every dungeon affix class, one instance each, in ID order.</summary>
    public static IReadOnlyList<DungeonAffix> DungeonAffixes { get; } = DungeonAffixConstructors.Select(c => c()).OrderBy(a => a.Id).ToList();

    public static Hero CreateHero(string name) =>
        NewHero(name) ?? throw new NotSupportedException($"{name} is not supported yet (supported: {string.Join(", ", Heroes.Keys)}).");

    /// <summary>
    /// Each talent class's constructor by its ID, read from an instance: the game data's half of a talent class has its
    /// <c>TalentId</c>. An ID of 0 is unknown and has no entry; two classes with one ID stop the catalog.
    /// </summary>
    private static readonly Dictionary<int, Func<Talent>> TalentsById = TalentsByIdOf();

    private static Dictionary<int, Func<Talent>> TalentsByIdOf()
    {
        var byId = new Dictionary<int, (Type Type, Func<Talent> New)>();
        foreach (var create in TalentConstructors)
        {
            var talent = create();
            if (talent.Id == 0) continue;
            if (byId.TryGetValue(talent.Id, out var other))
            {
                throw new InvalidOperationException($"{talent.GetType().Name} and {other.Type.Name} share the talent ID {talent.Id}.");
            }
            byId[talent.Id] = (talent.GetType(), create);
        }
        return byId.ToDictionary(e => e.Key, e => e.Value.New);
    }

    public static Talent? CreateTalent(int id) => TalentsById.TryGetValue(id, out var create) ? create() : null;

    /// <summary>
    /// Every augmentation class in declaration order: its ID, type and constructor from a rank. The game data's half of an
    /// augmentation class has its <c>&lt;Type&gt;Id</c>, so the ID is read from an instance. An ID of 0 is unknown and has
    /// no entry; two classes with one ID stop the catalog.
    /// </summary>
    private static readonly (int Id, Type Type, Func<int, Augmentation> New)[] AugmentationClasses = AugmentationClassesOf();

    private static (int Id, Type Type, Func<int, Augmentation> New)[] AugmentationClassesOf()
    {
        var classes = new List<(int Id, Type Type, Func<int, Augmentation> New)>();
        foreach (var create in AugmentationConstructors)
        {
            var augmentation = create(1);
            if (augmentation.Id == 0) continue;
            if (classes.FirstOrDefault(c => c.Id == augmentation.Id) is { Type: { } other })
            {
                throw new InvalidOperationException($"{augmentation.GetType().Name} and {other.Name} share the augmentation ID {augmentation.Id}.");
            }
            classes.Add((augmentation.Id, augmentation.GetType(), create));
        }
        return [.. classes];
    }

    private static readonly Dictionary<int, Func<int, Augmentation>> AugmentationsById = AugmentationClasses.ToDictionary(a => a.Id, a => a.New);

    public static Augmentation? CreateAugmentation(int id, int rank) => AugmentationsById.TryGetValue(id, out var create) ? create(rank) : null;

    /// <summary>
    /// Every glyph class in declaration order: its ID, name, type and constructor from a rank. The game data's half of a
    /// glyph class has its <c>GlyphId</c> and <c>GlyphName</c>, so they are read from an instance. An ID of 0 is unknown
    /// and has no entry; two classes with one ID stop the catalog.
    /// </summary>
    private static readonly (int Id, string Name, Type Type, Func<int, Glyph> New)[] GlyphClasses = GlyphClassesOf();

    private static (int Id, string Name, Type Type, Func<int, Glyph> New)[] GlyphClassesOf()
    {
        var classes = new List<(int Id, string Name, Type Type, Func<int, Glyph> New)>();
        foreach (var create in GlyphConstructors)
        {
            var glyph = create(1);
            if (glyph.Id == 0) continue;
            if (classes.FirstOrDefault(c => c.Id == glyph.Id) is { Type: { } other })
            {
                throw new InvalidOperationException($"{glyph.GetType().Name} and {other.Name} share the glyph ID {glyph.Id}.");
            }
            classes.Add((glyph.Id, glyph.Name, glyph.GetType(), create));
        }
        return [.. classes];
    }

    private static readonly Dictionary<int, Func<int, Glyph>> GlyphsById = GlyphClasses.ToDictionary(g => g.Id, g => g.New);

    private static readonly Dictionary<string, int> GlyphIdsByName = GlyphClasses.ToDictionary(g => g.Name, g => g.Id, StringComparer.OrdinalIgnoreCase);

    public static Glyph? CreateGlyph(int id, int rank) => GlyphsById.TryGetValue(id, out var create) ? create(rank) : null;

    /// <summary>
    /// Every weapon class in declaration order: its weapon ability ID, type and constructor. The game data's half of a
    /// weapon ability class has its <c>AbilityId</c>, so the ID is read from an instance. An ID of 0 is unknown and has no
    /// entry; two classes with one ID stop the catalog.
    /// </summary>
    private static readonly (int Id, Type Type, Func<Weapon> New)[] WeaponClasses = WeaponClassesOf();

    private static (int Id, Type Type, Func<Weapon> New)[] WeaponClassesOf()
    {
        var classes = new List<(int Id, Type Type, Func<Weapon> New)>();
        foreach (var create in WeaponConstructors)
        {
            var weapon = create();
            if (weapon.WeaponAbilityId == 0) continue;
            if (classes.FirstOrDefault(c => c.Id == weapon.WeaponAbilityId) is { Type: { } other })
            {
                throw new InvalidOperationException($"{weapon.GetType().Name} and {other.Name} share the weapon ability ID {weapon.WeaponAbilityId}.");
            }
            classes.Add((weapon.WeaponAbilityId, weapon.GetType(), create));
        }
        return [.. classes];
    }

    private static readonly Dictionary<int, Func<Weapon>> WeaponsById = WeaponClasses.ToDictionary(w => w.Id, w => w.New);

    public static Weapon? CreateWeapon(int weaponAbilityId) => WeaponsById.TryGetValue(weaponAbilityId, out var create) ? create() : null;

    /// <summary>
    /// Every relic passive class in declaration order: its ID, type and constructor. The game data's half of a relic passive
    /// class has its <c>RelicPassiveId</c>, so the ID is read from an instance. An ID of 0 is unknown and has no entry; two
    /// classes with one ID stop the catalog.
    /// </summary>
    private static readonly (int Id, Type Type, Func<RelicPassive> New)[] RelicPassiveClasses = RelicPassiveClassesOf();

    private static (int Id, Type Type, Func<RelicPassive> New)[] RelicPassiveClassesOf()
    {
        var classes = new List<(int Id, Type Type, Func<RelicPassive> New)>();
        foreach (var create in RelicPassiveConstructors)
        {
            var relicPassive = create();
            if (relicPassive.Id == 0) continue;
            if (classes.FirstOrDefault(c => c.Id == relicPassive.Id) is { Type: { } other })
            {
                throw new InvalidOperationException($"{relicPassive.GetType().Name} and {other.Name} share the relic passive ID {relicPassive.Id}.");
            }
            classes.Add((relicPassive.Id, relicPassive.GetType(), create));
        }
        return [.. classes];
    }

    private static readonly Dictionary<int, Func<RelicPassive>> RelicPassivesById = RelicPassiveClasses.ToDictionary(r => r.Id, r => r.New);

    public static RelicPassive? CreateRelicPassive(int id) => RelicPassivesById.TryGetValue(id, out var create) ? create() : null;

    /// <summary>The passive the relic item <paramref name="itemId"/> grants, or null for an item that is no relic.</summary>
    public static RelicPassive? RelicPassiveOfItem(int itemId) => AllRelicPassives.FirstOrDefault(r => r.Items.Any(i => i.Id == itemId));

    /// <summary>The relic item <paramref name="itemId"/>, or null for an item that is no relic.</summary>
    public static RelicItem? RelicItemOf(int itemId) => AllRelicItems.FirstOrDefault(i => i.Id == itemId);

    /// <summary>Every relic item that grants a relic passive.</summary>
    public static IEnumerable<RelicItem> AllRelicItems => AllRelicPassives.SelectMany(r => r.Items);

    /// <summary>
    /// A glyph's game ID from its name (0 if unknown). Combat logs give glyphs per-item IDs, so they are matched by name.
    /// </summary>
    public static int GlyphIdByName(string name) => GlyphIdsByName.TryGetValue(name, out int id) ? id : 0;

    /// <summary>
    /// Every legendary class: its hero (lower case), type, constructor and the item IDs that grant it. The game data's half of a
    /// legendary class has its <c>HeroName</c> and <c>ItemIds</c>, so they are read from an instance.
    /// </summary>
    private static readonly (string Hero, Type Type, Func<Legendary> New, IReadOnlyList<int> ItemIds)[] LegendaryClasses =
    [
        .. LegendaryConstructors.Select(create =>
        {
            var legendary = create();
            return (legendary.HeroName.ToLowerInvariant(), legendary.GetType(), create, legendary.ItemIds);
        }),
    ];

    /// <summary>Each legendary class's constructor by hero (lower case) and item ID; two classes with one key stop the catalog.</summary>
    private static readonly Dictionary<(string Hero, int ItemId), Func<Legendary>> LegendariesByItem =
        LegendaryClasses.SelectMany(l => l.ItemIds.Select(itemId => (Key: (l.Hero, itemId), l.New))).ToDictionary(e => e.Key, e => e.New);

    public static Legendary? CreateLegendary(string hero, int itemId) =>
        LegendariesByItem.TryGetValue((hero.ToLowerInvariant(), itemId), out var create) ? create() : null;

    /// <summary>One instance of every legendary <paramref name="hero"/> can wear.</summary>
    public static IEnumerable<Legendary> Legendaries(string hero) =>
        LegendaryClasses.Where(l => l.Hero == hero.ToLowerInvariant()).Select(l => l.New());

    /// <summary>One instance of every augmentation (aspects, sigils and protective enchants), at rank 1.</summary>
    public static IEnumerable<Augmentation> AllAugmentations => AugmentationClasses.Select(a => a.New(1)).ToArray();

    /// <summary>One instance of every glyph, at rank 1.</summary>
    public static IEnumerable<Glyph> AllGlyphs => GlyphClasses.Select(g => g.New(1)).ToArray();

    /// <summary>One instance of every weapon.</summary>
    public static IEnumerable<Weapon> AllWeapons => WeaponClasses.Select(w => w.New()).ToArray();

    /// <summary>One instance of every relic passive.</summary>
    public static IEnumerable<RelicPassive> AllRelicPassives => RelicPassiveClasses.Select(r => r.New()).ToArray();

    /// <summary>The augmentation, glyph, weapon, relic passive and legendary classes <paramref name="hero"/> can have.</summary>
    public static IEnumerable<Type> GearTypes(string hero) =>
        AugmentationClasses.Select(a => a.Type).Concat(GlyphClasses.Select(g => g.Type)).Concat(WeaponClasses.Select(w => w.Type)).Concat(RelicPassiveClasses.Select(r => r.Type))
            .Concat(LegendaryClasses.Where(l => l.Hero == hero.ToLowerInvariant()).Select(l => l.Type))
            .Distinct();
}
