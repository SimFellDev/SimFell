using System.Text.RegularExpressions;
using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>The completion list of a code field at its caret, and the text with a pick written in place of the part at the caret.</summary>
public static partial class CodeEdits
{
    /// <summary>The most items the completion list shows.</summary>
    public const int MaxItems = 50;

    /// <summary>The completion list's width the field keeps room for right of its left edge.</summary>
    public const double ListWidth = 260;

    [GeneratedRegex(@"[A-Za-z0-9_.]*\z")]
    private static partial Regex PathBefore();

    [GeneratedRegex(@"[A-Za-z0-9_]*\z")]
    private static partial Regex PartBefore();

    [GeneratedRegex("^[A-Za-z0-9_]*")]
    private static partial Regex PartAfter();

    /// <summary>
    /// The completion list for the path before the caret, or null when the field does not have the focus, the path is a
    /// number, nothing completes it, or it is empty and <paramref name="always"/> (Ctrl+Space) is false.
    /// </summary>
    public static CompletionList? Open(AplVocabulary v, string text, CaretPlace caret, bool always)
    {
        string word = PathBefore().Match(text[..Math.Min(caret.At, text.Length)]).Value;
        if (!caret.Focused || (word.Length > 0 && char.IsAsciiDigit(word[0])) || (word.Length == 0 && !always)) return null;
        var items = Completions.For(v, word).Take(MaxItems).ToList();
        if (items.Count == 0) return null;
        return new CompletionList(items, 0, Math.Max(0, Math.Min(caret.Left, caret.Width - ListWidth)), caret.Top + 4);
    }

    /// <summary>
    /// <paramref name="text"/> with <paramref name="item"/> in place of the part at <paramref name="at"/>, and the caret after
    /// it. A pick with parts after it is written with its '.'.
    /// </summary>
    public static (string Text, int Caret) Accept(string text, int at, Completion item)
    {
        at = Math.Min(at, text.Length);
        string partial = PartBefore().Match(text[..at]).Value;
        string rest = PartAfter().Match(text[at..]).Value;
        string written = item.Text + (item.HasMore ? "." : "");
        return (text[..(at - partial.Length)] + written + text[(at + rest.Length)..], at - partial.Length + written.Length);
    }
}
