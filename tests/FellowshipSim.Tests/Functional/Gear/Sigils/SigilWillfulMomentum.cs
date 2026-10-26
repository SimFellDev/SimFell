using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>
/// Willful Momentum (sigil 39): "Your Spirit Rating is increased by 7 / 11 / 17 / 23. Whenever you trigger a Spirit Refund your Main Stat is increased
/// by 1.5% / 2.6% / 3.7% / 4.8% for 4 seconds." This file has the Spirit rating; the Spirit Refund buff, which needs a hero that spends, is in
/// <c>Complex/SigilComplex</c>.
/// Dev: the hero is built without character sheet percentages (a sheet's Spirit already includes the rating), so its Spirit comes from ratings, 0.16% each.
/// </summary>
public class SigilWillfulMomentum
{
    private const int Sigil = 39;

    public static IEnumerable<object[]> ByRank => SigilFights.HeroesByRank(7 * 0.0016, 11 * 0.0016, 17 * 0.0016, 23 * 0.0016);

    /// <summary>
    /// Player: the sigil raises your Spirit rating by 7 / 11 / 17 / 23 by rank.
    /// Dev: Spirit of the same hero with and without the sigil, no sheet percentages.
    /// </summary>
    [Theory]
    [MemberData(nameof(ByRank))]
    public void It_Raises_Spirit_Rating_By_Rank(GearHero hero, int rank, double spirit)
    {
        var with = SigilFights.FightWithoutSheet(hero, Sigil, rank);
        var without = SigilFights.FightWithoutSheet(hero, 0, 1);
        Assert.Equal(spirit, with.Hero.SpiritPercent(with.Now) - without.Hero.SpiritPercent(without.Now), 1e-6);
    }
}
