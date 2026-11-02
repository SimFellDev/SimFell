using System.Text.RegularExpressions;
using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>The completion list of a code field at its caret, and the part at the caret a pick is written in place of.</summary>
public static partial class CodeEdits
{
    /// <summary>The most items the completion list shows.</summary>
    public const int MaxItems = 50;

    [GeneratedRegex(@"[A-Za-z0-9_.]*\z")]
    internal static partial Regex PathBefore();

    [GeneratedRegex(@"[A-Za-z0-9_]*\z")]
    internal static partial Regex PartBefore();

    [GeneratedRegex("^[A-Za-z0-9_]*")]
    private static partial Regex PartAfter();
    /// <summary>
    /// The completion list for the path before <paramref name="at"/>, or null when the path is a number, nothing completes
    /// it, or it is empty and <paramref name="always"/> (Ctrl+Space) is false.
    /// </summary>
    public static CompletionList? Open(AplVocabulary v, string text, int at, bool always, IReadOnlyList<string>? variables = null)
    {
        at = Math.Min(at, text.Length);
        string word = PathBefore().Match(text[..at]).Value;
        if ((word.Length > 0 && char.IsAsciiDigit(word[0])) || (word.Length == 0 && !always)) return null;
        var items = Completions.For(v, word, variables).Take(MaxItems).ToList();
        if (items.Count == 0) return null;
        var (from, to) = PartAt(text, at);
        return new CompletionList(items, from, to);
    }

    /// <summary>The part at <paramref name="at"/> a pick is written in place of: the name characters either side of it.</summary>
    public static (int From, int To) PartAt(string text, int at)
    {
        at = Math.Min(at, text.Length);
        string partial = PartBefore().Match(text[..at]).Value;
        string rest = PartAfter().Match(text[at..]).Value;
        return (at - partial.Length, at + rest.Length);
    }
}
