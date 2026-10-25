namespace FellowshipSim.Core.Engine;

/// <summary>One cast of a sim's typical fight: when it finished (seconds into the fight), the ability's name and its ID.</summary>
public sealed record CastReport(double Time, string Name, int Id);
