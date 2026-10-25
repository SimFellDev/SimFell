namespace FellowshipSim.UI.Apl;

/// <summary>The shown completion list: its items, the picked item, and its place in the field.</summary>
public sealed record CompletionList(IReadOnlyList<Completion> Items, int Selected, double Left, double Top);
