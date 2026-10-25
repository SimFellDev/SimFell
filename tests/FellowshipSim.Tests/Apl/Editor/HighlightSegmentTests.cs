using FellowshipSim.Core.APL;
using FellowshipSim.UI.Apl;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>The coloured runs of a code field's text, with the character at a parse error marked.</summary>
public class HighlightSegmentTests
{
    private static AplExpressionCheck Check(int? errorStart, params AplTokenSpan[] tokens) => new(tokens, errorStart == null ? null : "error", errorStart);

    [Fact]
    public void Tokens_have_their_kinds_and_the_text_between_them_is_plain()
    {
        var segments = HighlightSegment.Of("a & 3", Check(null,
            new AplTokenSpan(0, 1, AplTokenKind.Named), new AplTokenSpan(2, 1, AplTokenKind.Operator), new AplTokenSpan(4, 1, AplTokenKind.Number)));

        Assert.Equal(
        [
            new HighlightSegment("a", AplTokenKind.Named), new HighlightSegment(" "), new HighlightSegment("&", AplTokenKind.Operator),
            new HighlightSegment(" "), new HighlightSegment("3", AplTokenKind.Number),
        ], segments);
    }

    [Fact]
    public void An_error_between_tokens_marks_its_character()
    {
        var segments = HighlightSegment.Of("a  ) b", Check(3, new AplTokenSpan(0, 1, AplTokenKind.Named), new AplTokenSpan(5, 1, AplTokenKind.Named)));

        Assert.Equal(
        [
            new HighlightSegment("a", AplTokenKind.Named), new HighlightSegment("  "), new HighlightSegment(")", Error: true),
            new HighlightSegment(" "), new HighlightSegment("b", AplTokenKind.Named),
        ], segments);
    }

    [Fact]
    public void An_error_in_a_token_marks_the_token_and_an_error_at_the_end_is_a_space()
    {
        Assert.Equal([new HighlightSegment("ab", AplTokenKind.Unknown, true)],
            HighlightSegment.Of("ab", Check(1, new AplTokenSpan(0, 2, AplTokenKind.Unknown))));
        Assert.Equal([new HighlightSegment("a", AplTokenKind.Named), new HighlightSegment(" ", Error: true)],
            HighlightSegment.Of("a", Check(1, new AplTokenSpan(0, 1, AplTokenKind.Named))));
    }

    [Fact]
    public void Text_without_a_check_is_one_plain_run()
    {
        Assert.Equal([new HighlightSegment("a & b")], HighlightSegment.Of("a & b", null));
        Assert.Empty(HighlightSegment.Of("", null));
    }

    [Fact]
    public void The_engine_check_of_text_covers_every_character()
    {
        const string text = "worb >= 3 and not buff.nothing_here.up";
        var segments = HighlightSegment.Of(text, AplVocabulary.For("Rime").Check(text));

        Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
        Assert.Contains(segments, s => s.Kind == AplTokenKind.Unknown);
    }
}
