using System.Text.RegularExpressions;

namespace FellowshipSim.Core.APL.Editing;

/// <summary>
/// The completion list of the action-list text at its caret. The line's part at the caret picks the items: a list prefix,
/// an action's name after <c>+=/</c>, an argument's key after a ',', a list or variable after <c>name=</c>, a variable op
/// after <c>op=</c>, and the paths of <see cref="Completions"/> in an <c>if</c>, <c>target_if</c> or <c>value</c> expression.
/// </summary>
public static partial class ListEdits
{
    [GeneratedRegex(@"^\s*[A-Za-z0-9_.]*\z")]
    private static partial Regex Prefix();

    [GeneratedRegex(@"^\s*[A-Za-z0-9_.]+\s*\+?=\s*/?\s*([A-Za-z0-9_]*)\z")]
    private static partial Regex ActionName();

    [GeneratedRegex(@"^\s*[A-Za-z0-9_.]+\s*\+?=\s*/?\s*([A-Za-z0-9_]+)")]
    private static partial Regex ActionOf();

    [GeneratedRegex(@"^\s*([A-Za-z0-9_]*)\z")]
    private static partial Regex Key();

    [GeneratedRegex(@"^\s*([A-Za-z0-9_]+)\s*=(.*)\z", RegexOptions.Singleline)]
    private static partial Regex Argument();

