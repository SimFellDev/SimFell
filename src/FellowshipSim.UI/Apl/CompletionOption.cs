namespace FellowshipSim.UI.Apl;

/// <summary>
/// One item of the completion list for codeField.js: its text, the text a pick writes, its detail and its token class.
/// <see cref="More"/> is true when the pick has parts after it, so the list opens again on them.
/// </summary>
public sealed record CompletionOption(string Label, string Written, string Detail, string? Class, bool More)
{
    public static CompletionOption Of(Completion item) =>
        new(item.Text, item.Written, item.Node.Detail, HighlightSegment.ClassOf(item.Node.Kind, false), item.HasMore);
}
