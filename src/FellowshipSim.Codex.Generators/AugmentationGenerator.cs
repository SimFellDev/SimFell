using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fellowship.SDK;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FellowshipSim.Codex.Generators;

/// <summary>
/// Writes the game data's half of each augmentation class: for each <c>partial</c> class deriving from
/// <c>FellowshipSim.Core.Customization.Augmentations.Augmentation</c> in a namespace inside
/// <c>FellowshipSim.Core.Customization</c>, a <c>partial</c> of the same class with the values of its augmentation record.
/// The record is the aspect, sigil or protective enchant (the class's base: <c>Aspect</c>, <c>Sigil</c> or
/// <c>ProtectiveEnchant</c>) whose name in Pascal case (letters and digits only, an apostrophe inside its word) is the class
/// name. The constants are <c>&lt;Type&gt;Id</c>, <c>&lt;Type&gt;Name</c>, one per scalar field of the record under the type
/// and the field's name (<c>SigilMaxRank</c>, <c>ProtectiveEnchantDuration</c>, <c>AspectProcsPerMinute</c>...), and one per
/// tuning value of the record under the tuning value's name. Each rank run of the record is a
/// <c>public static readonly double[]</c> of its values by rank, named after its key without the <c>Trait.&lt;Row&gt;.</c>
/// prefix, its segments joined, and <c>ByRank</c>. The class's own part has the augmentation's behaviour and its
/// <c>Name</c> and <c>Id</c> overrides. A constant whose name the class or a base class already has is left out. The game
/// data is the AdditionalFiles <c>entities.jsonl</c> and <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK;
/// <see cref="CreatureGenerator"/> reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AugmentationGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string CustomizationNamespace = "FellowshipSim.Core.Customization.";
    private const string AugmentationType = "FellowshipSim.Core.Customization.Augmentations.Augmentation";

    /// <summary>Each augmentation base class and the record type of its classes, as the constants' prefix.</summary>
    private static readonly (string Base, string Type)[] Kinds =
    [
        ("FellowshipSim.Core.Customization.Aspects.Aspect", "Aspect"),
        ("FellowshipSim.Core.Customization.Sigils.Sigil", "Sigil"),
        ("FellowshipSim.Core.Customization.ProtectiveEnchants.ProtectiveEnchant", "ProtectiveEnchant"),
    ];

    private static readonly DiagnosticDescriptor NoRecord = new(
        "CODEX014", "A partial augmentation class names no augmentation record",
        "The partial class {0} names no {1} record in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralRecords = new(
        "CODEX015", "A partial augmentation class names several augmentation records",
        "The partial class {0} names the {1} records {2} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SharedName = new(
        "CODEX016", "Two values of an augmentation share a name",
        "Augmentation {0} has values {1} with different values under the name {2}; the class has no member {2}", "Codex", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX017", "An augmentation constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, so the member {1} of augmentation {2} is left out", "Codex", DiagnosticSeverity.Warning, true);

    /// <summary>
    /// A member of an augmentation record: its name, C# type and value (a literal, or an array's elements), its summary, and
    /// whether it is a per-rank array. A member with no type has several values under its name, and its summary names them.
    /// </summary>
    private sealed record Member(string Name, string Type, string Literal, string Summary, bool ByRank = false);

    /// <summary>An augmentation record: its type, ID, name, class name, its field constants, tuning values and rank arrays.</summary>
    private sealed record Record(string Type, int Id, string Name, string ClassName,
        EquatableArray<Member> Fields, EquatableArray<Member> Tunings, EquatableArray<Member> Ranks);

    /// <summary>The game data's augmentation records, and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, string? BuildDate);

    /// <summary>
    /// A partial augmentation class: its namespace, name, record type (empty for a class of no listed base), the member
    /// names of the class and its base classes, and where it is declared.
    /// </summary>
    private sealed record Declared(string Namespace, string Name, string Type, EquatableArray<string> Members, Location? Location)
    {
        public bool Equals(Declared? other) => other != null && Namespace == other.Namespace && Name == other.Name && Type == other.Type
            && Members.Equals(other.Members);

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
                    && NamespaceOf(c) is { } ns && ns.StartsWith(CustomizationNamespace, StringComparison.Ordinal),
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

    /// <summary>The partial class as a <see cref="Declared"/>, or null for a class not deriving from <c>Augmentation</c>.</summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        bool augmentation = false;
        string kind = "";
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        for (var t = type; t != null; t = t.BaseType)
        {
            string name = t.ToDisplayString();
            if (name == AugmentationType) augmentation = true;
            foreach (var (baseName, recordType) in Kinds)
            {
                if (name == baseName && kind.Length == 0) kind = recordType;
            }
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
        if (!augmentation) return null;
        return new Declared(NamespaceOf(node)!, type.Name, kind, new([.. members.OrderBy(m => m, StringComparer.Ordinal)]), node.Identifier.GetLocation());
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
                // Only aspect, sigil and protective enchant lines are read in full.
                if (!text.Contains("\"$type\":\"aspect\"") && !text.Contains("\"$type\":\"sigil\"") && !text.Contains("\"$type\":\"protective-enchant\"")) continue;
                AugmentationDocument? document;
                try
                {
                    document = JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options) as AugmentationDocument;
                }
                catch (JsonException)
                {
                    continue;
                }
                string? type = document switch
                {
                    AspectDocument => "Aspect",
                    SigilDocument => "Sigil",
                    ProtectiveEnchantDocument => "ProtectiveEnchant",
                    _ => null,
                };
                string name = (document?.Name ?? "").Trim();
                if (document == null || type == null || name.Length == 0) continue;
                records.Add(new Record(type, document.Id, name, PascalCase(name),
                    new([.. FieldsOf(document, type)]), new([.. TuningsOf(document, type)]), new([.. RanksOf(document, type)])));
            }
        }
        return new Export(new([.. records.OrderBy(r => r.Type, StringComparer.Ordinal).ThenBy(r => r.Id)]), buildDate);
    }

    /// <summary>The record's type as lower case words: <c>ProtectiveEnchant</c> is <c>protective enchant</c>.</summary>
    private static string Words(string pascal)
    {
        var result = new StringBuilder();
        foreach (char c in pascal)
        {
            if (char.IsUpper(c) && result.Length > 0) result.Append(' ');
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }

    /// <summary>
    /// The record's scalar fields, each under the record type and the field's name. A record with one proc roll has its
    /// procs per minute as <c>&lt;Type&gt;ProcsPerMinute</c>, except where a rank run has the proc roll's row: that rank run
    /// has the procs per minute of every rank.
    /// </summary>
    private static List<Member> FieldsOf(AugmentationDocument a, string type)
    {
        string owner = $"{Words(type)} {a.Id}";
        var fields = new List<Member>();
        AddNumber(fields, type + "MaxRank", a.MaxRank, $"Game data: <c>maxRank</c> of {owner}.");
        AddNumber(fields, type + "RandomChance", a.RandomChance, $"Game data: <c>randomChance</c> of {owner}.");
        AddNumber(fields, type + "Cooldown", a.Cooldown, $"Game data: <c>cooldown</c> of {owner}.");
        AddNumber(fields, type + "Duration", a.Duration, $"Game data: <c>duration</c> of {owner}.");
        if (a.ProcRolls is [{ ProcsPerMinute: { } ppm } roll] && !(a.RankRuns ?? []).Any(r => r.Key == roll.Row))
        {
            string formula = roll.Formula is { Length: > 0 } f ? $" ({Escape(f)})" : "";
            AddNumber(fields, type + "ProcsPerMinute", ppm, $"Game data: <c>procsPerMinute</c> of proc roll <c>{Escape(roll.Row ?? "")}</c> of {owner}{formula}.");
        }
        return fields;
    }

    private static void AddNumber(List<Member> fields, string name, double? value, string summary)
    {
        if (value is not { } v) return;
        bool whole = v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;
        fields.Add(new Member(name, whole ? "int" : "double", whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : Number(v), summary));
    }

    /// <summary>The record's tuning values, one per name, in the order of the game data.</summary>
    private static List<Member> TuningsOf(AugmentationDocument a, string type)
    {
        var tunings = new List<Member>();
        foreach (var group in (a.Tuning ?? new Dictionary<string, TuningReference>()).GroupBy(t => t.Value.Name ?? ""))
        {
            if (group.Key.Length == 0) continue;
            var values = group.Select(t => ValueOf(t.Value.Value)).Distinct().ToList();
            string keys = string.Join(", ", group.Select(t => t.Key).OrderBy(k => k.Length).ThenBy(k => k, StringComparer.Ordinal));
            if (values.Count != 1 || values[0] is not { } value)
            {
                // Two values under one name: no constant, and the diagnostic names the keys.
                tunings.Add(new Member(group.Key, "", "", keys));
                continue;
            }
            string source = SourceOf(group.First().Value);
            tunings.Add(new Member(group.Key, value.Type, value.Literal,
                $"Game data: tuning <c>{Escape(keys)}</c> of {Words(type)} {a.Id}{(source.Length > 0 ? "; " + Escape(source) : "")}."));
        }
        return tunings;
    }

    /// <summary>
    /// The record's rank runs, each an array of its values by rank (rank 1 first). The summary names the run's key, its
    /// transform and the tooltip's numbers: a <c>delta</c> run's values are multipliers (the tooltip has the value less 1),
    /// and a <c>ticks</c> run's values are per tick (the tooltip has the value times the ticks).
    /// </summary>
    private static List<Member> RanksOf(AugmentationDocument a, string type)
    {
        var ranks = new List<Member>();
        foreach (var group in (a.RankRuns ?? []).Where(r => r.Key is { Length: > 0 }).GroupBy(r => RankName(r.Key!)))
        {
            var runs = group.ToList();
            string keys = string.Join(", ", runs.Select(r => r.Key));
            var values = runs.Select(r => r.Values is { Count: > 0 } v && v.All(x => x != null) ? string.Join(", ", v.Select(x => Number(x!.Value))) : null)
                .Distinct().ToList();
            if (values.Count != 1 || values[0] is not { } literal)
            {
                // Two runs under one name, or a run with a rank of no value: no array, and the diagnostic names the keys.
                ranks.Add(new Member(group.Key, "", "", keys, ByRank: true));
                continue;
            }
            var run = runs[0];
            string transform = run.Transform is { Length: > 0 } t
                ? $", transform <c>{Escape(t)}</c>{(run.Ticks is { } ticks ? $" over {ticks} ticks" : "")}"
                : "";
            string printed = run.Printed is { Count: > 0 } p ? $"; the tooltip has {Escape(string.Join(" / ", p.Select(Untagged)))}" : "";
            ranks.Add(new Member(group.Key, "double[]", literal,
                $"Game data: rank run <c>{Escape(keys)}</c> of {Words(type)} {a.Id}{transform}{printed}.", ByRank: true));
        }
        return ranks;
    }

    /// <summary>A rank run's array name: its key without <c>Trait.&lt;Row&gt;.</c>, its segments joined, and <c>ByRank</c>.</summary>
    private static string RankName(string key)
    {
        var segments = key.Split('.');
        int skip = segments.Length > 2 && segments[0] == "Trait" ? 2 : 0;
        return string.Concat(segments.Skip(skip).Select(PascalCase)) + "ByRank";
    }

    /// <summary>A tooltip number without its markup tags.</summary>
    private static string Untagged(string printed) => Regex.Replace(printed, "<[^>]*>", "").Trim();

    /// <summary>The C# type and literal of a tuning value; null for a value of no constant type.</summary>
    private static (string Type, string Literal)? ValueOf(object? value)
    {
        if (value is not JsonElement element) return null;
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
            string type = Words(owner.Type.ToString());
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
            var candidates = byClass[d.Name].Where(r => d.Type.Length == 0 || r.Type == d.Type).ToList();
            string kind = d.Type.Length > 0 ? Words(d.Type) : "aspect, sigil or protective enchant";
            if (candidates.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoRecord, d.Location, d.Name, kind));
                continue;
            }
            if (candidates.Count != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(SeveralRecords, d.Location, d.Name, kind,
                    string.Join(", ", candidates.Select(r => $"{Words(r.Type)} {r.Id}"))));
                continue;
            }

            var record = candidates[0];
            string owner = $"{Words(record.Type)} {record.Id}";
            var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
            var all = new List<Member>
            {
                new(record.Type + "Id", "int", record.Id.ToString(CultureInfo.InvariantCulture), $"{Escape(record.Name)}, {owner} of the game data."),
                new(record.Type + "Name", "string", Literal(record.Name), $"Game data: <c>name</c> of {owner}."),
            };
            all.AddRange(record.Fields);
            var names = new HashSet<string>(all.Select(c => c.Name), StringComparer.Ordinal);
            foreach (var member in record.Tunings.Concat(record.Ranks))
            {
                if (member.Type.Length == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(SharedName, d.Location, owner, member.Summary, member.Name));
                    continue;
                }
                if (!names.Add(member.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, member.Name, owner));
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
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, member.Name, owner));
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
            context.AddSource($"{d.Namespace.Substring(CustomizationNamespace.Length)}.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
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
