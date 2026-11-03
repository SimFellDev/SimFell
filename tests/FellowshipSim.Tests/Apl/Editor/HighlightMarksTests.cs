using FellowshipSim.Core.APL;
using FellowshipSim.UI.Apl;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>The token classes of a checked text for the editor (<see cref="HighlightMarks"/>).</summary>
public class HighlightMarksTests
{
    private static AplExpressionCheck Check(int? errorStart, params AplTokenSpan[] tokens) => new(tokens, errorStart == null ? null : "error", errorStart);

    [Fact]
    public void Each_token_is_its_start_length_and_class_and_a_class_is_listed_once()
    {
        var marks = HighlightMarks.Of("a & b", Check(null,
            new AplTokenSpan(0, 1, AplTokenKind.Named), new AplTokenSpan(2, 1, AplTokenKind.Operator), new AplTokenSpan(4, 1, AplTokenKind.Named)));

        Assert.Equal(["tok-named", "tok-operator"], marks.Classes);
        Assert.Equal([0, 1, 0, 2, 1, 1, 4, 1, 0], marks.Marks);
        Assert.False(marks.ErrorAtEnd);
    }

    [Fact]
    public void An_error_between_tokens_marks_its_character_and_an_error_at_the_end_is_a_flag()
    {
        var between = HighlightMarks.Of("a ) b", Check(2, new AplTokenSpan(0, 1, AplTokenKind.Named), new AplTokenSpan(4, 1, AplTokenKind.Named)));
        Assert.Equal(["tok-named", "tok-error"], between.Classes);
        Assert.Equal([0, 1, 0, 2, 1, 1, 4, 1, 0], between.Marks);

        var atEnd = HighlightMarks.Of("a &", Check(3, new AplTokenSpan(0, 1, AplTokenKind.Named), new AplTokenSpan(2, 1, AplTokenKind.Operator)));
        Assert.True(atEnd.ErrorAtEnd);
        Assert.Equal([0, 1, 0, 2, 1, 1], atEnd.Marks);
    }
}
