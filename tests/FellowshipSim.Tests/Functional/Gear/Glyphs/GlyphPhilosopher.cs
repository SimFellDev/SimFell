using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// The Philosopher (glyph 709): "Your Major ability grants you +0.2% Haste, Expertise and Critical Strike for every 4.0% / 2.5% / 1.6% / 1.0% Spirit
/// you have, up to 50% Spirit, for 8 seconds" (ranks 1 to 4).
/// Dev: the bonus is Haste, Expertise and Crit read with and without the glyph at the same Spirit (Spirit's own effects cancel); it is read right after
/// the Major probe's cast, inside the 8 seconds. Its duration is timed from the cast's completion, within a server frame (0.02s).
/// </summary>
public class GlyphPhilosopherTests
{
    private static readonly double[] SpiritPerStep = [4.0, 2.5, 1.6, 1.0];
    private const double Tolerance = 1e-9;

    public static IEnumerable<object[]> HeroRanksSpirits =>
        GearFights.Heroes.SelectMany(h => Enumerable.Range(1, 4).SelectMany(r => new[] { 0.10, 0.40, 0.50 }.Select(s => new object[] { h.Name, r, s })));

    private static (double Haste, double Expertise, double Crit) Stats(StepDriver f) =>
        (f.Hero.GetHaste(f.Now), f.Hero.Expertise(f.Now), f.Hero.CritChance(f.Now));

    private static (double Haste, double Expertise, double Crit) AfterMajor(string hero, StepDriver f)
    {
        var h = Hero(hero);
        GearFights.Fill(f);
        f.Cast(h.Get(f, h.Major));
        Assert.True(f.Now.TotalSeconds < 7.9, "the probe cast must end inside the 8 seconds");
        return Stats(f);
    }

    /// <summary>
    /// Player: after a Major ability you gain Haste, Expertise and Critical Strike of 0.2% for every 4% / 2.5% / 1.6% / 1% Spirit.
    /// Dev: Spirit 10%, 40% and 50% (every hero and rank): each of the three stats rises by 0.002 x Spirit in percent / the rank's step.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroRanksSpirits))]
    public void A_Major_Ability_Grants_Haste_Expertise_And_Crit(string hero, int rank, double spirit)
    {
        var without = AfterMajor(hero, Fight(hero, spirit: spirit));
        var with = AfterMajor(hero, Fight(hero, Philosopher, rank, spirit: spirit));
        double bonus = 0.002 * (spirit * 100) / SpiritPerStep[rank - 1];
        Assert.Equal(bonus, with.Haste - without.Haste, Tolerance);
        Assert.Equal(bonus, with.Expertise - without.Expertise, Tolerance);
        Assert.Equal(bonus, with.Crit - without.Crit, Tolerance);
    }

    /// <summary>
    /// Player: the Spirit counted stops at 50%.
    /// Dev: rank 4 with 80% Spirit: 0.002 x 50 = 10% of each.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void Spirit_Counts_Up_To_Fifty_Percent(string hero)
    {
        var without = AfterMajor(hero, Fight(hero, spirit: 0.8));
        var with = AfterMajor(hero, Fight(hero, Philosopher, 4, spirit: 0.8));
        Assert.Equal(0.10, with.Haste - without.Haste, Tolerance);
        Assert.Equal(0.10, with.Expertise - without.Expertise, Tolerance);
        Assert.Equal(0.10, with.Crit - without.Crit, Tolerance);
    }

    /// <summary>
    /// Player: it lasts 8 seconds from the Major ability.
    /// Dev: rank 4, 40% Spirit; the Haste 0.1s before 8 seconds after the cast's completion, and 0.1s after.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void It_Lasts_Eight_Seconds(string hero)
    {
        var h = Hero(hero);
        var f = Fight(hero, Philosopher, 4, spirit: 0.4);
        GearFights.Fill(f);
        f.Cast(h.Get(f, h.Major));
        double done = f.Events.Last(e => e.Kind == StepEventKind.CastCompleted).Time.TotalSeconds;
        f.Wait(done + 7.9 - f.Now.TotalSeconds);
        Assert.Equal(0.08, f.Hero.GetHaste(f.Now), Tolerance);
        f.Wait(0.2);
        Assert.Equal(0.0, f.Hero.GetHaste(f.Now), Tolerance);
    }

    /// <summary>
    /// Player: it needs a Major ability: a Basic, Core, Power or Spirit ability grants nothing.
    /// Dev: rank 4, 40% Spirit; Haste, Expertise and Crit after each probe are those without the glyph.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void Other_Abilities_Grant_Nothing(string hero)
    {
        var h = Hero(hero);
        foreach (var type in new[] { h.Basic, h.Core, h.Power, h.Spirit })
        {
            var without = Fight(hero, spirit: 0.4);
            var with = Fight(hero, Philosopher, 4, spirit: 0.4);
            GearFights.Fill(without);
            GearFights.Fill(with);
            without.Cast(h.Get(without, type));
            with.Cast(h.Get(with, type));
            Assert.Equal(Stats(without), Stats(with));
        }
    }

    /// <summary>
    /// Player: the Philosopher's buff is refreshed by the next Major ability, and a refresh keeps part of the time left (up to 2.4 seconds), so it
    /// lasts up to 10.4 seconds.
    /// Dev: not a game rule from the tooltip: the engine measured it in logs (a refresh keeps 30% of the time left, so the second Major ability's
    /// buff ends 8 + 0.3 x left seconds after it). Rank 4, 40% Spirit; two different Major abilities in a row.
    /// TODO: check the 30% carry-over in game (it is a log measurement, not in the game data).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void A_Refresh_Keeps_Thirty_Percent_Of_The_Time_Left(string hero)
    {
        // the same two Major casts with and without the glyph: the Majors' own effects (e.g. a haste buff) cancel in the difference
        var with = Fight(hero, Philosopher, 4, spirit: 0.4);
        var without = Fight(hero, spirit: 0.4);
        var casts = new List<Ability>();
        foreach (var f in new[] { with, without })
        {
            casts.Clear();
            foreach (var major in f.Hero.Abilities.Where(a => a.Category == AbilityCategory.Major).ToList())
            {
                GearFights.Fill(f);
                if (casts.Count < 2 && f.TryCast(major).Succeeded) casts.Add(major);
            }
            Assert.Equal(2, casts.Count);
        }
        var completions = with.Events.Where(e => e.Kind == StepEventKind.CastCompleted).Select(e => e.Time.TotalSeconds).ToList();
        double first = completions[^2], second = completions[^1];
        double expectedEnd = second + 8 + 0.3 * (first + 8 - second);
        // the bonus the second cast gave (a Major ability's own Spirit, as Rime's Winter's Blessing gives, counts in it)
        double bonus = with.Hero.GetHaste(with.Now) - without.Hero.GetHaste(without.Now);
        Assert.True(bonus >= 0.08 - Tolerance);
        foreach (var f in new[] { with, without }) f.Wait(expectedEnd - 0.1 - f.Now.TotalSeconds);
        Assert.Equal(bonus, with.Hero.GetHaste(with.Now) - without.Hero.GetHaste(without.Now), Tolerance);
        foreach (var f in new[] { with, without }) f.Wait(0.2);
        Assert.Equal(0.0, with.Hero.GetHaste(with.Now) - without.Hero.GetHaste(without.Now), Tolerance);
    }
}
