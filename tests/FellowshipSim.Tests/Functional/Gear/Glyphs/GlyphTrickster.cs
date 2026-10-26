using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// The Trickster (glyph 700): "Your Power abilities grant you 2.5% / 4.0% / 6.4% / 10% increased Critical Strike chance for 4 seconds" (ranks 1 to 4).
/// Dev: the hero's crit chance is read with <c>Hero.CritChance</c> (the sheet's 0 plus the buff); the buff comes with the Power ability's cast and lasts 4
/// seconds from it, within a server frame (0.02s). A Power ability needs its resources, so every pool is filled first.
/// </summary>
public class GlyphTricksterTests
{
    private static readonly double[] CritChance = [0.025, 0.04, 0.064, 0.10];
    private const double Tolerance = 1e-9;

    private static double Crit(StepDriver f) => f.Hero.CritChance(f.Now);

    /// <summary>Casts the hero's Power probe and returns the time its cast started.</summary>
    private static double CastPower(string hero, StepDriver f)
    {
        var h = Hero(hero);
        GearFights.Fill(f);
        f.Cast(h.Get(f, h.Power));
        return f.Events.Last(e => e.Kind == StepEventKind.CastStarted).Time.TotalSeconds;
    }

    /// <summary>Player: after a Power ability you have the glyph's crit chance (2.5% / 4% / 6.4% / 10%).</summary>
    [Theory]
    [MemberData(nameof(HeroNamesAndRanks), MemberType = typeof(GlyphFights))]
    public void A_Power_Ability_Grants_Crit_Chance(string hero, int rank)
    {
        var f = Fight(hero, Trickster, rank);
        Assert.Equal(0.0, Crit(f), Tolerance);
        CastPower(hero, f);
        Assert.True(f.Now.TotalSeconds < 3.9, "the probe cast must end inside the 4 seconds");
        Assert.Equal(CritChance[rank - 1], Crit(f), Tolerance);
    }

    /// <summary>
    /// Player: it lasts 4 seconds from the cast.
    /// Dev: rank 4; the crit chance 0.1s before the 4 seconds are up and 0.1s after.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void It_Lasts_Four_Seconds(string hero)
    {
        var f = Fight(hero, Trickster, 4);
        double start = CastPower(hero, f);
        f.Wait(start + 3.9 - f.Now.TotalSeconds);
        Assert.Equal(0.10, Crit(f), Tolerance);
        f.Wait(0.2);
        Assert.Equal(0.0, Crit(f), Tolerance);
    }

    /// <summary>
    /// Player: only Power abilities grant it: a Basic, Core, Major or Spirit ability does not.
    /// Dev: rank 4; the crit chance after each of those probes is 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void Other_Categories_Do_Not_Grant_It(string hero)
    {
        var h = Hero(hero);
        foreach (var type in new[] { h.Basic, h.Core, h.Major, h.Spirit })
        {
            var f = Fight(hero, Trickster, 4);
            GearFights.Fill(f);
            f.Cast(h.Get(f, type));
            Assert.Equal(0.0, Crit(f), Tolerance);
        }
    }

    /// <summary>
    /// Player: a second Power ability refreshes it, it does not stack.
    /// Dev: rank 4; the hero's first Power ability, then its second (its first again for a hero with one, e.g. Ardeos's Detonate); the chance stays 10%
    /// and it is read less than 4 seconds after the second cast started.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void A_Second_Power_Ability_Refreshes_It(string hero)
    {
        var f = Fight(hero, Trickster, 4);
        var powers = f.Hero.Abilities.Where(a => a.Category == AbilityCategory.Power).ToList();
        var cast = new List<Ability>();
        foreach (var power in powers)
        {
            GearFights.Fill(f);
            if (cast.Count < 2 && f.TryCast(power).Succeeded) cast.Add(power);
        }
        if (cast.Count == 1)
        {
            GearFights.Fill(f);
            Assert.True(f.TryCast(cast[0]).Succeeded);
            cast.Add(cast[0]);
        }
        Assert.Equal(2, cast.Count);
        double start = f.Events.Last(e => e.Kind == StepEventKind.CastStarted).Time.TotalSeconds;
        Assert.True(f.Now.TotalSeconds < start + 3.9);
        Assert.Equal(0.10, Crit(f), Tolerance);
    }

    /// <summary>
    /// Player: your next hits have the bonus: a Basic ability cast after a Power ability hits with the added crit chance.
    /// Dev: rank 4, 20% crit on the sheet; Power probe, then the Basic probe: its first hit's chance is 30%.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void The_Next_Hits_Have_The_Crit_Chance(string hero)
    {
        var h = Hero(hero);
        var f = Fight(hero, Trickster, 4, crit: 0.2);
        double start = CastPower(hero, f);
        int before = f.Events.Count;
        f.Cast(h.Get(f, h.Basic));
        var hit = f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage && e.Name == h.Get(f, h.Basic).Name);
        Assert.True(hit.Time.TotalSeconds < start + 3.9, "the hit must land inside the 4 seconds");
        if (hero == "Tariq") Assert.Equal(0.0, hit.CritChance, Tolerance); // Wild Swing cannot crit
        else Assert.Equal(0.3, hit.CritChance, Tolerance);
    }
}
