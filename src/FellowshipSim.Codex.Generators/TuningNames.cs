using System.Text;
using Fellowship.SDK.Models;
using Microsoft.CodeAnalysis;

namespace FellowshipSim.Codex.Generators;

/// <summary>
/// The names of a record's tuning values in its class's nested <c>public static class Constants</c>. A tuning value's name
/// is its tuning key's segments joined, each in Pascal case (<c>EnergyGainProc.AgilityCoefficient</c> is
/// <c>EnergyGainProcAgilityCoefficient</c>), after the hero rule: a first segment naming the record's own hero is dropped,
/// and a first segment naming another hero is that hero's name (<see cref="HeroSegments"/>). A value by rank has
/// <c>ByRank</c> after its name. Keys of a record with one value are one constant when the segments of one end with the
/// segments of the other (<see cref="Groups"/>), named after the shortest key (by length, then ordinal). Two keys with one name and different values are CODEX038, and a hand-written
/// constant or static readonly field of the class with the name of a member of its <c>Constants</c> is CODEX039.
/// </summary>
internal static class TuningNames
{
    public static readonly DiagnosticDescriptor SharedName = new(
        "CODEX038", "Two tuning keys of a record have one constant name",
        "The tuning keys {1} and {2} of {0} have different values and the one constant name {3}", "Codex", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor DeclaredConstant = new(
        "CODEX039", "A hand-written constant has the name of a tuning constant",
        "The class {0} has a constant or static readonly field {1}, and Constants.{1} is a tuning value of {2}; delete or rename the field",
        "Codex", DiagnosticSeverity.Error, true);

    /// <summary>
    /// The record's tuning keys with a value of a constant type (<paramref name="valueOf"/> is not null), in the order of
    /// the game data.
    /// </summary>
    public static List<TuningEntry> Entries(IEnumerable<KeyValuePair<string, TuningReference>>? tuning,
        Func<object?, (string Type, string Literal, bool ByRank)?> valueOf, Func<TuningReference, string> sourceOf)
    {
        var entries = new List<TuningEntry>();
        foreach (var t in tuning ?? [])
        {
            if (t.Key is not { Length: > 0 } key || valueOf(t.Value.Value) is not { } value) continue;
            entries.Add(new TuningEntry(key, key, value.Type, value.Literal, value.ByRank, sourceOf(t.Value)));
        }
        return entries;
    }

    /// <summary>The constant name of a tuning key, after the hero rule for a record of the heroes <paramref name="ownHeroes"/>.</summary>
    public static string Name(string key, IEnumerable<HeroSegment> segments, IReadOnlyCollection<string> ownHeroes) =>
        string.Concat(Segments(key, segments, ownHeroes).Select(PascalCase));

    /// <summary>The segments of a tuning key after the hero rule.</summary>
    private static List<string> Segments(string key, IEnumerable<HeroSegment> segments, IReadOnlyCollection<string> ownHeroes)
    {
        var parts = key.Split('.').ToList();
        if (parts.Count > 1 && segments.FirstOrDefault(s => s.Segment == parts[0]) is { } hero)
        {
            if (ownHeroes.Contains(hero.Hero)) parts.RemoveAt(0);
            else parts[0] = hero.Hero;
        }
        return parts;
    }

    /// <summary>
    /// The keys of one value and last segment, in groups of one constant. Two keys are in one group when the segments of one,
    /// after the hero rule, end with the segments of the other (<c>Duration</c> and <c>Talent.Buff.Duration</c>). A key whose
    /// segments end two keys of which neither ends the other is in no group with them: <c>Duration</c> beside
    /// <c>Buff.Duration</c> and <c>Debuff.Duration</c> is three constants.
    /// </summary>
    private static List<List<(TuningEntry Entry, int Order)>> Groups(List<(TuningEntry Entry, int Order)> keys,
        IEnumerable<HeroSegment> segments, IReadOnlyCollection<string> ownHeroes)
    {
        var parts = keys.Select(k => Segments(k.Entry.NameKey, segments, ownHeroes)).ToList();
        bool Ends(int shorter, int longer) => parts[shorter].Count <= parts[longer].Count
            && parts[shorter].SequenceEqual(parts[longer].Skip(parts[longer].Count - parts[shorter].Count), StringComparer.Ordinal);

        var group = Enumerable.Range(0, keys.Count).ToArray();
        int Find(int i) => group[i] == i ? i : group[i] = Find(group[i]);
        for (int s = 0; s < keys.Count; s++)
        {
            var longer = Enumerable.Range(0, keys.Count).Where(l => l != s && Ends(s, l)).ToList();
            if (longer.Count == 0) continue;
            // The key and the keys ending with it, shortest first: one group when each ends the next.
            var chain = longer.Append(s).OrderBy(i => parts[i].Count).ToList();
            if (!chain.Zip(chain.Skip(1), (a, b) => Ends(a, b)).All(e => e)) continue;
            foreach (int l in longer) group[Find(l)] = Find(s);
        }
        return Enumerable.Range(0, keys.Count).GroupBy(Find).Select(g => g.Select(i => keys[i]).ToList()).ToList();
    }

    /// <summary>
    /// The record's constants, in the order of the game data, and the pairs of keys with one name and different values
    /// (<see cref="SharedName"/>). Of such a pair, the constant is the first key's.
    /// </summary>
    public static List<TuningConstant> Of(IEnumerable<TuningEntry> entries, IEnumerable<HeroSegment> segments, IReadOnlyCollection<string> ownHeroes,
        List<(string Name, string First, string Second)> shared)
    {
        var all = entries.Select((e, i) => (Entry: e, Order: i)).ToList();
        var byValue = all
            .GroupBy(e => (Last: e.Entry.NameKey.Split('.').Last(), e.Entry.Type, e.Entry.Literal, e.Entry.ByRank))
            .SelectMany(g => Groups(g.ToList(), segments, ownHeroes))
            .Select(g => g.OrderBy(e => e.Entry.NameKey.Length).ThenBy(e => e.Entry.NameKey, StringComparer.Ordinal).ToList())
            .Select(g => (Name: Name(g[0].Entry.NameKey, segments, ownHeroes) + (g[0].Entry.ByRank ? "ByRank" : ""), Entries: g, Order: g.Min(e => e.Order)))
            .OrderBy(g => g.Order)
            .ToList();
        var constants = new List<TuningConstant>();
        var named = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, group, _) in byValue)
        {
            var first = group[0].Entry;
            string keys = string.Join(", ", group.Select(e => e.Entry.Key));
            if (named.TryGetValue(name, out int at))
            {
                var other = constants[at];
                // Keys of several last segments with one value under one name are one constant.
                if (other.Type == first.Type && other.Literal == first.Literal && other.ByRank == first.ByRank)
                {
                    constants[at] = other with { Keys = string.Join(", ", other.Keys.Split([", "], StringSplitOptions.None).Concat(group.Select(e => e.Entry.Key))
                        .OrderBy(k => k.Length).ThenBy(k => k, StringComparer.Ordinal)) };
                }
                else
                {
                    shared.Add((name, other.Keys.Split([", "], StringSplitOptions.None)[0], first.Key));
                }
                continue;
            }
            named[name] = constants.Count;
            constants.Add(new TuningConstant(name, first.Type, first.Literal, first.ByRank, keys, first.Source));
        }
        return constants;
    }

    /// <summary>
    /// Reports CODEX038 for each pair of keys in <paramref name="shared"/>, and CODEX039 for each name of
    /// <paramref name="fields"/> (the class's constants and static readonly fields) in <paramref name="constants"/>.
    /// </summary>
    public static void Report(SourceProductionContext context, Location? location, string className, string owner,
        IEnumerable<(string Name, string First, string Second)> shared, IEnumerable<string> fields, IEnumerable<TuningConstant> constants)
    {
        foreach (var (name, first, second) in shared)
        {
            context.ReportDiagnostic(Diagnostic.Create(SharedName, location, owner, first, second, name));
        }
        var names = new HashSet<string>(constants.Select(c => c.Name), StringComparer.Ordinal);
        foreach (string field in fields.Where(names.Contains))
        {
            context.ReportDiagnostic(Diagnostic.Create(DeclaredConstant, location, className, field, owner));
        }
    }

    /// <summary>
    /// Appends the nested <c>Constants</c> class of <paramref name="constants"/>, each with its summary, after a blank line
    /// when <paramref name="separate"/>. A class deriving from a class with a <c>Constants</c> declares its own with
    /// <c>new</c> (<paramref name="hides"/>).
    /// </summary>
    public static void Write(StringBuilder source, string summary, IEnumerable<(TuningConstant Constant, string Summary)> constants, bool hides, bool separate)
    {
        if (separate) source.Append('\n');
        source.Append("    /// <summary>").Append(summary).Append("</summary>\n")
            .Append("    public ").Append(hides ? "new " : "").Append("static class Constants\n    {\n");
        bool first = true;
        foreach (var (constant, constantSummary) in constants)
        {
            if (!first) source.Append('\n');
            first = false;
            source.Append("        /// <summary>").Append(constantSummary).Append("</summary>\n");
            if (constant.ByRank)
            {
                source.Append("        public static readonly double[] ").Append(constant.Name).Append(" = [").Append(constant.Literal).Append("];\n");
            }
            else
            {
                source.Append("        public const ").Append(constant.Type).Append(' ').Append(constant.Name).Append(" = ").Append(constant.Literal).Append(";\n");
            }
        }
        source.Append("    }\n");
    }

    /// <summary>The class's constants and static readonly fields, the names <see cref="DeclaredConstant"/> checks.</summary>
    public static IEnumerable<string> FieldsOf(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsImplicitlyDeclared && (f.IsConst || (f.IsStatic && f.IsReadOnly)))
            .Select(f => f.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal);

    /// <summary>A key segment's words, each starting upper case, with letters and digits only.</summary>
    private static string PascalCase(string segment)
    {
        var result = new StringBuilder();
        bool start = true;
        foreach (char c in segment)
        {
            if (char.IsLetterOrDigit(c))
            {
                result.Append(start ? char.ToUpperInvariant(c) : c);
                start = false;
            }
            else start = true;
        }
        return result.ToString();
    }
}
