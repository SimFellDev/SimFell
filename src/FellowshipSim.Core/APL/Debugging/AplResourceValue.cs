namespace FellowshipSim.Core.APL.Debugging;

/// <summary>A resource's amount and maximum.</summary>
public sealed record AplResourceValue(string Name, double Current, double Max);
