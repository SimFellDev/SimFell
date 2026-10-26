using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// The Mystic (glyph 706): "Your Power abilities gain 0.3% / 0.5% / 0.8% / 1.2% increased power for every 3% Spirit you have, up to 50% Spirit"
/// (ranks 1 to 4).
/// Dev: the first hit of the hero's Power probe, with the glyph over without it at the same Spirit (so Spirit's own effects cancel; Spirit Refund and
/// Wild Swing's avoid chance are forced off). The ratio is 1 + the rank's value x (Spirit in percent / 3), Spirit capped at 50%.
/// </summary>
public class GlyphMysticTests
{
    private static readonly double[] PowerPerStep = [0.003, 0.005, 0.008, 0.012];

    public static IEnumerable<object[]> HeroRanksSpirits =>
        GearFights.Heroes.SelectMany(h => Enumerable.Range(1, 4).SelectMany(r => new[] { 0.09, 0.30, 0.50 }.Select(s => new object[] { h.Name, r, s })));

    /// <summary>
    /// Player: your Power abilities do more damage by the glyph's power for every 3% Spirit.
    /// Dev: Spirit 9%, 30% and 50% (3, 10 and 16.67 steps), every hero and rank.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanksSpirits))]
    public void A_Power_Hit_Gains_Power_For_Every_Three_Percent_Spirit(string hero, int rank, double spirit)
    {
        var without = Hit(hero, Fight(hero, spirit: spirit), Category.Power)!.Value;
        var with = Hit(hero, Fight(hero, Mystic, rank, spirit: spirit), Category.Power)!.Value;
        double expected = 1 + PowerPerStep[rank - 1] * (spirit * 100 / 3);
        Assert.Equal(expected, with.Value / without.Value, 1e-6);
    }

    /// <summary>
    /// Player: the bonus stops at 50% Spirit.
    /// Dev: rank 4 with 80% Spirit gives the same ratio as with 50%.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void The_Bonus_Stops_At_Fifty_Percent_Spirit(string hero)
    {
        var without = Hit(hero, Fight(hero, spirit: 0.8), Category.Power)!.Value;
        var with = Hit(hero, Fight(hero, Mystic, 4, spirit: 0.8), Category.Power)!.Value;
        Assert.Equal(1 + 0.012 * (50.0 / 3), with.Value / without.Value, 1e-6);
    }

    /// <summary>
    /// Player: with no Spirit the glyph does nothing.
    /// Dev: rank 4, Spirit 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void Without_Spirit_It_Does_Nothing(string hero)
    {
        var without = Hit(hero, Fight(hero), Category.Power)!.Value;
        var with = Hit(hero, Fight(hero, Mystic, 4), Category.Power)!.Value;
        Assert.Equal(without.Value, with.Value, 1e-6);
    }

    /// <summary>
    /// Player: only Power abilities gain it: a Basic or Core ability's hit is the same with the glyph.
    /// Dev: rank 4, 30% Spirit, the Basic and the Core probes' first hits.
    /// </summary>
    [Theory]
    [MemberData(nameof(BasicAndCore))]
    public void Other_Categories_Are_Not_Touched(string hero, Category category)
    {
        var without = Hit(hero, Fight(hero, spirit: 0.3), category)!.Value;
        var with = Hit(hero, Fight(hero, Mystic, 4, spirit: 0.3), category)!.Value;
        Assert.Equal(without.Value, with.Value, 1e-6);
    }

    public static IEnumerable<object[]> BasicAndCore =>
        GearFights.Heroes.SelectMany(h => new[] { Category.Basic, Category.Core }.Select(c => new object[] { h.Name, c }));
}
