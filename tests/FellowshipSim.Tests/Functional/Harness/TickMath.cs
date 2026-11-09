global using static FellowshipSim.Tests.Functional.TickMath;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many ticks fit in a duration, for the tests that state a tick count as a formula: <c>FullTicks(30, 2 / 1.1) + PartialTick(30, 2 /
/// 1.1, 0.1)</c> for a damage over time (0.1 seconds), a server frame (1/30 second) for another periodic effect such as a channel
/// (WRITING_TESTS.md, Tolerances). A global <c>using static</c> makes them available in every test.
/// </summary>
public static class TickMath
{
    /// <summary>
    /// How many full ticks of <paramref name="tick"/> seconds fit in <paramref name="duration"/>. Rounded before the floor: in floating
    /// point 2.8 / 0.4 is 6.999999999999999, which is 7 ticks.
    /// </summary>
    public static int FullTicks(double duration, double tick) => (int)Math.Floor(duration / tick + 1e-9);

    /// <summary>
    /// 1 for a last partial tick, else 0: when at least <paramref name="threshold"/> seconds are left after the full ticks, or, with no
    /// threshold, when any time is left.
    /// </summary>
    public static int PartialTick(double duration, double tick, double threshold = 0)
    {
        double left = duration - FullTicks(duration, tick) * tick;
        return threshold > 0 ? (left >= threshold - 1e-9 ? 1 : 0) : (left > 1e-9 ? 1 : 0);
    }
}
