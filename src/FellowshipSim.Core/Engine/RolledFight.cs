namespace FellowshipSim.Core.Engine;

/// <summary>
/// One fight of a run again with its seed, crits and damage spread rolled (<see cref="MonteCarloSimulator.RollFight"/>): its
/// casts and its hits, in order.
/// </summary>
public sealed record RolledFight(IReadOnlyList<CastReport> Casts, IReadOnlyList<HitReport> Hits);
