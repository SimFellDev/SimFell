namespace FellowshipSim.UI.Apl;

/// <summary>An action list: its name and its actions in priority order. The editor shows the default list first.</summary>
public sealed record ActionList(string Name, IReadOnlyList<EditorAction> Actions);
