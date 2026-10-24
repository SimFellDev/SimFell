namespace FellowshipSim.Core.Engine;

/// <summary>
/// A sim's Spirit Points over the fight, at the start of each second: <see cref="Mean"/> over the fights still going at that
/// second, and <see cref="Typical"/> in one typical fight (the sampled fight nearest the mean damage). <see cref="Max"/> is
/// the hero's most Spirit Points.
/// </summary>
public sealed record SpiritTimeline(IReadOnlyList<double> Mean, IReadOnlyList<double> Typical, double Max);
