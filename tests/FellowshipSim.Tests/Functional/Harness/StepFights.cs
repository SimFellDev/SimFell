using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Build a fight for a functional test: a hero with only what the test asks for, against training dummies with 100 million health.
///
/// By default the hero has no gear, no talents, the base Main Stat, and 0% haste, crit, Spirit and Expertise (<see cref="StatPins"/>), so
/// no hit crits and no Spirit Refund happens unless the test asks. Talents, a weapon, legendaries, gem power, glyphs, aspects, sigils and
/// relics are added the way a player gets them, through the build.
///
/// Every hero takes the same options. <c>StepFights.Rime(...)</c> is <c>StepFights.Of("Rime", ...)</c>; a theory over several heroes uses
/// <see cref="Of"/> with the hero's name. <c>f.RimeOf()</c> and the others return the hero as its own class, for its own state (Winter
/// Orbs...).
/// </summary>
public static class StepFights
{
    /// <summary>A bare <paramref name="hero"/> ("Rime", "Ardeos", "Elarion", "Gunde", "Mara" or "Tariq") in a fight of its own.</summary>
    /// <param name="haste">Haste as a fraction (0.5 is 50%).</param>
    /// <param name="enemies">How many enemies the fight has; the first is the target.</param>
    /// <param name="talents">Talent IDs, e.g. <c>Burstbolter.TalentId</c>.</param>
    /// <param name="seed">The dice's seed. Tests force the outcomes they check rather than pick a seed.</param>
    /// <param name="crit">Crit chance as a fraction (1.0 is 100%); 0 by default, so hits are not crit.</param>
    /// <param name="spirit">Spirit as a fraction (0.2 is 20%); 0 by default, so there is no Spirit Refund.</param>
    /// <param name="mainStat">The main stat. Every hero's main stat works the same; what the game calls it is cosmetic.</param>
    /// <param name="expertise">Expertise as a fraction; 0 by default.</param>
    /// <param name="weaponAbilityId">The weapon ability's ID, e.g. <c>Chronoshift.AbilityId</c>; 0 for no weapon.</param>
    /// <param name="legendaries">Item IDs of the legendary items worn, e.g. <c>Shimmer.HeadItemId</c>.</param>
    /// <param name="gemPower">Gem power per colour, which unlocks the gems' nodes, e.g. <c>new() { [GemColor.Emerald] = 1500 }</c>.</param>
    /// <param name="glyphs">Glyphs and their rank, e.g. <c>[new(TheMonarch.GlyphId, Rank: 4)]</c>.</param>
    /// <param name="augmentations">Aspects, sigils and protective enchants and their rank, e.g.
    /// <c>[new(RubyStorm.AspectId, Rank: 1)]</c>.</param>
    /// <param name="relics">Item IDs of the relics worn, in the first slot then the second, e.g. <c>[UndyingHeart.RelicItemId]</c>.</param>
    public static StepDriver Of(string hero, double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0,
        double spirit = 0, double mainStat = 0, double expertise = 0, int weaponAbilityId = 0, int[]? legendaries = null,
        Dictionary<GemColor, double>? gemPower = null, BuildEntry[]? glyphs = null, BuildEntry[]? augmentations = null,
        int[]? relics = null)
    {
        var build = new CharacterBuild { Name = "Test " + hero, Hero = hero, PrimaryStat = mainStat, WeaponAbilityId = weaponAbilityId };
        if (talents != null) build.Talents.AddRange(talents);
        if (legendaries != null) build.LegendaryItemIds.AddRange(legendaries);
        if (gemPower != null) build.GemPower = gemPower;
        if (glyphs != null) build.Glyphs.AddRange(glyphs);
        if (augmentations != null) build.Augmentations.AddRange(augmentations);
        if (relics is { Length: > 0 }) build.Relic1ItemId = relics[0];
        if (relics is { Length: > 1 }) build.Relic2ItemId = relics[1];
        return new StepDriver(StatPins.Import(build, haste, crit, spirit, expertise), seed, enemies);
    }

    /// <summary>A glyph, aspect or sigil at <paramref name="rank"/>, or nothing at rank 0: for a theory whose rank-0 row is the hero without
    /// it (<c>glyphs: StepFights.Ranked(TheMonarch.GlyphId, rank)</c>).</summary>
    public static BuildEntry[] Ranked(int id, int rank) => rank == 0 ? [] : [new(id, rank)];

