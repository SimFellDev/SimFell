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
/// Writes the game data's half of each hero ability class: for each <c>partial</c> class deriving from
/// <c>FellowshipSim.Core.Domain.Ability</c> in a namespace <c>FellowshipSim.Core.Heroes.&lt;Hero&gt;</c> or one inside it, a
/// <c>partial</c> of the same class with the values of its ability record. The record is the ability of that hero whose
/// name in Pascal case (letters and digits only, an apostrophe inside its word) is the class name; of several, the one with
/// a category. The constants are <c>AbilityId</c>, <c>AbilityName</c>, one per scalar field of the record under
/// <c>Ability</c> and the field's name (<c>AbilityCategory</c>, <c>AbilityCooldown</c>, <c>AbilityCastTime</c>...), and
/// one per tuning value of the record under the tuning value's name. The class's own part has the ability's behaviour. A
/// constant whose name the class or a base class already has is left out. The game data is the AdditionalFiles
/// <c>entities.jsonl</c> and <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK; <see cref="CreatureGenerator"/>
/// reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AbilityGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string HeroesNamespace = "FellowshipSim.Core.Heroes.";
    private const string AbilityType = "FellowshipSim.Core.Domain.Ability";

    private static readonly DiagnosticDescriptor NoAbility = new(
        "CODEX006", "A partial ability class names no ability record",
        "The partial class {0} names no ability record of hero {1} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralAbilities = new(
        "CODEX007", "A partial ability class names several ability records",
        "The partial class {0} names the ability records {1} of hero {2} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SharedTuningName = new(
        "CODEX008", "Two tuning values of an ability share a name",
        "Ability {0} has tuning values {1} with different values under the name {2}; the class has no constant {2}", "Codex", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX009", "An ability constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, so the constant {1} of ability {2} is left out", "Codex", DiagnosticSeverity.Warning, true);

    /// <summary>A constant of an ability record: its name, C# type and literal, and its summary.</summary>
    private sealed record Constant(string Name, string Type, string Literal, string Summary);

    /// <summary>
    /// An ability record: its ID, name, class name, heroes, whether it has a category, its field constants and its tuning
    /// values (a tuning value with no type has several values under its name, and its summary names the tuning keys).
    /// </summary>
    private sealed record Record(int Id, string Name, string ClassName, EquatableArray<string> Heroes, bool HasCategory,
        EquatableArray<Constant> Fields, EquatableArray<Constant> Tunings);

    /// <summary>The game data's ability records, and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, string? BuildDate);

    /// <summary>
    /// A partial ability class: its namespace, name and hero, the member names of the class and its base classes, the full
    /// names of its base classes (nearest first), and where it is declared.
    /// </summary>
    private sealed record Declared(string Namespace, string Name, string Hero, EquatableArray<string> Members, EquatableArray<string> Bases, Location? Location)
    {
        public string FullName => Namespace + "." + Name;

        public bool Equals(Declared? other) => other != null && Namespace == other.Namespace && Name == other.Name && Hero == other.Hero
            && Members.Equals(other.Members) && Bases.Equals(other.Bases);

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

    /// <summary>The partial class as a <see cref="Declared"/>, or null for a class not deriving from <c>Ability</c>.</summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        bool ability = false;
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        var bases = new List<string>();
        for (var t = type; t != null; t = t.BaseType)
        {
            string name = t.ToDisplayString();
            if (name == AbilityType) ability = true;
            if (!SymbolEqualityComparer.Default.Equals(t, type)) bases.Add(name);
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
        if (!ability) return null;
        string ns = NamespaceOf(node)!;
        string hero = ns.Substring(HeroesNamespace.Length).Split('.')[0];
        return new Declared(ns, type.Name, hero, new([.. members.OrderBy(m => m, StringComparer.Ordinal)]), new([.. bases]), node.Identifier.GetLocation());
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
                // Only ability lines are read in full.
                if (!text.Contains("\"$type\":\"ability\"")) continue;
                AbilityDocument? ability;
                try
                {
                    ability = JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options) as AbilityDocument;
                }
                catch (JsonException)
                {
                    continue;
                }
                if (ability == null) continue;
                string name = (ability.Name ?? "").Trim();
                var heroes = (ability.Heroes ?? []).Select(h => h.ToString()).ToList();
                if (name.Length == 0 || heroes.Count == 0) continue;
                records.Add(new Record(ability.Id, name, PascalCase(name), new([.. heroes]), ability.Category != null,
                    new([.. FieldsOf(ability)]), new([.. TuningsOf(ability)])));
            }
        }
        return new Export(new([.. records.OrderBy(r => r.Id)]), buildDate);
    }

    /// <summary>The record's scalar fields, each under <c>Ability</c> and the field's name.</summary>
    private static List<Constant> FieldsOf(AbilityDocument a)
    {
        var fields = new List<Constant>();
        if (a.Category is { } category)
        {
            fields.Add(new Constant("AbilityCategory", "global::FellowshipSim.Core.Domain.AbilityCategory",
                "global::FellowshipSim.Core.Domain.AbilityCategory." + category, Field("category", a.Id)));
        }
        AddNumber(fields, "Cooldown", "cooldown", a.Cooldown, a.Id);
        AddNumber(fields, "Charges", "charges", a.Charges, a.Id);
        AddNumber(fields, "GlobalCooldown", "globalCooldown", a.GlobalCooldown, a.Id);
        AddBool(fields, "GlobalCooldownScalesWithHaste", "globalCooldownScalesWithHaste", a.GlobalCooldownScalesWithHaste, a.Id);
        AddNumber(fields, "CastTime", "castTime", a.CastTime, a.Id);
        AddBool(fields, "CastTimeScalesWithHaste", "castTimeScalesWithHaste", a.CastTimeScalesWithHaste, a.Id);
        AddNumber(fields, "ChannelTime", "channelTime", a.ChannelTime, a.Id);
        AddNumber(fields, "ChannelTick", "channelTick", a.ChannelTick, a.Id);
        AddNumber(fields, "Cost", "cost", a.Cost, a.Id);
        if (a.Resource is { Length: > 0 } resource) fields.Add(new Constant("AbilityResource", "string", Literal(resource), Field("resource", a.Id)));
        AddNumber(fields, "Duration", "duration", a.Duration, a.Id);
        AddNumber(fields, "Period", "period", a.Period, a.Id);
        AddNumber(fields, "Range", "range", a.Range, a.Id);
        AddNumber(fields, "MinRange", "minRange", a.MinRange, a.Id);
        AddNumber(fields, "Radius", "radius", a.Radius, a.Id);
        AddNumber(fields, "MaxTargets", "maxTargets", a.MaxTargets, a.Id);
        return fields;
    }

    private static string Field(string field, int id) => $"Game data: <c>{field}</c> of ability {id}.";

    private static void AddNumber(List<Constant> fields, string name, string field, double? value, int id)
    {
        if (value is not { } v) return;
        bool whole = v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;
        fields.Add(new Constant("Ability" + name, whole ? "int" : "double",
            whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : Number(v), Field(field, id)));
    }

    private static void AddBool(List<Constant> fields, string name, string field, bool? value, int id)
    {
        if (value is not { } v) return;
        fields.Add(new Constant("Ability" + name, "bool", v ? "true" : "false", Field(field, id)));
    }

    /// <summary>The record's tuning values, one per name, in the order of the game data.</summary>
    private static List<Constant> TuningsOf(AbilityDocument ability)
    {
        var tunings = new List<Constant>();
        foreach (var group in (ability.Tuning ?? new Dictionary<string, TuningReference>()).GroupBy(t => t.Value.Name ?? ""))
        {
            if (group.Key.Length == 0) continue;
            var values = group.Select(t => ValueOf(t.Value.Value)).Distinct().ToList();
            string keys = string.Join(", ", group.Select(t => t.Key).OrderBy(k => k.Length).ThenBy(k => k, StringComparer.Ordinal));
            if (values.Count != 1 || values[0] is not { } value)
            {
                // Two values under one name: no constant, and the diagnostic names the keys.
                tunings.Add(new Constant(group.Key, "", "", keys));
                continue;
            }
            string source = SourceOf(group.First().Value);
            tunings.Add(new Constant(group.Key, value.Type, value.Literal,
                $"Game data: tuning <c>{Escape(keys)}</c> of ability {ability.Id}{(source.Length > 0 ? "; " + Escape(source) : "")}."));
        }
        return tunings;
    }

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

    /// <summary>The tuning value's owner, the talent changing it, and its attribute and operation where it has them.</summary>
    private static string SourceOf(TuningReference tuning)
    {
        var parts = new List<string>();
        if (tuning.Owner is { } owner) parts.Add(ReferenceOf(owner));
        if (tuning.Talent is { } talent) parts.Add("talent " + ReferenceOf(talent));
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
        // The constants written for each class, so a class deriving from another declares its own with new. A base class
        // comes before the classes deriving from it.
        var written = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var d in declared.GroupBy(d => (d.Namespace, d.Name)).Select(g => g.First())
                     .OrderBy(d => d.Bases.Count()).ThenBy(d => d.Namespace, StringComparer.Ordinal).ThenBy(d => d.Name, StringComparer.Ordinal))
        {
            var candidates = byClass[d.Name].Where(r => r.Heroes.Contains(d.Hero)).ToList();
            var picked = candidates.Count == 1 ? candidates : candidates.Where(r => r.HasCategory).ToList();
            if (candidates.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoAbility, d.Location, d.Name, d.Hero));
                continue;
            }
            if (picked.Count != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(SeveralAbilities, d.Location, d.Name, string.Join(", ", candidates.Select(r => r.Id)), d.Hero));
                continue;
            }

            var record = picked[0];
            var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
            var constants = new List<Constant>
            {
                new("AbilityId", "int", record.Id.ToString(CultureInfo.InvariantCulture), $"{Escape(record.Name)}, ability {record.Id} of the game data."),
                new("AbilityName", "string", Literal(record.Name), $"Game data: <c>name</c> of ability {record.Id}."),
            };
            constants.AddRange(record.Fields);
            var fieldNames = new HashSet<string>(constants.Select(c => c.Name), StringComparer.Ordinal);
            foreach (var tuning in record.Tunings)
            {
                if (tuning.Type.Length == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(SharedTuningName, d.Location, record.Id, tuning.Summary, tuning.Name));
                    continue;
                }
                if (fieldNames.Contains(tuning.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, tuning.Name, record.Id));
                    continue;
                }
                constants.Add(tuning);
            }

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(d.Namespace).Append(";\n\n")
                .Append("partial class ").Append(d.Name).Append("\n{\n");
            var inherited = new HashSet<string>(d.Bases.Where(written.ContainsKey).SelectMany(b => written[b]), StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var constant in constants)
            {
                if (members.Contains(constant.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, constant.Name, record.Id));
                    continue;
                }
                if (names.Count > 0) source.Append('\n');
                names.Add(constant.Name);
                source.Append("    /// <summary>").Append(constant.Summary).Append("</summary>\n")
                    .Append("    public ").Append(inherited.Contains(constant.Name) ? "new " : "").Append("const ").Append(constant.Type).Append(' ')
                    .Append(constant.Name).Append(" = ").Append(constant.Literal).Append(";\n");
            }
            source.Append("}\n");
            written[d.FullName] = names;
            context.AddSource($"{d.Namespace.Substring(HeroesNamespace.Length)}.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
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
            // An apostrophe stays inside its word: Widow's is Widows.
            else if (c != '\'') start = true;
        }
        return result.ToString();
    }

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
