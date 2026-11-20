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
/// Writes the game data's half of each hero talent class: for each <c>partial</c> class deriving from
/// <c>FellowshipSim.Core.Domain.Talent</c> in a namespace <c>FellowshipSim.Core.Heroes.&lt;Hero&gt;</c> or one inside it, a
/// <c>partial</c> of the same class with the values of its talent record. The record is the talent of that hero (the hero of
/// its talent slot, or one of its origins) whose name in Pascal case (letters and digits only, an apostrophe inside its word)
/// is the class name. The constants are <c>TalentId</c>, <c>TalentName</c>, <c>TalentIcon</c> (the full size image URL of
/// the record's icon), one per scalar field of the record under <c>Talent</c> and the field's name (<c>TalentCooldown</c>,
/// <c>TalentDuration</c>, <c>TalentRadius</c>...), and the record's tuning values in a nested <c>Constants</c> under their
/// <see cref="TuningNames"/> names, with the hero of the class's namespace as the record's hero. A tuning key with a field's
/// value (<see cref="FieldTuning"/>) has no constant in <c>Constants</c>, and the field's summary names it. The <c>Icon</c> override
/// of <c>Talent</c> is <c>TalentIcon</c>. The class's own part has the talent's behaviour and its <c>Name</c> and
/// <c>Id</c> overrides. <c>GameIcons.Talents</c> has the icon URLs of the talent records with no class, by ID, and
/// <c>GameHeroData.TalentNames</c> the names of those in a talent slot. A member of the class or a base class with a constant's name, or the name <c>Constants</c>, is CODEX013. The game
/// data is the AdditionalFiles <c>entities.jsonl</c> and <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK;
/// <see cref="CreatureGenerator"/> reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TalentGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string HeroesNamespace = "FellowshipSim.Core.Heroes.";
    private const string TalentType = "FellowshipSim.Core.Domain.Talent";

    private static readonly DiagnosticDescriptor NoTalent = new(
        "CODEX010", "A partial talent class names no talent record",
        "The partial class {0} names no talent record of hero {1} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralTalents = new(
        "CODEX011", "A partial talent class names several talent records",
        "The partial class {0} names the talent records {1} of hero {2} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX013", "A talent constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, and {1} is a constant of talent {2}; rename the member", "Codex", DiagnosticSeverity.Error, true);

    /// <summary>A constant of a talent record: its name, C# type and literal, and its summary.</summary>
    private sealed record Constant(string Name, string Type, string Literal, string Summary, string? Field = null);

    /// <summary>
    /// A talent record: its ID, name, class name, heroes, its field constants and its tuning values.
    /// </summary>
    private sealed record Record(int Id, string Name, string ClassName, EquatableArray<string> Heroes,
        EquatableArray<Constant> Fields, EquatableArray<TuningEntry> Tunings);

    /// <summary>A talent record of any hero: its ID, name, icon URL (empty for none) and whether a talent slot has it.</summary>
    private sealed record Listed(int Id, string Name, string Icon, bool InSlot);

    /// <summary>
    /// The game data's talent records with a hero, every talent record in ID order, the tuning keys' first segments naming
    /// a hero, and its build date.
    /// </summary>
    private sealed record Export(EquatableArray<Record> Records, EquatableArray<Listed> All, EquatableArray<HeroSegment> Segments, string? BuildDate);

    /// <summary>
    /// A partial talent class: its namespace, name and hero, the member names of the class and its base classes, the class's
    /// constants and static readonly fields, and where it is declared.
    /// </summary>
    private sealed record Declared(string Namespace, string Name, string Hero, EquatableArray<string> Members, EquatableArray<string> Fields, Location? Location)
    {
        public bool Equals(Declared? other) => other != null && Namespace == other.Namespace && Name == other.Name && Hero == other.Hero
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
                    && NamespaceOf(c) is { } ns && ns.StartsWith(HeroesNamespace, StringComparison.Ordinal),
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

    /// <summary>The partial class as a <see cref="Declared"/>, or null for a class not deriving from <c>Talent</c>.</summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        bool talent = false;
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.ToDisplayString() == TalentType) talent = true;
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
        if (!talent) return null;
        string ns = NamespaceOf(node)!;
        string hero = ns.Substring(HeroesNamespace.Length).Split('.')[0];
        return new Declared(ns, type.Name, hero, new([.. members.OrderBy(m => m, StringComparer.Ordinal)]), new([.. TuningNames.FieldsOf(type)]),
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

        var talents = new List<TalentDocument>();
        // The heroes of each talent's slots: a talent record's origins do not always name its hero.
        var slotHeroes = new Dictionary<int, List<string>>();
        if (entities != null)
        {
            foreach (var line in entities.Lines)
            {
                ct.ThrowIfCancellationRequested();
                string text = line.ToString();
                // Only talent and talent slot lines are read in full.
                if (!text.Contains("\"$type\":\"talent\"") && !text.Contains("\"$type\":\"talent-slot\"")) continue;
                try
                {
                    switch (JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options))
                    {
                        case TalentDocument t:
                            talents.Add(t);
                            break;
                        case TalentSlotDocument { Talent: { } slotted } slot:
                            if (!slotHeroes.TryGetValue(slotted.Id, out var heroes)) slotHeroes[slotted.Id] = heroes = [];
                            heroes.Add(slot.Hero.ToString());
                            break;
                    }
                }
                catch (JsonException)
                {
                }
            }
        }

        var records = new List<Record>();
        foreach (var talent in talents.OrderBy(t => t.Id))
        {
            string name = (talent.Name ?? "").Trim();
            var heroes = (slotHeroes.TryGetValue(talent.Id, out var slotted) ? slotted : [])
                .Concat((talent.Origins ?? []).Where(o => o.Type == EntityType.Hero).Select(o => (o.Name ?? "").Trim()))
                .Where(h => h.Length > 0).Distinct().ToList();
            if (name.Length == 0 || heroes.Count == 0) continue;
            var fields = FieldsOf(talent);
            var keys = FieldTuning.KeysOf(talent, fields.Select(f => f.Field).OfType<string>(), talent);
            var merged = new HashSet<string>(keys.Values.SelectMany(k => k), StringComparer.Ordinal);
            records.Add(new Record(talent.Id, name, PascalCase(name), new([.. heroes]),
                new([.. fields.Select(f => f.Field != null && keys.TryGetValue(f.Field, out var k) ? f with { Summary = FieldTuning.Summary(f.Summary, k) } : f)]),
                new([.. TuningsOf(talent).Where(t => !merged.Contains(t.Key))])));
        }
        var all = talents.OrderBy(t => t.Id)
            .Select(t => new Listed(t.Id, t.Name ?? "", t.Icon is { Length: > 0 } icon ? Textures.Url(icon) : "", slotHeroes.ContainsKey(t.Id)))
            .ToList();
        return new Export(new([.. records]), new([.. all]), HeroSegments.Of(entities, ct), buildDate);
    }

    /// <summary>The record's scalar fields, each under <c>Talent</c> and the field's name.</summary>
    private static List<Constant> FieldsOf(TalentDocument t)
    {
        var fields = new List<Constant>();
        if (t.Icon is { Length: > 0 } icon)
        {
            fields.Add(new Constant("TalentIcon", "string", Literal(Textures.Url(icon)), $"Game data: the full size image URL of <c>icon</c> of talent {t.Id}."));
        }
        AddNumber(fields, "Cooldown", "cooldown", t.Cooldown, t.Id);
        AddNumber(fields, "Cost", "cost", t.Cost, t.Id);
        AddNumber(fields, "Duration", "duration", t.Duration, t.Id);
        AddNumber(fields, "Range", "range", t.Range, t.Id);
        AddNumber(fields, "Radius", "radius", t.Radius, t.Id);
        return fields;
    }

    private static void AddNumber(List<Constant> fields, string name, string field, double? value, int id)
    {
        if (value is not { } v) return;
        bool whole = v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;
        fields.Add(new Constant("Talent" + name, whole ? "int" : "double",
            whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : Number(v), $"Game data: <c>{field}</c> of talent {id}.", field));
    }

    /// <summary>The record's tuning keys, in the order of the game data.</summary>
    private static List<TuningEntry> TuningsOf(TalentDocument talent) =>
        TuningNames.Entries(talent.Tuning, v => ValueOf(v) is { } value ? (value.Type, value.Literal, false) : null, SourceOf);

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

    /// <summary>
    /// The tuning value's owner, and its attribute and operation where it has them. The talent a tuning value names is left
    /// out: the game data names another talent on some records' tuning values.
    /// </summary>
    private static string SourceOf(TuningReference tuning)
    {
        var parts = new List<string>();
        if (tuning.Owner is { } owner) parts.Add(ReferenceOf(owner));
        if (tuning.Attribute is { Length: > 0 } attribute) parts.Add(tuning.Op is { Length: > 0 } op ? $"{attribute} {op}" : attribute);
        return string.Join(", ", parts);
    }

    private static string ReferenceOf(EntityReference reference)
    {
        string type = Words(reference.Type.ToString());
        return reference.Name is { Length: > 0 } name ? $"{name} ({type} {reference.Id})" : $"{type} {reference.Id}";
    }

    /// <summary>A Pascal case type name as lower case words: <c>LegendaryPower</c> is <c>legendary power</c>.</summary>
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

    private static void Write(SourceProductionContext context, Export export, ImmutableArray<Declared> declared)
    {
        var byClass = export.Records.ToLookup(r => r.ClassName);
        // The IDs of the talent records with a class.
        var matched = new HashSet<int>();
        foreach (var d in declared.GroupBy(d => (d.Namespace, d.Name)).Select(g => g.First())
                     .OrderBy(d => d.Namespace, StringComparer.Ordinal).ThenBy(d => d.Name, StringComparer.Ordinal))
        {
            var candidates = byClass[d.Name].Where(r => r.Heroes.Contains(d.Hero)).ToList();
            if (candidates.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoTalent, d.Location, d.Name, d.Hero));
                continue;
            }
            if (candidates.Count != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(SeveralTalents, d.Location, d.Name, string.Join(", ", candidates.Select(r => r.Id)), d.Hero));
                continue;
            }

            var record = candidates[0];
            matched.Add(record.Id);
            var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
            var constants = new List<Constant>
            {
                new("TalentId", "int", record.Id.ToString(CultureInfo.InvariantCulture), $"{Escape(record.Name)}, talent {record.Id} of the game data."),
                new("TalentName", "string", Literal(record.Name), $"Game data: <c>name</c> of talent {record.Id}."),
            };
            constants.AddRange(record.Fields);
            var shared = new List<(string Name, string First, string Second)>();
            var tunings = TuningNames.Of(record.Tunings, export.Segments, [d.Hero], shared);
            TuningNames.Report(context, d.Location, d.Name, $"talent {record.Id}", shared, d.Fields, tunings);

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(d.Namespace).Append(";\n\n")
                .Append("partial class ").Append(d.Name).Append("\n{\n");
            bool first = true;
            var written = new HashSet<string>(StringComparer.Ordinal);
            foreach (var constant in constants)
            {
                if (members.Contains(constant.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, constant.Name, record.Id));
                    continue;
                }
                if (!first) source.Append('\n');
                first = false;
                written.Add(constant.Name);
                source.Append("    /// <summary>").Append(constant.Summary).Append("</summary>\n")
                    .Append("    public const ").Append(constant.Type).Append(' ').Append(constant.Name).Append(" = ").Append(constant.Literal).Append(";\n");
            }
            if (tunings.Count > 0)
            {
                if (members.Contains("Constants"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "Constants", record.Id));
                }
                else
                {
                    TuningNames.Write(source, $"Game data: the tuning values of talent {record.Id}.",
                        tunings.Select(t => (t, $"Game data: tuning <c>{Escape(t.Keys)}</c> of talent {record.Id}{(t.Source.Length > 0 ? "; " + Escape(t.Source) : "")}.")),
                        false, !first);
                }
            }
            if (written.Contains("TalentIcon"))
            {
                source.Append("\n    public override string? Icon => TalentIcon;\n");
            }
            source.Append("}\n");
            context.AddSource($"{d.Namespace.Substring(HeroesNamespace.Length)}.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        // The talents with no class: their icons, and the names of those in a talent slot.
        var unmatched = export.All.Where(t => !matched.Contains(t.Id)).ToList();
        string Key(Listed t) => t.Id.ToString(CultureInfo.InvariantCulture);
        context.AddSource("GameIcons.g.cs", SourceText.From(GameTables.Write("GameIcons", null,
        [
            new GameTable("Talents", "Icon URLs of the game data's talent records with no talent class, by ID.", "int", "string", false,
                new([.. unmatched.Where(t => t.Icon.Length > 0).Select(t => new TableEntry(Key(t), Literal(t.Icon)))])),
        ], export.BuildDate), Encoding.UTF8));
        context.AddSource("GameHeroData.g.cs", SourceText.From(GameTables.Write("GameHeroData", null,
        [
            new GameTable("TalentNames", "The name of each talent in a talent tree with no talent class, by ID.", "int", "string", false,
                new([.. unmatched.Where(t => t.InSlot).Select(t => new TableEntry(Key(t), Literal(t.Name)))])),
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
            // An apostrophe stays inside its word: Skandi's is Skandis.
            else if (c != '\'') start = true;
        }
        return result.ToString();
    }

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
