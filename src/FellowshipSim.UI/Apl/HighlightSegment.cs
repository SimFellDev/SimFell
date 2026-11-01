using System.Net;
using System.Text;
using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>A run of highlighted text: its token kind (null for text between tokens), and whether a parse error is in it.</summary>
public sealed record HighlightSegment(string Text, AplTokenKind? Kind = null, bool Error = false)
{
    /// <summary>The classes of a token's text (design-system.css): tok-&lt;kind&gt;, and tok-error at a parse error; null for neither.</summary>
    public static string? ClassOf(AplTokenKind? kind, bool error)
    {
        string? kindClass = kind is { } k ? $"tok-{k.ToString().ToLowerInvariant()}" : null;
        return error ? (kindClass == null ? "tok-error" : $"{kindClass} tok-error") : kindClass;
    }

    /// <summary>
    /// <paramref name="text"/> as HTML with each segment of <see cref="Of"/> in a span of its classes. A long action list
    /// has thousands of tokens: one string sets them all at once.
    /// </summary>
    public static string Html(string text, AplExpressionCheck? check)
    {
        var html = new StringBuilder(text.Length * 2);
        foreach (var segment in Of(text, check))
        {
            string encoded = WebUtility.HtmlEncode(segment.Text);
            if (ClassOf(segment.Kind, segment.Error) is { } classes) html.Append("<span class=\"").Append(classes).Append("\">").Append(encoded).Append("</span>");
            else html.Append(encoded);
        }
        return html.ToString();
    }

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
