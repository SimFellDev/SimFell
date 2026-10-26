using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// Glyphs worn together: the same stat from two glyphs adds (crit chance from The Trickster and The Sinister or The Philosopher, haste from The Wayfarer and The
/// Philosopher), and damage multipliers from two glyphs multiply (The Subduer and The Mystic).
/// Dev: each combined effect is read as a difference from the same fight without glyphs, so the abilities' own effects cancel, and compared with the sum
/// of the differences of the glyphs worn alone. Every hero; The Trickster and The Philosopher at rank 4, 40% Spirit.
/// </summary>
public class GlyphComplexStackingTests
{
    private const double Tolerance = 1e-9;

    /// <summary>Casts the Major probe then the Power probe (a hero's resources full), and returns the crit chance and haste after them.</summary>
    private static (double Crit, double Haste) AfterMajorThenPower(string hero, (int Id, int Rank)[] glyphs)
    {
        var h = Hero(hero);
        var f = FightWith(hero, glyphs, spirit: 0.4);
        GearFights.Fill(f);
        f.Cast(h.Get(f, h.Major));
        double major = f.Events.Last(e => e.Kind == StepEventKind.CastCompleted).Time.TotalSeconds;
        GearFights.Fill(f);
        f.Cast(h.Get(f, h.Power));
        double power = f.Events.Last(e => e.Kind == StepEventKind.CastStarted).Time.TotalSeconds;
        Assert.True(f.Now.TotalSeconds < power + 3.9, "the Power cast must end inside The Trickster's 4 seconds");
        Assert.True(f.Now.TotalSeconds < major + 7.9, "the Power cast must end inside The Philosopher's 8 seconds");
        return (f.Hero.CritChance(f.Now), f.Hero.GetHaste(f.Now));
    }

    /// <summary>
    /// Player: the crit chance from The Trickster (after a Power ability) and from The Philosopher (after a Major ability) add.
    /// Dev: the Major probe then the Power probe, with each glyph alone and both; the crit chance over the same two casts without glyphs.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void The_Tricksters_And_The_Philosophers_Crit_Chance_Add(string hero)
    {
        var none = AfterMajorThenPower(hero, []).Crit;
        var trickster = AfterMajorThenPower(hero, [(Trickster, 4)]).Crit - none;
        var philosopher = AfterMajorThenPower(hero, [(Philosopher, 4)]).Crit - none;
        var both = AfterMajorThenPower(hero, [(Trickster, 4), (Philosopher, 4)]).Crit - none;
        Assert.Equal(0.10, trickster, Tolerance);
        Assert.True(philosopher >= 0.08 - Tolerance);
        Assert.Equal(trickster + philosopher, both, Tolerance);
    }

    /// <summary>
    /// Player: the haste from The Wayfarer and from The Philosopher add.
    /// Dev: the Major probe then the Power probe (the first ability starts The Wayfarer), with each glyph alone and both; the haste over the same two casts
    /// without glyphs. The Wayfarer's rank 4 is 25%.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void The_Wayfarers_And_The_Philosophers_Haste_Add(string hero)
    {
        var none = AfterMajorThenPower(hero, []).Haste;
        var wayfarer = AfterMajorThenPower(hero, [(Wayfarer, 4)]).Haste - none;
        var philosopher = AfterMajorThenPower(hero, [(Philosopher, 4)]).Haste - none;
        var both = AfterMajorThenPower(hero, [(Wayfarer, 4), (Philosopher, 4)]).Haste - none;
        Assert.Equal(0.25, wayfarer, Tolerance);
        Assert.True(philosopher >= 0.08 - Tolerance);
        Assert.Equal(wayfarer + philosopher, both, Tolerance);
    }

    /// <summary>
    /// Player: the crit chance of a Core hit from The Trickster (after a Power ability) and from The Sinister add: 10% + 4% on top of your crit chance.
    /// Dev: rank 4 of both, 20% crit; the Power probe, then the Core probe's first hit: the heroes whose Core hit is logged under the ability itself.
    /// </summary>
    [Theory]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    [InlineData("Gunde")]
    [InlineData("Mara")]
    public void The_Tricksters_And_The_Sinisters_Crit_Chance_Add(string hero)
    {
        var h = Hero(hero);
        var f = FightWith(hero, [(Trickster, 4), (Sinister, 4)], crit: 0.2);
        GearFights.Fill(f);
        f.Cast(h.Get(f, h.Power));
        double start = f.Events.Last(e => e.Kind == StepEventKind.CastStarted).Time.TotalSeconds;
        GearFights.Fill(f);
        int before = f.Events.Count;
        f.Cast(h.Get(f, h.Core));
        f.Wait(2);
        var hit = f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage && e.Name == h.Get(f, h.Core).Name);
        Assert.True(hit.Time.TotalSeconds < start + 3.9);
        Assert.Equal(0.2 + 0.10 + 0.04, hit.CritChance, Tolerance);
    }

    /// <summary>
    /// Player: The Subduer and The Mystic multiply: a Power hit on an enemy at 30% health with 30% Spirit does 1.20 x 1.12 of what it does without them.
    /// Dev: rank 4 of both (The Mystic: 1.2% for each of 10 steps of 3% Spirit); the Power probe's first hit, with and without. Ardeos fails for the same
    /// reason as in The Mystic's tests: his Detonate does not get The Mystic's power.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void The_Subduers_And_The_Mystics_Damage_Multiply(string hero)
    {
        double Damage((int, int)[] glyphs)
        {
            var f = FightWith(hero, glyphs, spirit: 0.3);
            f.SetEnemyHealth(30_000_000);
            return Hit(hero, f, Category.Power)!.Value.Value;
        }
        double without = Damage([]);
        double with = Damage([(Subduer, 4), (Mystic, 4)]);
        Assert.Equal(1.2 * (1 + 0.012 * 10), with / without, 1e-6);
    }
}
