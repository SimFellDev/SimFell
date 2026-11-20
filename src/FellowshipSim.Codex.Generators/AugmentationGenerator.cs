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
/// name. The constants are <c>&lt;Type&gt;Id</c>, <c>&lt;Type&gt;Name</c>, <c>&lt;Type&gt;Icon</c> (the full size image URL
/// of the record's icon, and the <c>Icon</c> override of <c>Augmentation</c>), one per scalar field of the record under the type
/// and the field's name (<c>SigilMaxRank</c>, <c>ProtectiveEnchantDuration</c>, <c>AspectProcsPerMinute</c>...), and the
/// record's tuning values and rank runs in a nested <c>Constants</c> under their <see cref="TuningNames"/> names, with no hero
/// as the record's hero. A tuning key with a field's value (<see cref="FieldTuning"/>) has no constant in <c>Constants</c>,
/// and the field's summary names it. Each rank run is a <c>public static readonly double[]</c> of its values by rank, named after its
/// key without the <c>Trait.&lt;Row&gt;.</c> prefix and <c>ByRank</c> (<c>Constants.CritPowerMultiplierByRank</c>). The
/// class's own part has the augmentation's behaviour and its <c>Name</c> and <c>Id</c> overrides. <c>GameIcons.Augmentations</c>
/// has the icon URLs of the records with no class, by ID. A member of the class or a
/// base class with a constant's name, or the name <c>Constants</c>, is CODEX017. The game
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

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX017", "An augmentation constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, and {1} is a member of {2}; rename the class's member", "Codex", DiagnosticSeverity.Error, true);

    /// <summary>A field constant of an augmentation record: its name, C# type and literal, and its summary.</summary>
    private sealed record Member(string Name, string Type, string Literal, string Summary, string? Field = null);

    /// <summary>
    /// An augmentation record: its type, ID, name, class name, its field constants, and its tuning keys and rank runs (a
    /// rank run is a tuning entry by rank, and its source is its summary's transform and tooltip numbers).
    /// </summary>
    private sealed record Record(string Type, int Id, string Name, string ClassName,
        EquatableArray<Member> Fields, EquatableArray<TuningEntry> Tunings);

    /// <summary>The game data's augmentation records, the tuning keys' first segments naming a hero, and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, EquatableArray<HeroSegment> Segments, string? BuildDate);

    /// <summary>
    /// A partial augmentation class: its namespace, name, record type (empty for a class of no listed base), the member
    /// names of the class and its base classes, the class's constants and static readonly fields, and where it is declared.
    /// </summary>
    private sealed record Declared(string Namespace, string Name, string Type, EquatableArray<string> Members, EquatableArray<string> Fields, Location? Location)
    {
        public bool Equals(Declared? other) => other != null && Namespace == other.Namespace && Name == other.Name && Type == other.Type
            && Members.Equals(other.Members) && Fields.Equals(other.Fields);

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
        return new Declared(NamespaceOf(node)!, type.Name, kind, new([.. members.OrderBy(m => m, StringComparer.Ordinal)]), new([.. TuningNames.FieldsOf(type)]),
            node.Identifier.GetLocation());
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
                var fields = FieldsOf(document, type);
                var keys = FieldTuning.KeysOf(document, fields.Select(f => f.Field).OfType<string>(), document);
                var merged = new HashSet<string>(keys.Values.SelectMany(k => k), StringComparer.Ordinal);
                records.Add(new Record(type, document.Id, name, PascalCase(name),
                    new([.. fields.Select(f => f.Field != null && keys.TryGetValue(f.Field, out var k) ? f with { Summary = FieldTuning.Summary(f.Summary, k) } : f)]),
                    new([.. TuningsOf(document).Where(t => !merged.Contains(t.Key)), .. RanksOf(document)])));
            }
        }
        return new Export(new([.. records.OrderBy(r => r.Type, StringComparer.Ordinal).ThenBy(r => r.Id)]), HeroSegments.Of(entities, ct), buildDate);
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
        if (a.Icon is { Length: > 0 } icon)
        {
            fields.Add(new Member(type + "Icon", "string", Literal(Textures.Url(icon)), $"Game data: the full size image URL of <c>icon</c> of {owner}."));
        }
        AddNumber(fields, type + "MaxRank", a.MaxRank, $"Game data: <c>maxRank</c> of {owner}.", "maxRank");
        AddNumber(fields, type + "RandomChance", a.RandomChance, $"Game data: <c>randomChance</c> of {owner}.", "randomChance");
        AddNumber(fields, type + "Cooldown", a.Cooldown, $"Game data: <c>cooldown</c> of {owner}.", "cooldown");
        AddNumber(fields, type + "Duration", a.Duration, $"Game data: <c>duration</c> of {owner}.", "duration");
        if (a.ProcRolls is [{ ProcsPerMinute: { } ppm } roll] && !(a.RankRuns ?? []).Any(r => r.Key == roll.Row))
        {
            string formula = roll.Formula is { Length: > 0 } f ? $" ({Escape(f)})" : "";
            AddNumber(fields, type + "ProcsPerMinute", ppm, $"Game data: <c>procsPerMinute</c> of proc roll <c>{Escape(roll.Row ?? "")}</c> of {owner}{formula}.");
        }
        return fields;
    }

    private static void AddNumber(List<Member> fields, string name, double? value, string summary, string? field = null)
    {
        if (value is not { } v) return;
        bool whole = v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;
        fields.Add(new Member(name, whole ? "int" : "double", whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : Number(v), summary, field));
    }

    /// <summary>The record's tuning keys, in the order of the game data.</summary>
    private static List<TuningEntry> TuningsOf(AugmentationDocument a) =>
        TuningNames.Entries(a.Tuning, v => ValueOf(v) is { } value ? (value.Type, value.Literal, false) : null, SourceOf);

    /// <summary>
    /// The record's rank runs, each an array of its values by rank (rank 1 first), named after its key without
    /// <c>Trait.&lt;Row&gt;.</c>. A run with a rank of no value has no array. The source names the run's transform and the
    /// tooltip's numbers: a <c>delta</c> run's values are multipliers (the tooltip has the value less 1), and a <c>ticks</c>
    /// run's values are per tick (the tooltip has the value times the ticks).
    /// </summary>
    private static List<TuningEntry> RanksOf(AugmentationDocument a)
    {
        var ranks = new List<TuningEntry>();
        foreach (var run in (a.RankRuns ?? []).Where(r => r.Key is { Length: > 0 }))
        {
            if (run.Values is not { Count: > 0 } values || values.Any(x => x == null)) continue;
            string transform = run.Transform is { Length: > 0 } t
                ? $", transform <c>{Escape(t)}</c>{(run.Ticks is { } ticks ? $" over {ticks} ticks" : "")}"
                : "";
            string printed = run.Printed is { Count: > 0 } p ? $"; the tooltip has {Escape(string.Join(" / ", p.Select(Untagged)))}" : "";
            var segments = run.Key!.Split('.');
            string nameKey = string.Join(".", segments.Skip(segments.Length > 2 && segments[0] == "Trait" ? 2 : 0));
            ranks.Add(new TuningEntry(run.Key!, nameKey, "double[]", string.Join(", ", values.Select(x => Number(x!.Value))), true, transform + printed));
        }
        return ranks;
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
        // The records with a class.
        var matched = new HashSet<int>();
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
            matched.Add(record.Id);
            string owner = $"{Words(record.Type)} {record.Id}";
            var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
            var all = new List<Member>
            {
                new(record.Type + "Id", "int", record.Id.ToString(CultureInfo.InvariantCulture), $"{Escape(record.Name)}, {owner} of the game data."),
                new(record.Type + "Name", "string", Literal(record.Name), $"Game data: <c>name</c> of {owner}."),
            };
            all.AddRange(record.Fields);
            var shared = new List<(string Name, string First, string Second)>();
            var tunings = TuningNames.Of(record.Tunings, export.Segments, [], shared);
            TuningNames.Report(context, d.Location, d.Name, owner, shared, d.Fields, tunings);

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(d.Namespace).Append(";\n\n")
                .Append("partial class ").Append(d.Name).Append("\n{\n");
            bool first = true;
            var written = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in all)
            {
                if (members.Contains(member.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, member.Name, owner));
                    continue;
                }
                if (!first) source.Append('\n');
                first = false;
                written.Add(member.Name);
                source.Append("    /// <summary>").Append(member.Summary).Append("</summary>\n")
                    .Append("    public const ").Append(member.Type).Append(' ').Append(member.Name).Append(" = ").Append(member.Literal).Append(";\n");
            }
            if (tunings.Count > 0)
            {
                if (members.Contains("Constants"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "Constants", owner));
                }
                else
                {
                    TuningNames.Write(source, $"Game data: the tuning values and rank runs of {owner}.",
                        tunings.Select(t => (t, t.ByRank
                            ? $"Game data: rank run <c>{Escape(t.Keys)}</c> of {owner}{t.Source}."
                            : $"Game data: tuning <c>{Escape(t.Keys)}</c> of {owner}{(t.Source.Length > 0 ? "; " + Escape(t.Source) : "")}.")),
                        false, !first);
                }
            }
            if (written.Contains(record.Type + "Icon"))
            {
                source.Append("\n    public override string? Icon => ").Append(record.Type).Append("Icon;\n");
            }
            source.Append("}\n");
            context.AddSource($"{d.Namespace.Substring(CustomizationNamespace.Length)}.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        // The icons of the records with no class.
        var unmatched = export.Records.Where(r => !matched.Contains(r.Id)).OrderBy(r => r.Id)
            .Select(r => (r.Id, Icon: r.Fields.FirstOrDefault(f => f.Name == r.Type + "Icon")?.Literal))
            .Where(r => r.Icon != null)
            .Select(r => new TableEntry(r.Id.ToString(CultureInfo.InvariantCulture), r.Icon!));
        context.AddSource("GameIcons.g.cs", SourceText.From(GameTables.Write("GameIcons", null,
        [
            new GameTable("Augmentations", "Icon URLs of the game data's aspect, sigil and protective enchant records with no augmentation class, by ID.",
                "int", "string", false, new([.. unmatched])),
        ], export.BuildDate), Encoding.UTF8));
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
