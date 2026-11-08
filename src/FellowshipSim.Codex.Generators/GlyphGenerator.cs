using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Fellowship.SDK;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FellowshipSim.Codex.Generators;

/// <summary>
/// Writes the game data's half of each glyph class: for each <c>partial</c> class deriving from
/// <c>FellowshipSim.Core.Customization.Glyphs.Glyph</c>, a <c>partial</c> of the same class with the values of its glyph
/// record. The record is the glyph whose name in Pascal case (letters and digits only, an apostrophe inside its word) is
/// the class name. The constants are <c>GlyphId</c>, <c>GlyphName</c>, one per scalar field of the record under
/// <c>Glyph</c> and the field's name (<c>GlyphMaxRank</c>, <c>GlyphAbilityRank</c>), and one per tuning value of the
/// record under the tuning value's name. A tuning value has a value per rank: one value at every rank is a constant, and
/// values differing by rank are a <c>public static readonly double[]</c> named after the tuning value and <c>ByRank</c>.
/// The class's own part has the glyph's behaviour and its <c>Name</c> and <c>Id</c> overrides. A constant whose name the
/// class or a base class already has is left out. The game data is the AdditionalFiles <c>entities.jsonl</c> and
/// <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK; <see cref="CreatureGenerator"/> reports missing game
/// data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class GlyphGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string GlyphType = "FellowshipSim.Core.Customization.Glyphs.Glyph";
    private const string GlyphNamespace = "FellowshipSim.Core.Customization.Glyphs";

    private static readonly DiagnosticDescriptor NoRecord = new(
        "CODEX018", "A partial glyph class names no glyph record",
        "The partial class {0} names no glyph record in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralRecords = new(
        "CODEX019", "A partial glyph class names several glyph records",
        "The partial class {0} names the glyph records {1} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SharedName = new(
        "CODEX020", "Two tuning values of a glyph share a name",
        "Glyph {0} has tuning values {1} with different values under the name {2}; the class has no member {2}", "Codex", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX021", "A glyph constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, so the member {1} of glyph {2} is left out", "Codex", DiagnosticSeverity.Warning, true);

    /// <summary>
    /// A member of a glyph record: its name, C# type and value (a literal, or an array's elements), its summary, and whether
    /// it is a per-rank array. A member with no type has several values under its name, and its summary names them.
    /// </summary>
    private sealed record Member(string Name, string Type, string Literal, string Summary, bool ByRank = false);

    /// <summary>A glyph record: its ID, name, class name, its field constants and its tuning values.</summary>
    private sealed record Record(int Id, string Name, string ClassName, EquatableArray<Member> Fields, EquatableArray<Member> Tunings);

    /// <summary>The game data's glyph records, and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, string? BuildDate);

    /// <summary>A partial glyph class: its namespace, name, the member names of the class and its base classes, and where it is declared.</summary>
    private sealed record Declared(string Namespace, string Name, EquatableArray<string> Members, Location? Location)
    {
        public bool Equals(Declared? other) => other != null && Namespace == other.Namespace && Name == other.Name && Members.Equals(other.Members);

        public override int GetHashCode() => (Namespace, Name).GetHashCode();
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var export = context.AdditionalTextsProvider
            .Where(f => Path.GetFileName(f.Path) is EntitiesName or SettingsName)
            .Collect()
            .Select((all, ct) => Read(all, ct));
        var declared = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax c && c.Modifiers.Any(SyntaxKind.PartialKeyword) && c.Parent is BaseNamespaceDeclarationSyntax
                    && NamespaceOf(c) is { } ns && ns.StartsWith(GlyphNamespace, StringComparison.Ordinal),
                static (ctx, ct) => DeclaredOf(ctx, ct))
            .Where(static d => d != null)
            .Collect();
        context.RegisterSourceOutput(export.Combine(declared), (spc, pair) => Write(spc, pair.Left, pair.Right!));
    }

    private static string? NamespaceOf(SyntaxNode node)
    {
        for (var parent = node.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is BaseNamespaceDeclarationSyntax ns) return ns.Name.ToString();
        }
        return null;
    }

    /// <summary>The partial class as a <see cref="Declared"/>, or null for a class not deriving from <c>Glyph</c>.</summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        bool glyph = false;
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t != type && t.ToDisplayString() == GlyphType) glyph = true;
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
        if (!glyph) return null;
        return new Declared(NamespaceOf(node)!, type.Name, new([.. members.OrderBy(m => m, StringComparer.Ordinal)]), node.Identifier.GetLocation());
    }

    private static Export Read(ImmutableArray<AdditionalText> files, CancellationToken ct)
    {
        var entities = files.FirstOrDefault(f => Path.GetFileName(f.Path) == EntitiesName)?.GetText(ct);
        var settings = files.FirstOrDefault(f => Path.GetFileName(f.Path) == SettingsName)?.GetText(ct);

        string? buildDate = null;
        if (settings != null)
        {
            var file = JsonSerializer.Deserialize<SettingsFile>(settings.ToString(), EntityDocuments.Options);
            buildDate = file?.BuildDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var records = new List<Record>();
        if (entities != null)
        {
            foreach (var line in entities.Lines)
            {
                ct.ThrowIfCancellationRequested();
                string text = line.ToString();
                // Only glyph lines are read in full.
                if (!text.Contains("\"$type\":\"glyph\"")) continue;
                GlyphDocument? document;
                try
                {
                    document = JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options) as GlyphDocument;
                }
                catch (JsonException)
                {
                    continue;
                }
                string name = (document?.Name ?? "").Trim();
                if (document == null || name.Length == 0) continue;
                records.Add(new Record(document.Id, name, PascalCase(name), new([.. FieldsOf(document)]), new([.. TuningsOf(document)])));
            }
        }
        return new Export(new([.. records.OrderBy(r => r.Id)]), buildDate);
    }

    /// <summary>The record's scalar fields, each under <c>Glyph</c> and the field's name.</summary>
    private static List<Member> FieldsOf(GlyphDocument g)
    {
        var fields = new List<Member>();
        if (g.MaxRank is { } maxRank)
        {
            fields.Add(new Member("GlyphMaxRank", "int", maxRank.ToString(CultureInfo.InvariantCulture), $"Game data: <c>maxRank</c> of glyph {g.Id}."));
        }
        if (g.AbilityRank is { } abilityRank)
        {
            fields.Add(new Member("GlyphAbilityRank", "int", abilityRank.ToString(CultureInfo.InvariantCulture), $"Game data: <c>abilityRank</c> of glyph {g.Id}."));
        }
        return fields;
    }

    /// <summary>
    /// The record's tuning values, one per name, in the order of the game data. A value the same at every rank is a
    /// constant; values differing by rank are an array by rank (rank 1 first), named after the tuning value and
    /// <c>ByRank</c>.
    /// </summary>
    private static List<Member> TuningsOf(GlyphDocument g)
    {
        var tunings = new List<Member>();
        foreach (var group in (g.Tuning ?? new Dictionary<string, TuningReference>()).GroupBy(t => t.Value.Name ?? ""))
        {
            if (group.Key.Length == 0) continue;
            var values = group.Select(t => ValueOf(t.Value.Value)).Distinct().ToList();
            string keys = string.Join(", ", group.Select(t => t.Key).OrderBy(k => k.Length).ThenBy(k => k, StringComparer.Ordinal));
            if (values.Count != 1 || values[0] is not { } value)
            {
                // Two values under one name: no member, and the diagnostic names the keys.
                tunings.Add(new Member(group.Key, "", "", keys));
                continue;
            }
            string source = SourceOf(group.First().Value);
            string summary = $"Game data: tuning <c>{Escape(keys)}</c> of glyph {g.Id}{(source.Length > 0 ? "; " + Escape(source) : "")}";
            if (value.ByRank)
            {
                tunings.Add(new Member(group.Key + "ByRank", "double[]", value.Literal, summary + ", by rank.", ByRank: true));
            }
            else
            {
                tunings.Add(new Member(group.Key, value.Type, value.Literal, summary + "."));
            }
        }
        return tunings;
    }

    /// <summary>
    /// The C# type and literal of a tuning value, and whether it is an array by rank; null for a value of no constant type.
    /// An array with one value at every rank is that value.
    /// </summary>
    private static (string Type, string Literal, bool ByRank)? ValueOf(object? value)
    {
        if (value is not JsonElement element) return null;
        if (element.ValueKind != JsonValueKind.Array) return ScalarOf(element) is { } scalar ? (scalar.Type, scalar.Literal, false) : null;
        var ranks = element.EnumerateArray().ToList();
        if (ranks.Count == 0 || ranks.Any(r => r.ValueKind != JsonValueKind.Number)) return null;
        var numbers = ranks.Select(r => r.GetDouble()).ToList();
        if (numbers.Distinct().Count() > 1) return ("double[]", string.Join(", ", numbers.Select(Number)), true);
        bool whole = ranks.All(r => r.TryGetInt32(out _) && r.GetRawText().IndexOfAny(['.', 'e', 'E']) < 0);
        return whole ? ("int", ((int)numbers[0]).ToString(CultureInfo.InvariantCulture), false) : ("double", Number(numbers[0]), false);
    }

    /// <summary>The C# type and literal of a scalar tuning value; null for a value of no constant type.</summary>
    private static (string Type, string Literal)? ScalarOf(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                string raw = element.GetRawText();
                if (element.TryGetInt32(out int whole) && raw.IndexOfAny(['.', 'e', 'E']) < 0) return ("int", whole.ToString(CultureInfo.InvariantCulture));
                return ("double", Number(element.GetDouble()));
            case JsonValueKind.True:
            case JsonValueKind.False:
                return ("bool", element.ValueKind == JsonValueKind.True ? "true" : "false");
            case JsonValueKind.String:
                return ("string", Literal(element.GetString() ?? ""));
            default:
                return null;
        }
    }

    /// <summary>The tuning value's owner, and its attribute and operation where it has them.</summary>
    private static string SourceOf(TuningReference tuning)
    {
        var parts = new List<string>();
        if (tuning.Owner is { } owner)
        {
            string type = owner.Type.ToString().ToLowerInvariant();
            parts.Add(owner.Name is { Length: > 0 } ownerName ? $"{ownerName} ({type} {owner.Id})" : $"{type} {owner.Id}");
        }
        if (tuning.Attribute is { Length: > 0 } attribute) parts.Add(tuning.Op is { Length: > 0 } op ? $"{attribute} {op}" : attribute);
        return string.Join(", ", parts);
    }

    private static void Write(SourceProductionContext context, Export export, ImmutableArray<Declared> declared)
    {
        var byClass = export.Records.ToLookup(r => r.ClassName);
        foreach (var d in declared.GroupBy(d => (d.Namespace, d.Name)).Select(g => g.First())
                     .OrderBy(d => d.Namespace, StringComparer.Ordinal).ThenBy(d => d.Name, StringComparer.Ordinal))
        {
            var candidates = byClass[d.Name].ToList();
            if (candidates.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoRecord, d.Location, d.Name));
                continue;
            }
            if (candidates.Count != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(SeveralRecords, d.Location, d.Name, string.Join(", ", candidates.Select(r => r.Id))));
                continue;
            }

            var record = candidates[0];
            var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
            var all = new List<Member>
            {
                new("GlyphId", "int", record.Id.ToString(CultureInfo.InvariantCulture), $"{Escape(record.Name)}, glyph {record.Id} of the game data."),
                new("GlyphName", "string", Literal(record.Name), $"Game data: <c>name</c> of glyph {record.Id}."),
            };
            all.AddRange(record.Fields);
            var names = new HashSet<string>(all.Select(c => c.Name), StringComparer.Ordinal);
            foreach (var member in record.Tunings)
            {
                if (member.Type.Length == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(SharedName, d.Location, record.Id, member.Summary, member.Name));
                    continue;
                }
                if (!names.Add(member.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, member.Name, record.Id));
                    continue;
                }
                all.Add(member);
            }

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(d.Namespace).Append(";\n\n")
                .Append("partial class ").Append(d.Name).Append("\n{\n");
            bool first = true;
            foreach (var member in all)
            {
                if (members.Contains(member.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, member.Name, record.Id));
                    continue;
                }
                if (!first) source.Append('\n');
                first = false;
                source.Append("    /// <summary>").Append(member.Summary).Append("</summary>\n");
                if (member.ByRank)
                {
                    source.Append("    public static readonly double[] ").Append(member.Name).Append(" = [").Append(member.Literal).Append("];\n");
                }
                else
                {
                    source.Append("    public const ").Append(member.Type).Append(' ').Append(member.Name).Append(" = ").Append(member.Literal).Append(";\n");
                }
            }
            source.Append("}\n");
            context.AddSource($"Glyphs.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }
    }

    /// <summary>The name's words, each starting upper case, with letters and digits only.</summary>
    private static string PascalCase(string name)
    {
        var result = new StringBuilder();
        bool start = true;
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                result.Append(start ? char.ToUpperInvariant(c) : c);
                start = false;
            }
            // An apostrophe stays inside its word: Hunter's is Hunters.
            else if (c != '\'') start = true;
        }
        return result.ToString();
    }

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
