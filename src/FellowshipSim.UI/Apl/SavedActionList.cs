using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>A saved action list's text, with its actions, or with the parse error when the text does not parse.</summary>
public sealed record SavedActionList(string Text, IReadOnlyList<AplDocumentAction>? Actions, string? Error);