    /// <inheritdoc cref="Of"/>
    public static StepDriver Rime(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 0, double expertise = 0, int weaponAbilityId = 0, int[]? legendaries = null,
        Dictionary<GemColor, double>? gemPower = null, BuildEntry[]? glyphs = null, BuildEntry[]? augmentations = null,
        int[]? relics = null) =>
        Of("Rime", haste, enemies, talents, seed, crit, spirit, mainStat, expertise, weaponAbilityId, legendaries, gemPower, glyphs,
            augmentations, relics);

    /// <inheritdoc cref="Of"/>
    public static StepDriver Ardeos(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 0, double expertise = 0, int weaponAbilityId = 0, int[]? legendaries = null,
        Dictionary<GemColor, double>? gemPower = null, BuildEntry[]? glyphs = null, BuildEntry[]? augmentations = null,
        int[]? relics = null) =>
        Of("Ardeos", haste, enemies, talents, seed, crit, spirit, mainStat, expertise, weaponAbilityId, legendaries, gemPower, glyphs,
            augmentations, relics);

    /// <inheritdoc cref="Of"/>
    public static StepDriver Elarion(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 0, double expertise = 0, int weaponAbilityId = 0, int[]? legendaries = null,
        Dictionary<GemColor, double>? gemPower = null, BuildEntry[]? glyphs = null, BuildEntry[]? augmentations = null,
        int[]? relics = null) =>
        Of("Elarion", haste, enemies, talents, seed, crit, spirit, mainStat, expertise, weaponAbilityId, legendaries, gemPower, glyphs,
            augmentations, relics);

    /// <inheritdoc cref="Of"/>
    public static StepDriver Gunde(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 0, double expertise = 0, int weaponAbilityId = 0, int[]? legendaries = null,
        Dictionary<GemColor, double>? gemPower = null, BuildEntry[]? glyphs = null, BuildEntry[]? augmentations = null,
        int[]? relics = null) =>
        Of("Gunde", haste, enemies, talents, seed, crit, spirit, mainStat, expertise, weaponAbilityId, legendaries, gemPower, glyphs,
            augmentations, relics);

    /// <inheritdoc cref="Of"/>
    public static StepDriver Mara(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 0, double expertise = 0, int weaponAbilityId = 0, int[]? legendaries = null,
        Dictionary<GemColor, double>? gemPower = null, BuildEntry[]? glyphs = null, BuildEntry[]? augmentations = null,
        int[]? relics = null) =>
        Of("Mara", haste, enemies, talents, seed, crit, spirit, mainStat, expertise, weaponAbilityId, legendaries, gemPower, glyphs,
            augmentations, relics);

    /// <inheritdoc cref="Of"/>
    public static StepDriver Tariq(double haste = 0, int enemies = 1, int[]? talents = null, int seed = 1, double crit = 0, double spirit = 0,
        double mainStat = 0, double expertise = 0, int weaponAbilityId = 0, int[]? legendaries = null,
        Dictionary<GemColor, double>? gemPower = null, BuildEntry[]? glyphs = null, BuildEntry[]? augmentations = null,
        int[]? relics = null) =>
        Of("Tariq", haste, enemies, talents, seed, crit, spirit, mainStat, expertise, weaponAbilityId, legendaries, gemPower, glyphs,
            augmentations, relics);

    // The hero as its own class, for its own state (Winter Orbs, combo points...).
    public static Core.Heroes.Rime.Rime RimeOf(this StepDriver driver) => (Core.Heroes.Rime.Rime)driver.Hero;
    public static Core.Heroes.Ardeos.Ardeos ArdeosOf(this StepDriver driver) => (Core.Heroes.Ardeos.Ardeos)driver.Hero;
    public static Core.Heroes.Elarion.Elarion ElarionOf(this StepDriver driver) => (Core.Heroes.Elarion.Elarion)driver.Hero;
    public static Core.Heroes.Gunde.Gunde GundeOf(this StepDriver driver) => (Core.Heroes.Gunde.Gunde)driver.Hero;
    public static Core.Heroes.Mara.Mara MaraOf(this StepDriver driver) => (Core.Heroes.Mara.Mara)driver.Hero;
    public static Core.Heroes.Tariq.Tariq TariqOf(this StepDriver driver) => (Core.Heroes.Tariq.Tariq)driver.Hero;
}
