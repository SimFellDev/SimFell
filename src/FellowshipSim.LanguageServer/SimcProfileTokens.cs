using System.Globalization;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The tokens of a .simc file's profile lines, in the action list's kinds. A hero line's key and a pull's are
/// <see cref="AplTokenKind.List"/>, and every other key, an option's key, <c>boss.N</c>, <c>true</c> and <c>false</c> are
/// <see cref="AplTokenKind.Keyword"/>. A value's number is <see cref="AplTokenKind.Number"/>, its name or quoted text
/// <see cref="AplTokenKind.Named"/>, and <c>=</c>, <c>/</c>, <c>,</c>, <c>:</c> and <c>%</c> are <see cref="AplTokenKind.Operator"/>.
/// </summary>
public static class SimcProfileTokens
{
    private const string Punctuation = "=/,:%";
    private const string SourceKey = "source";

    /// <summary>Every profile line's tokens, and with <paramref name="comments"/> the comment lines' tokens.</summary>
    public static List<AplTokenSpan> Of(SimcFile file, string text, bool comments)
    {
        var tokens = new List<AplTokenSpan>();
        string[] lines = text.Split('\n');
        for (int n = 0, lineStart = 0; n < lines.Length; lineStart += lines[n].Length + 1, n++)
        {
            string line = lines[n].TrimEnd('\r');
            int start = line.Length - line.TrimStart().Length;
            if (comments && start < line.Length && line[start] == '#') tokens.Add(new AplTokenSpan(lineStart + start, line.Length - start, AplTokenKind.Comment));
            if (file.ProfileLines.Contains(n)) AddLine(tokens, line, lineStart, start);
        }
        return tokens;
    }

    private static void AddLine(List<AplTokenSpan> tokens, string line, int lineStart, int start)
    {
        void Add(int from, int to, AplTokenKind kind) => tokens.Add(new AplTokenSpan(lineStart + from, to - from, kind));

        int eq = line.IndexOf('=');
        if (eq <= start) return;
        string key = line[start..eq].TrimEnd().TrimEnd('+').TrimEnd();
        int keyEnd = start + key.Length;
        Add(start, keyEnd, SimcProfile.HeroOf(key) != null || key.StartsWith(DungeonProfile.PullPrefix, StringComparison.Ordinal) ? AplTokenKind.List : AplTokenKind.Keyword);
        Add(keyEnd, eq + 1, AplTokenKind.Operator);
        if (key == SourceKey)
        {
            int end = line.TrimEnd().Length;
            if (end > eq + 1) Add(eq + 1 + (line.Length - eq - 1 - line[(eq + 1)..].TrimStart().Length), end, AplTokenKind.Named);
            return;
        }

        for (int i = eq + 1; i < line.Length;)
        {
            char c = line[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '"')
            {
                int close = line.IndexOf('"', i + 1);
                int end = close < 0 ? line.Length : close + 1;
                Add(i, end, AplTokenKind.Named);
                i = end;
            }
            else if (Punctuation.Contains(c))
            {
                Add(i, i + 1, AplTokenKind.Operator);
                i++;
            }
            else
            {
                int end = i;
                while (end < line.Length && !char.IsWhiteSpace(line[end]) && line[end] != '"' && !Punctuation.Contains(line[end])) end++;
                Add(i, end, KindOf(line[i..end], end < line.Length && line[end] == '='));
                i = end;
            }
        }
    }

    private static AplTokenKind KindOf(string word, bool beforeEquals) =>
        beforeEquals || word is "true" or "false" || word.StartsWith(DungeonProfile.BossPrefix, StringComparison.Ordinal) ? AplTokenKind.Keyword
        : double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? AplTokenKind.Number
        : AplTokenKind.Named;
}
