using System.Collections.Concurrent;
using FellowshipSim.Core;
using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.Augmentations;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.ProtectiveEnchants;
using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Customization.StatEnchants;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Import;

namespace FellowshipSim.UI.Build;

/// <summary>
/// Everything a build of one hero can have, from the engine's catalog, with the game data's icons and highest ranks. A
/// choice the engine does not model is marked.
/// </summary>
public sealed class BuildCatalog
{
    /// <summary>The highest rank of an augmentation or glyph the game data gives none for.</summary>
    public const int DefaultMaxRank = 4;

    /// <summary>
    /// Every hero's legendary items in the order the editor shows them: head, chest and legs (the game data's item records
    /// 5899 Head, 5898 Chest, 5900 Legs, the same IDs for every hero).
    /// </summary>
    public static readonly int[] LegendarySlotOrder = [5899, 5898, 5900];

    private static readonly ConcurrentDictionary<string, BuildCatalog> Heroes = new(StringComparer.OrdinalIgnoreCase);

    private BuildCatalog(string hero)
    {
        Hero = hero;
        // The engine's hero, or the game's for one it does not have yet: its talents are all not modeled.
        var heroClass = BuildHeroes.Create(hero);
        Talents = heroClass.TalentTreeSlots
            .Select(slot => new TalentOption(slot, Catalog.CreateTalent(slot.TalentId) is { } talent
                // A talent the engine knows by its combat-log ID (Macabre Stratagem) has its icon by name.
                ? new BuildOption(talent.Id, talent.Name, GameIcons.Talents.GetValueOrDefault(talent.Id) ?? GameIcons.Names.GetValueOrDefault(talent.Name))
                : new BuildOption(slot.TalentId, GameHeroData.TalentNames.GetValueOrDefault(slot.TalentId, $"Talent {slot.TalentId}"),
                    GameIcons.Talents.GetValueOrDefault(slot.TalentId), Modeled: false)))
            .ToList();
        // The hero's own weapons, by the hero's item names; a name the game data gives two of the
        // hero's weapons (Rime's An'zhyr staff is a second "Void-touched Conduit") is the engine's weapon's, else its ability's.
        var weapons = GameHeroData.Weapons.GetValueOrDefault(hero) ?? new Dictionary<int, string>();
        Weapons = weapons
            .Select(w => (Id: w.Key, Name: weapons.Values.Count(n => n == w.Value) == 1
                ? w.Value
                : Catalog.CreateWeapon(w.Key)?.Name ?? GameHeroData.WeaponAbilityNames.GetValueOrDefault(w.Key, w.Value)))
            .Select(w => new BuildOption(w.Id, w.Name, GameIcons.WeaponAbilities.GetValueOrDefault(w.Id), Modeled: Catalog.CreateWeapon(w.Id) != null))
            .OrderBy(o => o.Name).ToList();
        // The hero's legendaries as the game has them, one per legendary item, each the engine's when it models it; an
        // engine legendary none of whose items the game data gives the hero comes by its first item.
        var gameLegendaries = GameHeroData.Legendaries.GetValueOrDefault(hero) ?? new Dictionary<int, string>();
        var engineLegendaries = Catalog.Legendaries(hero).Where(l => l.ItemIds.Count > 0).ToList();
        var legendaries = gameLegendaries.Select(l => (Item: l.Key, Name: l.Value, Engine: Catalog.CreateLegendary(hero, l.Key)))
            .Concat(engineLegendaries.Where(e => !e.ItemIds.Any(gameLegendaries.ContainsKey)).Select(e => (Item: e.ItemIds[0], e.Name, Engine: (Legendary?)e)))
            .ToList();
        // The hero's legendary item's icon, else the first of its other items' with one, else the power's.
        Legendaries = legendaries
            .Select(l => new BuildOption(l.Item, l.Engine?.Name ?? l.Name,
                GameIcons.LegendaryItems.GetValueOrDefault($"{hero}:{l.Item}")
                ?? l.Engine?.ItemIds.Select(id => GameIcons.LegendaryItems.GetValueOrDefault($"{hero}:{id}")).FirstOrDefault(icon => icon != null)
                ?? (l.Engine is { } engine ? GameIcons.LegendaryPowers.GetValueOrDefault(engine.LegendaryPowerId) : null),
                l.Engine?.IsModeled == true))
            .OrderBy(o => Array.IndexOf(LegendarySlotOrder, o.Id) is >= 0 and var slot ? slot : LegendarySlotOrder.Length)
            .ThenBy(o => o.Name).ToList();
        _legendaryOfItem = legendaries.SelectMany(l => (l.Engine?.ItemIds ?? []).Append(l.Item).Select(id => (Item: id, Option: l.Item)))
            .DistinctBy(x => x.Item).ToDictionary(x => x.Item, x => x.Option);
        Relics = Catalog.AllRelicPassives
            .SelectMany(r => r.Items.Select(i => new RelicOption(i.Id, i.Name, r.Name)))
            .OrderBy(o => o.Name).ToList();
        var augmentations = Catalog.AllAugmentations.ToList();
        List<BuildOption> OfKind<T>() => augmentations.OfType<T>().Cast<Augmentation>()
            .Select(t => new BuildOption(t.Id, t.Name, GameIcons.Augmentations.GetValueOrDefault(t.Id), MaxRank: GameIcons.AugmentationMaxRanks.GetValueOrDefault(t.Id, DefaultMaxRank)))
            .OrderBy(o => o.Name).ToList();
        Aspects = OfKind<Aspect>();
        Sigils = OfKind<Sigil>();
        ProtectiveEnchants = OfKind<ProtectiveEnchant>();
        var mainStat = heroClass.MainStat;
        MainStatShort = mainStat switch { MainStat.Strength => "Str", MainStat.Agility => "Agi", _ => "Int" };
        StatEnchants = StatEnchant.All
            .Select(e => new StatEnchantOption(e, e.NameFor(mainStat)))
            .ToList();
        Glyphs = Catalog.AllGlyphs
            .Select(b => new BuildOption(b.Id, b.Name, GameIcons.Glyphs.GetValueOrDefault(b.Id), MaxRank: GameIcons.GlyphMaxRanks.GetValueOrDefault(b.Id, DefaultMaxRank)))
            .OrderBy(o => o.Name).ToList();
    }

