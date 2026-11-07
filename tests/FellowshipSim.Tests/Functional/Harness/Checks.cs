using System.Globalization;
using System.Runtime.CompilerServices;
using Xunit.Sdk;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Soft asserts: a failed check is recorded and the test continues, then the test fails at the end with every failed check. Use it when a
/// test checks several parts of one rule, so a failure shows every wrong part instead of only the first.
///
/// <code>
/// using var check = new Checks(f);
/// check.True(f.Has(IceBlitz.Buff));
/// f.WaitUntil(20.1);
/// check.False(f.Has(IceBlitz.Buff));
/// </code>
///
/// <c>using var</c> calls <see cref="Dispose"/> when the test method ends, and <c>Dispose</c> throws the failures. Each failure shows its
/// line, the checked expression (filled in by the compiler through <c>[CallerArgumentExpression]</c>) and, when given the fight, the fight
/// time: <c>line 140, at 20.100s: f.Has(IceBlitz.Buff) is true</c>. Add a <c>what:</c> label only when the expression does not identify the
/// value (in a loop).
///
/// If the test throws, <c>Dispose</c> still runs: with no failed check the exception is reported; with failed checks, the failed checks are
/// reported instead.
/// </summary>
public sealed class Checks(StepDriver? fight = null) : IDisposable
{
    private readonly List<string> _failures = [];

    public void True(bool condition, [CallerArgumentExpression(nameof(condition))] string what = "", [CallerLineNumber] int line = 0)
    {
        if (!condition) Fail(line, $"{what} is false");
    }

    public void False(bool condition, [CallerArgumentExpression(nameof(condition))] string what = "", [CallerLineNumber] int line = 0)
    {
        if (condition) Fail(line, $"{what} is true");
    }

    /// <summary>Checks that <paramref name="actual"/> equals <paramref name="expected"/> within <paramref name="tolerance"/>. The default is exact, for
    /// counts, stacks and resources.</summary>
    public void Equal(double expected, double actual, double tolerance = 1e-9,
        [CallerArgumentExpression(nameof(actual))] string what = "", [CallerLineNumber] int line = 0)
    {
        if (Math.Abs(expected - actual) > tolerance) Fail(line, string.Create(CultureInfo.InvariantCulture, $"{what} is {actual:G6}, expected {expected:G6}"));
    }

    /// <summary>A damage ratio, within <see cref="StepAsserts.RatioTolerance"/> as <see cref="StepAsserts.AssertRatio"/>.</summary>
    public void Ratio(double expected, double actual, [CallerArgumentExpression(nameof(actual))] string what = "",
        [CallerLineNumber] int line = 0) =>
        Equal(expected, actual, StepAsserts.RatioTolerance, what, line);

    /// <summary>A time in seconds, within <see cref="StepAsserts.TimeTolerance"/> as <see cref="StepAsserts.AssertSeconds"/>.</summary>
    public void Seconds(double expected, double actual, [CallerArgumentExpression(nameof(actual))] string what = "",
        [CallerLineNumber] int line = 0) =>
        Equal(expected, actual, StepAsserts.TimeTolerance, what, line);

    private void Fail(int line, string message) =>
        _failures.Add(fight is null ? $"line {line}: {message}" : string.Create(CultureInfo.InvariantCulture, $"line {line}, at {fight.Seconds():F3}s: {message}"));

    public void Dispose()
    {
        if (_failures.Count > 0)
            throw new XunitException($"{_failures.Count} check(s) failed:{Environment.NewLine}{string.Join(Environment.NewLine, _failures)}");
    }
}
