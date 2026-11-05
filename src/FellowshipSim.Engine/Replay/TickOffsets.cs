namespace FellowshipSim.Engine.Replay;

/// <summary>Log ticks outside the engine's tick time.</summary>
/// <param name="MedianSeconds">Log time minus the engine's, median.</param>
/// <param name="LateShare">Share of them later than the engine's.</param>
public sealed record TickOffsets(int Count, double MedianSeconds, double LateShare);
