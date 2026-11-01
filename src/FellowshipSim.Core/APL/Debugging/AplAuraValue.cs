namespace FellowshipSim.Core.APL.Debugging;

/// <summary>An aura's stacks and seconds left; <see cref="Remains"/> is null for an aura with no end.</summary>
public sealed record AplAuraValue(string Name, int Stacks, double? Remains);
