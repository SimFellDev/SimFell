namespace FellowshipSim.Core.APL.Editing;

/// <summary>The items of the completion list, and the part of the text from <see cref="From"/> to <see cref="To"/> a pick is written in place of.</summary>
public sealed record CompletionList(IReadOnlyList<Completion> Items, int From, int To);
