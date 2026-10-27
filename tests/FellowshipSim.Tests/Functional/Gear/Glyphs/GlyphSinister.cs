using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// The Sinister (glyph 701): "Your Core abilities have 1% / 2% / 3% / 4% increased Critical Strike chance and 5% / 10% / 15% / 20% increased Critical
/// Strike power" (ranks 1 to 4), on every hero's Core ability.
/// Dev: a hit's crit chance is read off its damage event; its damage is the expected value, 1 + chance x (crit multiplier - 1) times the hit, so
/// the ratio of a hit with the glyph to one without is exact. The crit multiplier is 2.0, and crit power multiplies it (2.0 x 1.05 at rank 1), as the
/// combat logs showed on Rime's Bursting Ice. The probes are each hero's Core ability, its first hit; the hits an ability applies as an effect
/// (Rime's Bursting Ice pulses, Tariq's Heavy Strike) are logged under the effect and do not get the glyph in the engine.
/// </summary>
public class GlyphSinisterTests
{
    private static readonly double[] CritChance = [0.01, 0.02, 0.03, 0.04];
    private static readonly double[] CritPower = [0.05, 0.10, 0.15, 0.20];
    private const double Tolerance = 1e-9;
    private const double BaseCrit = 0.2;

    /// <summary>The heroes whose Core ability's hit is logged under the ability itself.</summary>
    public static IEnumerable<object[]> OwnHitHeroesAndRanks =>
        new[] { "Ardeos", "Elarion", "Gunde", "Mara" }.SelectMany(h => Enumerable.Range(1, 4).Select(r => new object[] { h, r }));

    public static IEnumerable<object[]> OwnHitHeroes => new[] { "Ardeos", "Elarion", "Gunde", "Mara" }.Select(h => new object[] { h });

    /// <summary>
    /// Player: your Core ability's hit has the glyph's added crit chance (1% / 2% / 3% / 4%) on top of the crit chance you have.
    /// Dev: 20% crit on the sheet; the hit's chance with the glyph minus the one without.
    /// </summary>
    [Theory]
    [MemberData(nameof(OwnHitHeroesAndRanks))]
    public void Core_Hit_Gets_The_Added_Crit_Chance(string hero, int rank)
    {
        var without = Hit(hero, Fight(hero, crit: BaseCrit), Category.Core)!.Value;
        var with = Hit(hero, Fight(hero, Sinister, rank, crit: BaseCrit), Category.Core)!.Value;
        Assert.Equal(BaseCrit, without.CritChance, Tolerance);
        Assert.Equal(BaseCrit + CritChance[rank - 1], with.CritChance, Tolerance);
    }

    /// <summary>
    /// Player: your Core ability's crit hits are worth more by the glyph's crit power (5% / 10% / 15% / 20% of the crit multiplier), and it crits more often.
    /// Dev: with 20% crit the expected hit is 1 + chance x (2 x (1 + power) - 1) times the hit that cannot crit; the ratio with the glyph over without is
    /// checked to 1e-9.
    /// </summary>
    [Theory]
    [MemberData(nameof(OwnHitHeroesAndRanks))]
    public void Core_Hit_Is_Worth_More_By_Crit_Chance_And_Crit_Power(string hero, int rank)
    {
        var without = Hit(hero, Fight(hero, crit: BaseCrit), Category.Core)!.Value;
        var with = Hit(hero, Fight(hero, Sinister, rank, crit: BaseCrit), Category.Core)!.Value;
        double chance = BaseCrit + CritChance[rank - 1];
        double expected = (1 + chance * (2.0 * (1 + CritPower[rank - 1]) - 1)) / (1 + BaseCrit * (2.0 - 1));
        Assert.Equal(expected, with.Value / without.Value, 1e-9);
    }

    /// <summary>
    /// Player: only Core abilities get it: a Basic or Power ability's hit has the crit chance and the damage it has without the glyph.
    /// Dev: rank 4, every hero, the Basic and the Power probe's first hit, with and without.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNamesForBoth))]
    public void Basic_And_Power_Hits_Are_Not_Touched(string hero, Category category)
    {
        var without = Hit(hero, Fight(hero, crit: BaseCrit), category)!.Value;
        var with = Hit(hero, Fight(hero, Sinister, 4, crit: BaseCrit), category)!.Value;
        Assert.Equal(without.CritChance, with.CritChance, Tolerance);
        Assert.Equal(without.Value, with.Value, 1e-6);
    }

    public static IEnumerable<object[]> HeroNamesForBoth =>
        GearFights.Heroes.SelectMany(h => new[] { Category.Basic, Category.Power }.Select(c => new object[] { h.Name, c }));

    /// <summary>
    /// Player: the hits of an effect your Core ability applies do not get the glyph, only the ability's own hits (the combat logs of Rime's Bursting Ice
    /// pulses: they crit for the same multiple of a normal pulse with the glyph as without it).
    /// Dev: Rime's Bursting Ice, rank 4: its pulse has the same crit chance and damage with and without the glyph.
    /// </summary>
    [Fact]
    public void An_Effects_Hits_Are_Not_Touched_Rime()
    {
        var without = Hit("Rime", Fight("Rime", crit: BaseCrit), Category.Core)!.Value;
        var with = Hit("Rime", Fight("Rime", Sinister, 4, crit: BaseCrit), Category.Core)!.Value;
        Assert.Equal(without.CritChance, with.CritChance, Tolerance);
        Assert.Equal(without.Value, with.Value, 1e-6);
    }

    /// <summary>
    /// Player: Tariq's Heavy Strike is a Core ability, so the glyph's crit chance and crit power apply to its hit.
    /// Dev: the engine logs Heavy Strike's hit under an effect (ID 1,001,776), and the glyph reaches only hits logged under the ability's own ID, so it
    /// gets nothing; whether the game's glyph reaches it is not known.
    /// TODO: check in game whether The Sinister buffs Heavy Strike's hit; if it does, the engine is wrong (the Rime log rule is for Bursting Ice only).
    /// </summary>
    [Fact(Skip = "Unverified: the engine's Sinister skips hits logged under an effect (from Rime's Bursting Ice); whether Heavy Strike's hit gets it is not known")]
    public void Tariqs_Heavy_Strike_Gets_The_Glyph()
    {
        var without = Hit("Tariq", Fight("Tariq", crit: BaseCrit), Category.Core)!.Value;
        var with = Hit("Tariq", Fight("Tariq", Sinister, 4, crit: BaseCrit), Category.Core)!.Value;
        Assert.Equal(BaseCrit + CritChance[3], with.CritChance, Tolerance);
        Assert.True(with.Value > without.Value);
    }
}