    /// <summary>The catalog of <paramref name="hero"/> (a name in <see cref="BuildHeroes.Names"/>).</summary>
    public static BuildCatalog For(string hero) => Heroes.GetOrAdd(hero, h => new BuildCatalog(h));

    public string Hero { get; }

    /// <summary>The hero's main stat, short as the gear's stat columns name it ("Int").</summary>
    public string MainStatShort { get; }

    /// <summary>The talent tree, row by row and column by column.</summary>
    public IReadOnlyList<TalentOption> Talents { get; }

    public IReadOnlyList<BuildOption> Weapons { get; }

    public IReadOnlyList<BuildOption> Legendaries { get; }

    // Each legendary's option (its item ID) by every item ID it comes on: a saved Vexira's Venom can be the old wristbands, 5225.
    private readonly Dictionary<int, int> _legendaryOfItem;

    /// <summary>The ID of the <see cref="Legendaries"/> option that legendary item <paramref name="itemId"/> is, or null.</summary>
    public int? LegendaryOption(int? itemId) => itemId is { } id && _legendaryOfItem.TryGetValue(id, out int option) ? option : null;

    public IReadOnlyList<RelicOption> Relics { get; }

    public IReadOnlyList<BuildOption> Aspects { get; }

    public IReadOnlyList<BuildOption> Sigils { get; }

    /// <summary>Protective enchants: one rank each, up to <see cref="ProtectiveEnchant.MaxPerBuild"/> in a build.</summary>
    public IReadOnlyList<BuildOption> ProtectiveEnchants { get; }

    /// <summary>The stat enchants, named for the hero's main stat ("Chimera's Cunning" for an Intellect hero).</summary>
    public IReadOnlyList<StatEnchantOption> StatEnchants { get; }

    public IReadOnlyList<BuildOption> Glyphs { get; }

    public static IReadOnlyList<GemColor> GemColors { get; } = Enum.GetValues<GemColor>();

    /// <summary>The icon's image of gem colour <paramref name="color"/>, or null.</summary>
    public static string? GemIconUrl(GemColor color) => GameIcons.Gems.TryGetValue(color.ToString(), out var icon) ? GameIcons.Url(icon) : null;

    /// <summary>
    /// The icon's image of a damage source or aura with log ID <paramref name="logId"/> (an ability's ID, or an effect's,
    /// talent's or augmentation's plus its offset): its own record's icon, else the first record of its name with one.
    /// </summary>
    public static string? IconUrl(int logId, string name)
    {
        string? icon = logId switch
        {
            >= LogIds.AugmentationOffset => GameIcons.Augmentations.GetValueOrDefault(logId - LogIds.AugmentationOffset),
            >= LogIds.TalentOffset => GameIcons.Talents.GetValueOrDefault(logId - LogIds.TalentOffset),
            >= LogIds.EffectOffset => GameIcons.Effects.GetValueOrDefault(logId - LogIds.EffectOffset),
            > 0 => GameIcons.Abilities.GetValueOrDefault(logId),
            _ => null,
        };
        icon ??= GameIcons.Names.GetValueOrDefault(name);
        return icon == null ? null : GameIcons.Url(icon);
    }

    /// <summary>The icon's image of gem <paramref name="gem"/> (its colour and size), or null.</summary>
    public static string? GemIconUrl(GemItem gem) => GameIcons.GemItems.TryGetValue(gem.Id, out var icon) ? GameIcons.Url(icon) : null;

    /// <summary>Every hero of the game (the game data's), by name; <see cref="Catalog.Heroes"/> are the ones the engine has.</summary>
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
