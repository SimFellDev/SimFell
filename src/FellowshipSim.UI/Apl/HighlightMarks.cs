using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// The token classes of a checked text for codeField.js: each mark is three numbers in <see cref="Marks"/>, its start, its
/// length and its index in <see cref="Classes"/>. <see cref="ErrorAtEnd"/> is true for a parse error at the end of the text.
/// </summary>
public sealed record HighlightMarks(string Text, IReadOnlyList<string> Classes, IReadOnlyList<int> Marks, bool ErrorAtEnd)
{
    public static HighlightMarks Of(string text, AplExpressionCheck? check)
    {
        var classes = new List<string>();
        var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var marks = new List<int>();
        int at = 0;
        foreach (var segment in HighlightSegment.Of(text, check))
        {
            if (at >= text.Length && segment.Error) return new HighlightMarks(text, classes, marks, true);
            if (segment.Text.Length > 0 && HighlightSegment.ClassOf(segment.Kind, segment.Error) is { } classNames)
            {
                if (!indexOf.TryGetValue(classNames, out int index))
                {
                    index = classes.Count;
                    classes.Add(classNames);
                    indexOf[classNames] = index;
                }
                marks.AddRange([at, segment.Text.Length, index]);
            }
            at += segment.Text.Length;
        }
        return new HighlightMarks(text, classes, marks, false);
    }
}