    // A line of a named list ("actions.aoe+="), and a variable action's name ("variable,name=orbs").
    private static readonly Regex ListLine = new(
        $@"^\s*{Regex.Escape(AplActionLine.ListPrefix)}\.([A-Za-z0-9_]+)\s*\+?=", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex VariableLine = new(
        $@"\b{Regex.Escape(AplVariable.ActionName)}\s*,(?:[^\n]*?,)?\s*{Regex.Escape(AplVariable.NameKey)}\s*=\s*([A-Za-z0-9_]+)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The completion list for the part before <paramref name="at"/>, or null when it is in a comment or a number, nothing
    /// completes the part, or the part is empty and <paramref name="always"/> (Ctrl+Space) is false.
    /// </summary>
    public static CompletionList? Open(AplVocabulary v, string text, int at, bool always)
    {
        at = Math.Min(at, text.Length);
        int lineStart = at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
        string before = text[lineStart..at];
        if (before.TrimStart().StartsWith('#')) return null;

        // The lists and variables are those of the other lines: the line at the caret is still being written. Only a
        // list or variable name reads them.
        int lineEnd = text.IndexOf('\n', at) is var end and >= 0 ? end : text.Length;
        var items = Items(v, () => text[..lineStart] + text[lineEnd..], before, always);
        if (items == null || items.Count == 0) return null;
        var (from, to) = CodeEdits.PartAt(text, at);
        return new CompletionList(items.Take(CodeEdits.MaxItems).ToList(), from, to);
    }

    private static IReadOnlyList<Completion>? Items(AplVocabulary v, Func<string> otherLines, string before, bool always)
    {
        int comma = LastTopLevelComma(before);
        if (comma < 0)
        {
            if (ActionName().Match(before) is { Success: true } action)
            {
                return Filter(Actions(v), action.Groups[1].Value, always);
            }
            if (!Prefix().IsMatch(before)) return null;
            string prefix = before.Trim();
            int dot = prefix.IndexOf('.');
            return dot < 0
                ? Filter([Item(AplActionLine.ListPrefix, AplTokenKind.List), .. Actions(v)], prefix, always)
                : prefix[..dot] == AplActionLine.ListPrefix ? Filter(Lists(otherLines()), prefix[(dot + 1)..], always) : null;
        }

        string actionName = ActionOf().Match(before) is { Success: true } named ? named.Groups[1].Value : "";
        string segment = before[(comma + 1)..];
        if (Key().Match(segment) is { Success: true } key)
        {
            return Filter(Keys(v, actionName), key.Groups[1].Value, always);
        }
        if (Argument().Match(segment) is not { Success: true } argument) return null;

        string value = argument.Groups[2].Value;
        string partial = CodeEdits.PartBefore().Match(value).Value;
        switch (argument.Groups[1].Value.ToLowerInvariant())
        {
            case AplActionLine.ListNameKey when v.ListActions.Contains(actionName):
                return Filter(Lists(otherLines()), partial, always);
            case AplVariable.NameKey when actionName == AplVariable.ActionName:
                return Filter(Variables(otherLines()).Select(n => Item(n, AplTokenKind.Named)).ToList(), partial, always);
            case AplVariable.OpKey when actionName == AplVariable.ActionName:
                return Filter(Enum.GetValues<AplVariableOp>().Select(op => Item(op.ToString().ToLowerInvariant(), AplTokenKind.Keyword)).ToList(),
                    partial, always);
            case AplActionLine.TargetIfKey when !value.Contains(':') && Key().IsMatch(value):
                return Filter(v.TargetSelections.Select(s => Item(s.Prefix, AplTokenKind.Keyword, s.Label))
                    .Concat(Completions.For(v, value.Trim(), Variables(otherLines()))).ToList(), partial, always);
            case AplActionLine.ConditionKey or AplActionLine.TargetIfKey or AplVariable.ValueKey:
                string path = CodeEdits.PathBefore().Match(value).Value;
                if ((path.Length > 0 && char.IsAsciiDigit(path[0])) || (path.Length == 0 && !always)) return null;
                return Completions.For(v, path, Variables(otherLines()));
            default:
                return null;
        }
    }

    // The last ',' outside parentheses: the start of the argument at the caret.
    private static int LastTopLevelComma(string line)
    {
        int depth = 0, comma = -1;
        for (int i = 0; i < line.Length; i++)
        {
            switch (line[i])
            {
                case '(': depth++; break;
                case ')': depth = Math.Max(0, depth - 1); break;
                case ',' when depth == 0: comma = i; break;
            }
        }
        return comma;
    }

    private static List<Completion> Actions(AplVocabulary v) =>
    [
        .. v.Abilities.Select(a => Item(a.Slug, AplTokenKind.Cooldown, a.Name == a.Slug ? "" : a.Name)),
        .. v.ListActions.Select(a => Item(a, AplTokenKind.Keyword)),
        Item(AplVariable.ActionName, AplTokenKind.Keyword),
        Item(AplSwitchTarget.ActionName, AplTokenKind.Keyword),
    ];

    private static List<Completion> Keys(AplVocabulary v, string action)
    {
        var keys = new List<string> { AplActionLine.ConditionKey };
        if (v.ListActions.Contains(action)) keys.Add(AplActionLine.ListNameKey);
        else if (action == AplVariable.ActionName) keys.AddRange([AplVariable.NameKey, AplVariable.ValueKey, AplVariable.OpKey, AplVariable.DefaultKey]);
        else if (action == AplSwitchTarget.ActionName) keys.Add(AplActionLine.TargetIfKey);
        else keys.AddRange([AplActionLine.TargetIfKey, AplActionLine.UseOffGcdKey, AplActionLine.UseWhileCastingKey]);
        if (v.Channels.Any(c => c.Slug == action)) keys.AddRange([AplActionLine.InterruptIfKey, AplActionLine.InterruptImmediateKey]);
        return keys.Select(k => Item(k, AplTokenKind.Keyword)).ToList();
    }

    private static List<string> Variables(string text) => VariableLine.Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();

    private static List<Completion> Lists(string text) =>
        ListLine.Matches(text).Select(m => m.Groups[1].Value).Distinct().Select(l => Item(l, AplTokenKind.List)).ToList();

    private static Completion Item(string text, AplTokenKind kind, string detail = "") =>
        new(text, new CompletionNode(kind) { Leaf = true, Detail = detail });

    // Items starting with the part first, then those containing it; none for a finished name, or for an empty part unless always.
    public static List<Completion>? Filter(IReadOnlyList<Completion> items, string partial, bool always)
    {
        if (partial.Length == 0 && !always) return null;
        var found = items.Where(i => i.Text.StartsWith(partial, StringComparison.Ordinal))
            .Concat(partial.Length > 0 ? items.Where(i => !i.Text.StartsWith(partial, StringComparison.Ordinal) && i.Text.Contains(partial, StringComparison.Ordinal)) : [])
            .DistinctBy(i => i.Text)
            .ToList();
        return found.Count == 1 && found[0].Text == partial ? null : found;
    }
}
