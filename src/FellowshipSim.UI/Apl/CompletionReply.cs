namespace FellowshipSim.UI.Apl;

/// <summary>The completion list for codeField.js: its items, and the part of the text from <see cref="From"/> to <see cref="To"/> a pick is written in place of.</summary>
public sealed record CompletionReply(int From, int To, IReadOnlyList<CompletionOption> Options)
{
    public static CompletionReply Of(CompletionList list) => new(list.From, list.To, list.Items.Select(CompletionOption.Of).ToList());
}
