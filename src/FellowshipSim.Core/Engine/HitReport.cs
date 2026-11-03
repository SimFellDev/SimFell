namespace FellowshipSim.Core.Engine;

/// <summary>One hit of a sim's typical fight: when it landed (seconds into the fight), the source's name and ID, its damage, and whether it crit.</summary>
public sealed record HitReport(double Time, string Name, int Id, double Amount, bool Crit);
