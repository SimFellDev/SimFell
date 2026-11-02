namespace FellowshipSim.Core.APL;

/// <summary>
/// The check of one line of an action list (<see cref="AplListCheck"/>), at offsets from the line's start: its tokens, the
/// first error with its offset, the list its prefix names, and the tokens naming a list for a run_action_list or
/// call_action_list. Problems and the error do not name the line, so the same text checks once wherever it is.
/// </summary>
internal sealed record AplLineCheck(
    IReadOnlyList<AplTokenSpan> Tokens,
    string? Error,
    int ErrorAt,
    string? List,
    IReadOnlyList<(int Token, string Name)> ListNames);
