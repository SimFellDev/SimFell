namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// One action line of a traced fight: its list, its line number in the action-list text and the line itself.
/// <see cref="InBuild"/> is false for an action naming an ability of another build of the hero, which no decision reads.
/// </summary>
public sealed record AplTraceLine(string List, int LineNumber, string Text, bool InBuild);
