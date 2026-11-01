namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// One decision: its time in seconds, the hero and enemies at that time (<see cref="State"/>, before the cast), each step
/// it read in order, and its cast. <see cref="CastLine"/>, <see cref="Cast"/> and <see cref="Target"/> are null for a
/// decision that cast nothing.
/// </summary>
public sealed record AplDecision(
    double Time,
    AplHeroState State,
    IReadOnlyList<AplStepTrace> Steps,
    int? CastLine,
    string? Cast,
    string? Target);
