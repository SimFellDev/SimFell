using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// A decorator that removes the +/-10% damage spread, so every hit deals exactly its base damage (times the crit multiplier on a crit) and
/// damage can be compared as exact ratios. Installed by <see cref="StepForcing.WithoutDamageSpread"/>.
///
/// The engine decides a hit's random parts once, when the hit is released: <see cref="IOutcomeSource.ResolveHit"/> returns a <see
/// cref="HitOutcome"/> (crit, travel time, spread), which the engine stores and passes to <see cref="IOutcomeSource.Land"/> when the hit
/// lands. <c>Land</c> multiplies the damage by <see cref="HitOutcome.Spread"/>. This decorator calls the wrapped <c>ResolveHit</c> and sets
/// <c>Spread</c> to 1.0 on its result.
///
/// The wrapped source still rolls the spread and the result is discarded, so the random sequence is unchanged and the seed's other outcomes
/// stay the same.
/// </summary>
public sealed class NoDamageSpread(IOutcomeSource inner) : OutcomeSourceDecorator(inner)
{
    public override HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx) => Inner.ResolveHit(hit, ctx) with { Spread = 1.0 };
}
