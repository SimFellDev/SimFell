namespace FellowshipSim.UI.Apl;

/// <summary>A builder row's path in parts: its kind, its name, and its property (a resource or a fight value has none).</summary>
public sealed record PathParts(string Kind, string Name, string? Property = null);
