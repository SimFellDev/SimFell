namespace FellowshipSim.UI.Apl;

/// <summary>
/// The action list the Sim page runs, as its embedded editor has it: the saved list picked (kept/hero/name, empty for an
/// imported list), its name, and its text with the edits not saved.
/// </summary>
public sealed record ActionListPick(string Value, string Name, string Text);
