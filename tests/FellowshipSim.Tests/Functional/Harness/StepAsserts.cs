global using static FellowshipSim.Tests.Functional.StepAsserts;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Asserts with a tolerance, for damage ratios and times, and the tolerances <see cref="Checks"/> uses. A global <c>using static</c> makes
/// <c>AssertRatio</c> and <c>AssertSeconds</c> available in every test. Expected value first, as in xUnit.
///
/// <see cref="RatioTolerance"/>: damage is compared as a ratio between two fights that differ only by what is tested. With no damage spread
/// and decided crits the ratio is exact except for floating-point error, which is all 0.001 allows for.
///
/// <see cref="TimeTolerance"/>: the engine's clock counts whole milliseconds, so an event time can be up to 1ms from a computed time, and
/// the difference of two event times up to 2ms.
/// </summary>
public static class StepAsserts
{
    /// <summary>Damage ratios: exact to 0.001 when the fights have no spread and decided crits.</summary>
    public const double RatioTolerance = 0.001;

    /// <summary>Times: the engine rounds to the millisecond, so two roundings apart at most.</summary>
    public const double TimeTolerance = 0.002;

    public static void AssertRatio(double expected, double actual, double tolerance = RatioTolerance, string? what = null) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{what}{(what is null ? "" : ": ")}expected a ratio of {expected:F4}, got {actual:F4}"));

    public static void AssertSeconds(double expected, double actual, double tolerance = TimeTolerance, string? what = null) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{what}{(what is null ? "" : ": ")}expected {expected:F3}s, got {actual:F3}s"));
}
