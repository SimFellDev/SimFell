using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many ticks Mara's periodic effects deal at each haste. Haste speeds the ticks up, so the numbers go up in steps (breakpoints).
/// Dev: counts the damage events of one application on a single enemy and compares them with the formula. The formulas are computed in floating
/// point, so a whole number in theory is rounded with a tolerance before the floor.
/// </summary>
public class MaraHasteBreakpoints
{
    private const double Epsilon = 1e-9;

    /// <summary>The engine counts a last, partial tick only when at least 0.1 seconds are left after the last full one.</summary>
    private const double MinPartialTickSeconds = 0.1;

    private static int Floor(double x) => (int)Math.Floor(x + Epsilon);

    /// <summary>
    /// Player: Hemorrhaging Strike's bleed ticks every 3 seconds shortened by haste for 12 + 3 seconds per Combo Point: floor(duration x (1 +
    /// haste) / 3) full ticks, plus a partial one for the time left (at least 0.1 seconds) over the rest of the duration, whose damage is that fraction of a tick.
    /// Dev: a bleed at 1 Combo Point (15s) and at 2 (18s), at haste steps; hits counted after the bleed is over; the damage of all ticks over a
    /// full tick is duration x (1 + haste) / 3.
    /// </summary>
    [Theory]
    [InlineData(1, 0.1)]
    [InlineData(1, 0.2)]
    [InlineData(1, 0.3)]
    [InlineData(2, 0.1)]
    [InlineData(2, 0.25)]
    [InlineData(2, 0.5)]
    public void The_Bleed_Ticks_Floor_Of_Duration_Over_Period_Times_1_Plus_Haste_Plus_A_Partial(int points, double haste)
    {
        double duration = 12 + 3 * points;
        int full = Floor(duration * (1 + haste) / 3);
        double left = duration - full * 3 / (1 + haste);
        int expected = full + (left >= MinPartialTickSeconds - Epsilon ? 1 : 0);
        var f = StepFights.Mara(haste);
        f.SetComboPoints(points);

        f.Cast<HemorrhagingStrike>();
        f.Wait(duration + 5);

        var ticks = f.Damage("Hemorrhage");
        Assert.Equal(expected, ticks.Count);
        Assert.Equal(duration * (1 + haste) / 3, ticks.Sum(t => t.Value) / ticks[0].Value, 0.01);
    }

    /// <summary>
    /// Player: Seething Poison ticks every 2 seconds shortened by haste, for 60 seconds that haste does not shorten: floor(60 x (1 + haste) / 2) full
    /// ticks plus a partial one.
    /// Dev: a Widow's Bite from Stealth; 65 seconds; at hastes where the 60 seconds are not a whole number of ticks.
    /// </summary>
    [Theory]
    [InlineData(0.05)]
    [InlineData(0.15)]
    [InlineData(0.25)]
    [InlineData(0.35)]
    public void Seething_Poison_Ticks_Floor_Of_60_Over_Period_Times_1_Plus_Haste_Plus_A_Partial(double haste)
    {
        int full = Floor(60 * (1 + haste) / 2);
        double left = 60 - full * 2 / (1 + haste);
        int expected = full + (left >= MinPartialTickSeconds - Epsilon ? 1 : 0);
        var f = StepFights.Mara(haste);
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>();

        f.Wait(65);

        Assert.Equal(expected, f.Damage("Seething Poison").Count);
    }

    /// <summary>
    /// Player: Volatile Poison ticks 6 times at no haste, and more with haste: 6 x (1 + haste) rounded to the nearest, spread evenly over its
    /// duration, then it erupts once.
    /// Dev: a Skittering Blades from Stealth; 10 seconds; the ticks (effect 1731) are counted and the erupts (effect 1730) are 1. Hastes are away
    /// from the rounding edges.
    /// </summary>
    [Theory]
    [InlineData(0.0, 6)]
    [InlineData(0.13, 7)]
    [InlineData(0.2, 7)]
    [InlineData(0.3, 8)]
    [InlineData(0.43, 9)]
    [InlineData(0.7, 10)]
    public void Volatile_Poison_Ticks_6_Times_1_Plus_Haste_Rounded(double haste, int ticks)
    {
        var f = StepFights.Mara(haste);
        f.Cast<BroodingShadows>();
        f.Cast<StealthSkitteringBlades>();

        f.Wait(10);

        Assert.Equal(ticks, f.EffectTicks(1731).Count);
        Assert.Single(f.EffectTicks(1730));
    }
}
