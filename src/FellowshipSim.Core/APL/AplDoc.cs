namespace FellowshipSim.Core.APL;

/// <summary>A condition's name in the editor (<see cref="Label"/>) and its meaning in the condition reference.</summary>
public sealed record AplDoc(string Label, string Description);

/// <summary>A value an action list reads (<see cref="Value"/>) with its <see cref="AplDoc"/>.</summary>
public sealed record AplDescribed<T>(AplDoc Doc, T Value);
