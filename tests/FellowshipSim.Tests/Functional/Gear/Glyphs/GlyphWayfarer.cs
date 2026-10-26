using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// The Wayfarer (glyph 703): "Every 60 seconds you gain The Wayfarer, granting you +6% / +10% / +16% / +25% Haste for 14 seconds. Each time you use your
/// Core ability, the time to The Wayfarer's next application is reduced by 0.2 to 8 seconds, relative to the cooldown of the Core ability used. Abilities
/// with no cooldown provide 0.2 seconds reduction" (ranks 1 to 4). The first application is at the start of the fight; the reduction is the cooldown x 4 / 30
/// (from the combat logs), at least 0.2 and at most 8 seconds.
/// Dev: the buff's gains are read off the events (the aura "The Wayfarer"); times are within 0.05s (the engine ends and renews an aura on its server frame, up to
/// 0.02s late). The haste is the hero's haste with the glyph minus without it. The Core cooldowns are those of the probes: Rime's Bursting Ice 10s, Ardeos's
/// Fire Ball 30s, Elarion's Multishot none, Gunde's Blood Arc 9s, Mara's Widow's Bite 9s, Tariq's Heavy Strike 8s.
/// Not covered: the 8 second ceiling (it needs a Core ability with a 60 second cooldown, and none of the six heroes has one).
/// </summary>
public class GlyphWayfarerTests
{
    private static readonly double[] Haste = [0.06, 0.10, 0.16, 0.25];
    private const double Time = 0.05;

    /// <summary>What one Core cast takes off the next application, in seconds: the cooldown x 4 / 30, from 0.2 to 8.</summary>
    private static readonly Dictionary<string, double> Reduction = new()
    {
        ["Rime"] = 10 * 4.0 / 30,
        ["Ardeos"] = 30 * 4.0 / 30,
        ["Elarion"] = 0.2,
        ["Gunde"] = 9 * 4.0 / 30,
        ["Mara"] = 9 * 4.0 / 30,
        ["Tariq"] = 8 * 4.0 / 30,
    };

    private static List<double> Gains(StepDriver f) =>
        f.Events.Where(e => e.Kind == StepEventKind.AuraGained && e.Name == "The Wayfarer").Select(e => e.Time.TotalSeconds).ToList();

    private static double Bonus(StepDriver with, StepDriver without) => with.Hero.GetHaste(with.Now) - without.Hero.GetHaste(without.Now);

    private static void Cast(string hero, StepDriver f, Type type)
    {
        GearFights.Fill(f);
        f.Cast(Hero(hero).Get(f, type));
    }

    /// <summary>
    /// Player: your first ability starts The Wayfarer: you have its haste (6% / 10% / 16% / 25%) from then on.
    /// Dev: the Basic probe's cast; the buff is gained as that cast starts, and the haste is read right after it (inside the 14 seconds).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNamesAndRanks), MemberType = typeof(GlyphFights))]
    public void The_First_Ability_Grants_The_Haste(string hero, int rank)
    {
        var with = Fight(hero, Wayfarer, rank);
        var without = Fight(hero);
        Assert.Equal(0.0, Bonus(with, without), 1e-9);
        Cast(hero, with, Hero(hero).Basic);
        Cast(hero, without, Hero(hero).Basic);
        Assert.Equal(Haste[rank - 1], Bonus(with, without), 1e-9);
        Assert.Equal([0.0], Gains(with));
    }

    /// <summary>
    /// Player: the haste lasts 14 seconds.
    /// Dev: rank 4; the haste 0.1s before 14 seconds from the first cast, and 0.1s after.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void It_Lasts_Fourteen_Seconds(string hero)
    {
        var with = Fight(hero, Wayfarer, 4);
        var without = Fight(hero);
        Cast(hero, with, Hero(hero).Basic);
        Cast(hero, without, Hero(hero).Basic);
        foreach (var f in new[] { with, without }) f.Wait(13.9 - f.Now.TotalSeconds);
        Assert.Equal(0.25, Bonus(with, without), 1e-9);
        foreach (var f in new[] { with, without }) f.Wait(0.2);
        Assert.Equal(0.0, Bonus(with, without), 1e-9);
    }

    /// <summary>
    /// Player: it comes again every 60 seconds.
    /// Dev: rank 4, only a Basic cast at the start; The Wayfarer is gained at 0s and again at 60s, and the haste is back just after.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void It_Comes_Back_Every_Sixty_Seconds(string hero)
    {
        var with = Fight(hero, Wayfarer, 4);
        var without = Fight(hero);
        Cast(hero, with, Hero(hero).Basic);
        Cast(hero, without, Hero(hero).Basic);
        foreach (var f in new[] { with, without }) f.Wait(59.9 - f.Now.TotalSeconds);
        Assert.Equal(0.0, Bonus(with, without), 1e-9);
        foreach (var f in new[] { with, without }) f.Wait(0.2);
        Assert.Equal(0.25, Bonus(with, without), 1e-9);
        var gains = Gains(with);
        Assert.Equal(2, gains.Count);
        Assert.Equal(60.0, gains[1], Time);
    }

    /// <summary>
    /// Player: using your Core ability brings the next application closer by its cooldown x 4 / 30 (0.2 seconds for one with no cooldown).
    /// Dev: rank 1, the Core probe as the first ability; the second gain is at 60 seconds less that.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void A_Core_Ability_Brings_The_Next_Application_Closer(string hero)
    {
        var f = Fight(hero, Wayfarer, 1);
        Cast(hero, f, Hero(hero).Core);
        f.Wait(65 - f.Now.TotalSeconds);
        var gains = Gains(f);
        Assert.True(gains.Count >= 2, "the buff should come a second time");
        Assert.Equal(60.0 - Reduction[hero], gains[1], Time);
    }

    /// <summary>
    /// Player: only Core abilities do it: a Basic, Power or Major ability leaves the next application at 60 seconds.
    /// Dev: rank 1; the probe of that category as the first ability.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroesAndOtherCategories))]
    public void Other_Abilities_Do_Not(string hero, Category category)
    {
        var h = Hero(hero);
        var f = Fight(hero, Wayfarer, 1);
        Cast(hero, f, category switch { Category.Basic => h.Basic, Category.Power => h.Power, _ => h.Major });
        f.Wait(65 - f.Now.TotalSeconds);
        var gains = Gains(f);
        Assert.True(gains.Count >= 2);
        Assert.Equal(60.0, gains[1], Time);
    }

    public static IEnumerable<object[]> HeroesAndOtherCategories =>
        GearFights.Heroes.SelectMany(h => new[] { Category.Basic, Category.Power, Category.Major }.Select(c => new object[] { h.Name, c }));

    /// <summary>
    /// Player: each Core cast counts: two of them bring the next application twice as close.
    /// Dev: rank 1; the Core probe cast, then cast again once its cooldown is over; the second gain is at 60 seconds less twice the reduction.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void Each_Core_Cast_Counts(string hero)
    {
        var h = Hero(hero);
        var f = Fight(hero, Wayfarer, 1);
        var core = h.Get(f, h.Core);
        Cast(hero, f, h.Core);
        if (core.Cooldown.TotalSeconds > 0) f.Wait(core.Cooldown.TotalSeconds + 1);
        Cast(hero, f, h.Core);
        f.Wait(65 - f.Now.TotalSeconds);
        var gains = Gains(f);
        Assert.True(gains.Count >= 2);
        Assert.Equal(60.0 - 2 * Reduction[hero], gains[1], Time);
    }
}
