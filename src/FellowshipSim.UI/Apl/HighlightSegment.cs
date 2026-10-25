using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>A run of highlighted text: its token kind (null for text between tokens), and whether a parse error is in it.</summary>
public sealed record HighlightSegment(string Text, AplTokenKind? Kind = null, bool Error = false)
{
    /// <summary>
    /// <paramref name="text"/> cut into its tokens and the text between them. The character at a parse error is its own
    /// segment, and an error at the end of the text is a space.
    /// </summary>
    public static IReadOnlyList<HighlightSegment> Of(string text, AplExpressionCheck? check)
    {
        var parts = new List<HighlightSegment>();
        int? errorAt = check?.ErrorStart;
        int at = 0;
        void Plain(int end)
        {
            end = Math.Min(end, text.Length);
            if (end <= at) return;
            if (errorAt is { } e && e >= at && e < end)
            {
                if (e > at) parts.Add(new HighlightSegment(text[at..e]));
                parts.Add(new HighlightSegment(text[e].ToString(), Error: true));
                at = e + 1;
            }
            if (end > at) parts.Add(new HighlightSegment(text[at..end]));
            at = Math.Max(at, end);
        }
        foreach (var token in check?.Tokens ?? [])
        {
            Plain(token.Start);
            int end = Math.Min(token.Start + token.Length, text.Length);
            bool error = errorAt is { } e && e >= token.Start && e < token.Start + token.Length;
            parts.Add(new HighlightSegment(token.Start < end ? text[token.Start..end] : "", token.Kind, error));
            at = token.Start + token.Length;
        }
        Plain(text.Length);
        if (errorAt is { } last && last >= text.Length) parts.Add(new HighlightSegment(" ", Error: true));
        return parts;
    }
}
