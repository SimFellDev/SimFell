namespace FellowshipSim.Core.Import;

/// <summary>
/// The heroes a build can be made for: every hero of the game. A hero the engine has (<see cref="Catalog.Heroes"/>) is its
/// own class; one it does not have yet (<see cref="GameHeroData"/>) is a <see cref="BuildOnlyHero"/>, to edit, save and
/// share but not to simulate.
/// </summary>
public static class BuildHeroes
{
    /// <summary>Every hero's name, in order.</summary>
    public static IReadOnlyList<string> Names { get; } = Catalog.Heroes.Keys.Concat(GameHeroData.MainStats.Keys)
        .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The hero named <paramref name="name"/> in any case, as <see cref="Names"/> spells it, or null.</summary>
    public static string? Find(string name) => Names.FirstOrDefault(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>True for a hero of the game the engine does not have yet.</summary>
    public static bool IsBuildOnly(string hero) => !Catalog.HasHero(hero) && GameHeroData.MainStats.ContainsKey(hero);

    /// <summary>The hero's class, or its <see cref="BuildOnlyHero"/>; throws as <see cref="Catalog.CreateHero"/> for no hero of the game.</summary>
    public static Hero Create(string hero) => IsBuildOnly(hero)
        ? new BuildOnlyHero(GameHeroData.TalentTrees[hero], GameHeroData.MainStats[hero], GameHeroData.Weapons[hero])
        : Catalog.CreateHero(hero);

    /// <summary>
    /// The name of talent <paramref name="id"/> of <paramref name="hero"/>'s tree: the engine's talent's, else the game's
    /// for a hero the engine does not have yet; null otherwise.
    /// </summary>
    public static string? TalentName(string hero, int id) =>
        Catalog.CreateTalent(id)?.Name ?? (IsBuildOnly(hero) ? GameHeroData.TalentNames.GetValueOrDefault(id) : null);

    /// <summary>
    /// The name of legendary item <paramref name="itemId"/> of <paramref name="hero"/>: the engine's legendary's, else what the
    /// game's item grants (a legendary the engine does not model yet); null for no legendary item of the hero.
    /// </summary>
    public static string? LegendaryName(string hero, int itemId) =>
        Catalog.CreateLegendary(hero, itemId)?.Name ?? GameHeroData.Legendaries.GetValueOrDefault(hero)?.GetValueOrDefault(itemId);
}
