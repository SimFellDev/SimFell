using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Superpower;
using Superpower.Model;

namespace FellowshipSim.Core.APL;

/// <summary>
/// The check of a whole action list's text for one hero: each line's tokens with their kinds and offsets in the text, the
/// problem of each name the hero does not have, and the first line that does not parse.
/// </summary>
public static class AplListCheck
{
    /// <summary>The most lines kept checked for one hero; past it the lines are checked again as they come.</summary>
    private const int MaxLines = 20_000;

    // Each hero's lines as checked: an edit changes one line, so the check of a long list reads the others from here.
    private static readonly ConditionalWeakTable<AplVocabulary, ConcurrentDictionary<string, AplLineCheck>> Lines = new();

    /// <summary>
    /// Tokens of every line of <paramref name="text"/>: a comment, a list prefix, an action's name, each argument's key and
    /// value. An <c>if</c>, <c>target_if</c> or <c>value</c> expression has the tokens <see cref="AplVocabulary.Check"/>
    /// gives it. Each problem and the error name their line.
    /// </summary>
    public static AplExpressionCheck Check(AplVocabulary v, string text)
    {
        var checkedLines = Lines.GetValue(v, _ => new ConcurrentDictionary<string, AplLineCheck>(StringComparer.Ordinal));
        if (checkedLines.Count > MaxLines) checkedLines.Clear();

        var tokens = new List<AplTokenSpan>();
        string? error = null;
        int? errorStart = null;
        var lists = new HashSet<string>();
        var listNames = new List<(int Index, string Name, int Line)>();

        int lineStart = 0;
        string[] lines = text.Split('\n');
        for (int n = 0; n < lines.Length; lineStart += lines[n].Length + 1, n++)
        {
            string line = lines[n].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            int lineNumber = n + 1;
            var lineCheck = checkedLines.GetOrAdd(line, l => CheckLine(v, l));

            if (lineCheck.List != null) lists.Add(lineCheck.List);
            foreach (var (token, name) in lineCheck.ListNames) listNames.Add((tokens.Count + token, name, lineNumber));
            foreach (var token in lineCheck.Tokens)
            {
                tokens.Add(token with { Start = lineStart + token.Start, Problem = token.Problem is { } p ? $"Line {lineNumber}: {p}" : null });
            }
            if (error == null && lineCheck.Error != null)
            {
                error = $"Line {lineNumber}: {lineCheck.Error}";
                errorStart = lineStart + lineCheck.ErrorAt;
            }
        }

        foreach (var (index, name, line) in listNames)
        {
            if (!lists.Contains(name)) tokens[index] = tokens[index] with { Kind = AplTokenKind.Unknown, Problem = $"Line {line}: No action list named `{name}`." };
        }
        return new AplExpressionCheck(tokens, error, errorStart);
    }

    private static AplLineCheck CheckLine(AplVocabulary v, string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith('#')) return new AplLineCheck([new AplTokenSpan(line.IndexOf('#'), trimmed.Length, AplTokenKind.Comment)], null, 0, null, []);

        var tokenized = AplParser.Tokenize(line);
        if (!tokenized.HasValue) return new AplLineCheck([], tokenized.FormatErrorMessageFragment(), tokenized.ErrorPosition.Absolute, null, []);

        var tokens = new List<AplTokenSpan>();
        var listNames = new List<(int Token, string Name)>();
        var lineTokens = tokenized.Value.ToArray();
        static AplTokenSpan Span(Token<AplToken> token, AplTokenKind kind, string? problem = null) =>
            new(token.Position.Absolute, token.Span.Length, kind, problem);

        int i = 0;
        string? list = null;
        if (lineTokens.Length > 1 && lineTokens[0].Kind == AplToken.Identifier
            && AplActionLine.ListOf(lineTokens[0].ToStringValue()) is { } prefixList
            && lineTokens[1].Kind is AplToken.Equal or AplToken.PlusEqual)
        {
            list = prefixList;
            tokens.Add(Span(lineTokens[0], AplTokenKind.List));
            i = 2;
            if (i < lineTokens.Length && lineTokens[i].Kind == AplToken.Slash) i++;
        }

        string? action = null;
        if (i < lineTokens.Length && lineTokens[i].Kind == AplToken.Identifier)
        {
            action = lineTokens[i].ToStringValue();
            tokens.Add(IsControl(v, action) ? Span(lineTokens[i], AplTokenKind.Keyword)
                : v.Abilities.Any(a => a.Slug == action) ? Span(lineTokens[i], AplTokenKind.Cooldown)
                : Span(lineTokens[i], AplTokenKind.Unknown, $"{v.Hero} has no ability named `{action}`."));
            i++;
        }

        while (i < lineTokens.Length)
        {
            if (lineTokens[i].Kind != AplToken.Comma || i + 2 >= lineTokens.Length
                || lineTokens[i + 1].Kind != AplToken.Identifier || lineTokens[i + 2].Kind != AplToken.Equal)
            {
                i++;
                continue;
            }
            string key = lineTokens[i + 1].ToStringValue().ToLowerInvariant();
            tokens.Add(Span(lineTokens[i + 1], AplTokenKind.Keyword));
            int start = i + 3, end = start;
            for (int depth = 0; end < lineTokens.Length && (depth > 0 || lineTokens[end].Kind != AplToken.Comma); end++)
            {
                if (lineTokens[end].Kind == AplToken.LParen) depth++;
                else if (lineTokens[end].Kind == AplToken.RParen) depth--;
            }
            i = end;
            if (start >= end) continue;

            var value = lineTokens[start..end];
            if (key == AplActionLine.ListNameKey && action != null && AplListActions.Parse(action) != null)
            {
                listNames.Add((tokens.Count, value[0].ToStringValue()));
                tokens.Add(Span(value[0], AplTokenKind.List));
            }
            else if (key == AplVariable.NameKey && action == AplVariable.ActionName)
            {
                tokens.Add(Span(value[0], AplTokenKind.Named));
            }
            else if (key == AplVariable.OpKey && action == AplVariable.ActionName)
            {
                tokens.Add(Span(value[0], AplTokenKind.Keyword));
            }
            else
            {
                // A target selection ("min:") comes before a target_if's expression.
                if (key == AplActionLine.TargetIfKey && value.Length > 1 && value[1].Kind == AplToken.Colon)
                {
                    tokens.Add(Span(value[0], AplTokenKind.Keyword));
                    value = value[2..];
                }
                tokens.AddRange(v.Classified(value));
            }
        }

        try
        {
            AplParser.ParseActionLine(line);
            return new AplLineCheck(tokens, null, 0, list, listNames);
        }
        catch (ParseException e)
        {
            int at = e.ErrorPosition.HasValue ? e.ErrorPosition.Absolute : line.Length - line.TrimStart().Length;
            return new AplLineCheck(tokens, e.Message, at, list, listNames);
        }
    }

    /// <summary>
    /// An action that controls the list instead of casting: run_action_list, call_action_list, variable, and an action the
    /// engine skips (snapshot_stats).
    /// </summary>
    private static bool IsControl(AplVocabulary v, string action) =>
        v.ListActions.Contains(action) || action == AplVariable.ActionName || AplParser.SkippedActions.Contains(action);
}
