using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// A decorator that pins the random duration of effects that roll one (Volatile Poison's 6 to 8 seconds), and records every duration
/// rolled. Installed by <c>f.ForceDurations(request =&gt; 7.0)</c>.
///
/// Every such roll is an <see cref="IOutcomeSource.RollDuration"/> call. For each call, <paramref name="force"/> returns the seconds to
/// use, or null to let the wrapped source roll it. Every call is recorded in <see cref="Rolls"/> with the range the engine gave it and the
/// duration used.
/// </summary>
public sealed class ForcedDurations(IOutcomeSource inner, Func<EffectDurationRequest, double?> force) : OutcomeSourceDecorator(inner)
{
    /// <summary>Every duration rolled so far: the request's range and the seconds used.</summary>
    public List<(double Min, double Max, double Seconds)> Rolls { get; } = new();

    public override double RollDuration(in EffectDurationRequest application, SimulationContext ctx)
    {
        double rolled = Inner.RollDuration(application, ctx); // the wrapped source still rolls, so the random sequence is unchanged
        double seconds = force(application) ?? rolled;
        Rolls.Add((application.MinSeconds, application.MaxSeconds, seconds));
        return seconds;
    }
}
