using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// The Subduer (glyph 704): "You deal 5% / 10% / 15% / 20% more damage to Low Health Enemies" (ranks 1 to 4); low health is 30% or less.
/// Dev: the enemy has 100,000,000 health and is given a whole number of it, so 30,000,000 is exactly 30%. A hit's damage with the glyph over the
/// same hit without it is 1 + the rank's value on an enemy at 30% or less, and 1 on one above. The damage over time cases read the first tick of the effect.
/// The tooltip's second part (Major abilities reduce the cooldown of a Control ability) changes no damage and is not modeled: see the skipped test.
/// </summary>
public class GlyphSubduerTests
{
    private static readonly double[] Damage = [0.05, 0.10, 0.15, 0.20];
    private const double Whole = 100_000_000;

    // health, whether it counts as low health
    private static readonly (double Health, bool Low)[] Healths =
    [
        (30_000_000, true),    // exactly 30%
        (29_999_999, true),
        (1_000_000, true),
        (30_000_001, false),   // just above
        (31_000_000, false),
        (Whole, false),
    ];

    public static IEnumerable<object[]> HeroesCategoriesHealths =>
        GearFights.Heroes.SelectMany(h => new[] { Category.Basic, Category.Core, Category.Power }
            .SelectMany(c => Healths.Select(x => new object[] { h.Name, c, x.Health, x.Low })));

    public static IEnumerable<object[]> HeroesRanks30 =>
        GearFights.Heroes.SelectMany(h => Enumerable.Range(1, 4).Select(r => new object[] { h.Name, r }));

    private static double HitAt(string hero, int glyph, int rank, Category category, double health)
    {
        var f = Fight(hero, glyph, rank);
        f.SetEnemyHealth(health);
        return Hit(hero, f, category)!.Value.Value;
    }

    /// <summary>
    /// Player: you deal 5% / 10% / 15% / 20% more damage to an enemy at 30% health.
    /// Dev: every hero and rank, the Basic probe's first hit on an enemy at exactly 30%.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroesRanks30))]
    public void An_Enemy_At_Thirty_Percent_Takes_More_Damage(string hero, int rank)
    {
        double without = HitAt(hero, 0, 1, Category.Basic, 30_000_000);
        double with = HitAt(hero, Subduer, rank, Category.Basic, 30_000_000);
        Assert.Equal(1 + Damage[rank - 1], with / without, 1e-9);
    }

    /// <summary>
    /// Player: the bonus is on at 30% and below and off above: 30.000001% is already too healthy.
    /// Dev: rank 4; 30%, 29.999999% and 1% (on); 30.000001%, 31% and full health (off); the Basic, Core and Power probes' first hits of every hero.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroesCategoriesHealths))]
    public void It_Is_On_At_Thirty_Percent_And_Below_And_Off_Above(string hero, Category category, double health, bool low)
    {
        double without = HitAt(hero, 0, 1, category, health);
        double with = HitAt(hero, Subduer, 4, category, health);
        Assert.Equal(low ? 1.2 : 1.0, with / without, 1e-9);
    }

    /// <summary>
    /// Player: a damage over time effect deals more damage to a low health enemy too: its ticks on an enemy at 30% are 5% / 10% / 15% / 20% higher.
    /// Dev: the first tick of Ardeos's Searing Blaze, Mara's Hemorrhage (from Hemorrhaging Strike) and Gunde's Rend (from Double Strike), the enemy at 30%
    /// when the effect is applied and for its first ticks (the hits are a few thousand of its 30,000,000).
    /// </summary>
    [Theory]
    [MemberData(nameof(DotCases))]
    public void Damage_Over_Time_Ticks_Deal_More_To_A_Low_Health_Enemy(string hero, int rank)
    {
        double without = FirstTick(hero, 0, 1, 30_000_000);
        double with = FirstTick(hero, Subduer, rank, 30_000_000);
        Assert.Equal(1 + Damage[rank - 1], with / without, 1e-6);
    }

    public static IEnumerable<object[]> DotCases =>
        new[] { "Ardeos", "Mara", "Gunde" }.SelectMany(h => Enumerable.Range(1, 4).Select(r => new object[] { h, r }));

    /// <summary>
    /// Player: a damage over time effect on an enemy above 30% is not increased.
    /// Dev: rank 4, the enemy at 30.1%.
    /// </summary>
    [Theory]
    [InlineData("Ardeos")]
    [InlineData("Mara")]
    [InlineData("Gunde")]
    public void Damage_Over_Time_Ticks_Are_Not_Increased_Above_Thirty_Percent(string hero)
    {
        double without = FirstTick(hero, 0, 1, 30_100_000);
        double with = FirstTick(hero, Subduer, 4, 30_100_000);
        Assert.Equal(1.0, with / without, 1e-6);
    }

    private static double FirstTick(string hero, int glyph, int rank, double health)
    {
        var f = Fight(hero, glyph, rank);
        f.SetEnemyHealth(health);
        GearFights.Fill(f);
        switch (hero)
        {
            case "Ardeos":
                f.Cast<SearingBlaze>();
                f.Wait(8);
                return f.Ticks(SearingBlazeDot.EffectId).First().Value;
            case "Mara":
                f.Cast<HemorrhagingStrike>();
                f.Wait(8);
                return f.EffectTicks(Hemorrhage.EffectId).First().Value;
            default:
                f.Cast<DoubleStrike>();
                f.Wait(8);
                return f.Damage("Rend").First().Value;
        }
    }

    /// <summary>
    /// Player: your Major abilities reduce the cooldown of one of your Control abilities.
    /// TODO: not modeled (the tooltip is cut off in the engine's notes and the only Control abilities, Gunde's Butcher's Hook and Rime's Frigid Winds, have
    /// no recorded rule); read the full tooltip in game and write the test.
    /// </summary>
    [Fact(Skip = "Not modeled: the Control cooldown part of the glyph (the tooltip is cut off in the engine notes)")]
    public void A_Major_Ability_Reduces_A_Control_Cooldown() { }
}
