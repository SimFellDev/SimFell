using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many hits Elarion's periodic abilities deal, and how many casts fit in a window, at each haste. Haste makes them tick faster
/// without making them last longer, so the numbers go up in steps (breakpoints) rather than smoothly. Every test casts on a single
/// enemy and counts against the formula in the test's description, written out in the test.
/// </summary>
public class ElarionHasteBreakpoints
{
    // ---- Starfall Volley

    /// <summary>
    /// Starfall Volley lasts 8 seconds and strikes every second, faster with haste: once when you cast it, then once per full hasted
    /// second that fits in the 8 seconds, 1 + floor(8 x (1 + haste)).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.25)]
    [InlineData(0.3)]
    [InlineData(0.4)]
    [InlineData(0.499)]
    [InlineData(0.5)]
    public void Starfall_Volley_Strikes_On_Cast_And_Once_Per_Full_Hasted_Second_Of_8(double haste)
    {
        double tick = 1.0 / (1 + haste);
        int hits = 1 + FullTicks(8.0, tick); // the strike on cast, then the full ticks
        var f = StepFights.Elarion(haste: haste);

        f.Cast<StarfallVolley>();
        f.Wait(12.0); // the 8 second volley ends

        Assert.Equal(hits, f.Damage<StarfallVolley>().Count);
    }

    /// <summary>
    /// A Starfall Volley keeps the haste you had when you cast it: if your haste drops during the volley, it still strikes as often as
    /// it would have at that haste.
    ///
    /// 20% haste and Skystrider's Grace's 30% make 50% at the cast; the Grace runs out 4 seconds into the volley.
    /// The rule is believed to be the game's.
    /// </summary>
    [Fact]
    public void Starfall_Volley_Keeps_The_Haste_It_Was_Cast_With()
    {
        double tick = 1.0 / (1 + 0.2 + 0.3);
        int hits = 1 + FullTicks(8.0, tick); // as at the cast's 50% haste
        var f = StepFights.Elarion(haste: 0.2);

        f.Cast<SkystridersGrace>();
        f.WaitUntil(16.0); // 4 seconds of the Grace left
        f.Cast<StarfallVolley>();
        f.Wait(12.0); // the Grace runs out, then the volley ends

        Assert.Equal(hits, f.Damage<StarfallVolley>().Count);
    }

    // ---- Heartseeker Barrage (Fusillade's critical strike chance is in ElarionTalents.cs)

    /// <summary>
    /// Heartseeker Barrage channels for 2 seconds, 2.5 with Fusillade, and fires an arrow every 0.2 seconds, faster with haste. It fires
    /// one arrow per full tick that fits in the channel, floor(channel / 0.2 x (1 + haste)), and never a partial one.
    /// </summary>
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(false, 0.1)]
    [InlineData(false, 0.2)]
    [InlineData(false, 0.3)]
    [InlineData(false, 0.4)]
    [InlineData(false, 0.5)]
    [InlineData(false, 0.6)]
    [InlineData(false, 0.7)]
    [InlineData(false, 0.8)]
    [InlineData(false, 0.9)]
    [InlineData(true, 0.0)]
    [InlineData(true, 0.1)]
    [InlineData(true, 0.2)]
    [InlineData(true, 0.3)]
    [InlineData(true, 0.4)]
    [InlineData(true, 0.5)]
    [InlineData(true, 0.6)]
    [InlineData(true, 0.7)]
    [InlineData(true, 0.73)]
    [InlineData(true, 0.8)]
    [InlineData(true, 0.9)]
    public void Heartseeker_Barrage_Fires_An_Arrow_Per_Full_Tick_And_Never_A_Partial_One(bool fusillade, double haste)
    {
        double channel = fusillade ? 2.5 : 2.0;
        double tick = 0.2 / (1 + haste);
        int arrows = FullTicks(channel, tick);
        var f = StepFights.Elarion(haste: haste, talents: fusillade ? [Fusillade.TalentId] : []);

        f.Cast<HeartseekerBarrage>();
        f.Wait(1.0); // the last arrow lands

        Assert.Equal(arrows, f.Damage<HeartseekerBarrage>().Count);
    }

    // ---- Skystrider's Supremacy (Fervent Supremacy's charges are in ElarionTalents.cs)

    /// <summary>
    /// Skystrider's Supremacy empowers the Multishots you start in the next 4 seconds, so how many fit depends on haste: the first at
    /// once, then one per hasted 1.5 second global cooldown that fits in the 4 seconds, 1 + floor(4 / (1.5 / (1 + haste))): 3 at no
    /// haste, 4 from 12.5%, 5 from 50%, 6 from 87.5%. Each fires 3 arrows at one enemy, and the next Multishot is not empowered.
    ///
    /// NB: in-game, there is some lag between pressing supremacy and starting to chain-cast MS.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.10)]
    [InlineData(0.14)]
    [InlineData(0.45)]
    [InlineData(0.54)]
    [InlineData(0.83)]
    [InlineData(0.91)]
    public void Skystriders_Supremacy_Empowers_The_Multishots_Started_In_4_Seconds(double haste)
    {
        double gcd = 1.5 / (1 + haste);
        int empowered = 1 + FullTicks(4.0, gcd); // the first at once, then one per global cooldown
        var f = StepFights.Elarion(haste: haste);

        f.Cast<SkystridersSupremacy>();
        for (int i = 0; i < empowered; i++) f.Cast<Multishot>();
        int empoweredArrows = f.Damage<Multishot>().Count;
        f.Cast<Multishot>();
        int nextArrows = f.Damage<Multishot>().Count - empoweredArrows;

        using var check = new Checks(f);
        check.Equal(3 * empowered, empoweredArrows);
        check.Equal(1, nextArrows);
    }
}
