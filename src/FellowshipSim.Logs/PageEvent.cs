namespace FellowshipSim.Logs;

/// <summary>
/// One event of an <see cref="EventsPage"/>: its place in the answer (<see cref="Start"/>, <see cref="Length"/>), and its
/// <c>timestamp</c>, <c>sourceID</c> and <c>targetID</c>, each null when the event has none.
/// </summary>
internal readonly record struct PageEvent(int Start, int Length, double? Timestamp, int? SourceId, int? TargetId);
