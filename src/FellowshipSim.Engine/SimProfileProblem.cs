namespace FellowshipSim.Engine;

/// <summary>A problem of a <c>.simc</c> profile: its line number from 1, or null for a problem of the whole dungeon.</summary>
public sealed record SimProfileProblem(int? Line, string Problem);
