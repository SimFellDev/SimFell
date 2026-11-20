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
/// a category. The constants are <c>AbilityId</c>, <c>AbilityName</c>, <c>AbilityIcon</c> (the full size image URL of the
/// record's icon), one per scalar field of the record under
/// <c>Ability</c> and the field's name (<c>AbilityCategory</c>, <c>AbilityCooldown</c>, <c>AbilityCastTime</c>...), and
/// the record's tuning values in a nested <c>Constants</c> under their <see cref="TuningNames"/> names
/// (<c>Constants.MaxCharges</c>), with the hero of the class's namespace as the record's hero. A tuning key with a field's
/// value (<see cref="FieldTuning"/>) has no constant in <c>Constants</c>, and the field's summary names it. A class deriving from another
/// ability class declares its <c>Constants</c> with <c>new</c>. The class's own part has the ability's behaviour. A member
/// of the class or a base class with a constant's name, or the name <c>Constants</c>, is CODEX009. A record listed in
/// another record's <c>additionalAbilities</c>, or with <c>replaces</c>, <c>subcastOf</c> or <c>triggeredBy</c>, is no
/// class's record by its name. A class with <c>[Ability(id)]</c> (<c>FellowshipSim.Core.Domain.AbilityAttribute</c>) has
/// the record of that ID and hero, with or without a base class of its own part; no such record is CODEX046. When the
/// record names a base ability, the half derives from that ability's class (no such class is CODEX047) and has
/// <c>AbilityId</c>, the constants with another value than the base record's, and the tuning values the base record has
/// no key of with that value. A constant of the class's own part replaces the generated one, except <c>AbilityId</c>. The game data is the AdditionalFiles
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

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX009", "An ability constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, and {1} is a constant of ability {2}; rename the member", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NoAbilityId = new(
        "CODEX046", "An ability class's Ability attribute names no ability record",
        "The partial class {0} has [Ability({1})], and the game data has no ability {1} of hero {2}", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NoBaseClass = new(
        "CODEX047", "An ability record's base ability has no partial class",
        "The partial class {0} has [Ability({1})], ability {1} has {2} ability {3}, and no partial ability class of hero {4} has ability {3}",
        "Codex", DiagnosticSeverity.Error, true);

    private const string AbilityAttributeType = "FellowshipSim.Core.Domain.AbilityAttribute";

    /// <summary>A constant of an ability record: its name, C# type and literal, its summary, and the record's field it has.</summary>
    private sealed record Constant(string Name, string Type, string Literal, string Summary, string? Field = null);

    /// <summary>A tuning key of an ability record: the key, its entry's name, its owner's ability ID, and its value's literal.</summary>
    private sealed record TuningKey(string Key, string Name, int? OwnerAbility, string Literal);

    /// <summary>
    /// An ability record: its ID, name, class name, heroes, whether it has a category, its field constants, its tuning
    /// values and keys, the ability it is a version of (<c>replaces</c>, <c>subcastOf</c> or <c>triggeredBy</c>) with that
    /// field's name, and its <c>additionalAbilities</c>.
    /// </summary>
    private sealed record Record(int Id, string Name, string ClassName, EquatableArray<string> Heroes, bool HasCategory,
        EquatableArray<Constant> Fields, EquatableArray<TuningEntry> Tunings, EquatableArray<TuningKey> Keys,
        int? BaseAbility, string? BaseField, EquatableArray<int> Additional);

    /// <summary>The game data's ability records, the tuning keys' first segments naming a hero, and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, EquatableArray<HeroSegment> Segments, string? BuildDate);

    /// <summary>
    /// A partial ability class: its namespace, name and hero, the member names of the class and its base classes, the
    /// member names of the class alone, the class's constants and static readonly fields, the full names of its base
    /// classes (nearest first), the ID of its <c>[Ability]</c> attribute, and where it is declared.
    /// </summary>
    private sealed record Declared(string Namespace, string Name, string Hero, EquatableArray<string> Members, EquatableArray<string> Own,
        EquatableArray<string> Fields, EquatableArray<string> Bases, int? RecordId, Location? Location)
    {
        public string FullName => Namespace + "." + Name;

        public bool Equals(Declared? other) => other != null && Namespace == other.Namespace && Name == other.Name && Hero == other.Hero
            && Members.Equals(other.Members) && Own.Equals(other.Own) && Fields.Equals(other.Fields) && Bases.Equals(other.Bases)
            && RecordId == other.RecordId;

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

    /// <summary>
    /// The partial class as a <see cref="Declared"/>, or null for a class with no <c>[Ability]</c> attribute and not deriving
    /// from <c>Ability</c>. The generated half of a class with the attribute has its base class, so the class's own half
    /// can have none.
    /// </summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        int? recordId = type.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AbilityAttributeType)?
            .ConstructorArguments.FirstOrDefault().Value as int?;
        bool ability = recordId != null;
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
        var own = type.GetMembers().Where(m => !m.IsImplicitlyDeclared).Select(m => m.Name).Distinct().OrderBy(m => m, StringComparer.Ordinal);
        // A class with the attribute and no base class of its own half has System.Object as its base.
        if (type.BaseType?.SpecialType == SpecialType.System_Object) bases.Clear();
        return new Declared(ns, type.Name, hero, new([.. members.OrderBy(m => m, StringComparer.Ordinal)]), new([.. own]),
            new([.. TuningNames.FieldsOf(type)]), new([.. bases]), recordId, node.Identifier.GetLocation());
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
                var fields = FieldsOf(ability);
                var keys = FieldTuning.KeysOf(ability, fields.Select(f => f.Field).OfType<string>(), ability);
                var merged = new HashSet<string>(keys.Values.SelectMany(k => k), StringComparer.Ordinal);
                var tunings = TuningsOf(ability).Where(t => !merged.Contains(t.Key)).ToList();
                var tuningKeys = tunings.Select(t =>
                {
                    var tuning = ability.Tuning![t.Key];
                    return new TuningKey(t.Key, tuning.Name ?? t.Key, tuning.Owner is { Type: EntityType.Ability } owner ? owner.Id : null, t.Literal);
                });
                var (baseAbility, baseField) = ability.Replaces is { } replaces ? (replaces, "replaces")
                    : ability.SubcastOf is { } subcast ? (subcast, "subcastOf")
                    : ability.TriggeredBy is { } trigger ? (trigger, "triggeredBy")
                    : ((EntityReference?)null, (string?)null);
                records.Add(new Record(ability.Id, name, PascalCase(name), new([.. heroes]), ability.Category != null,
                    new([.. fields.Select(f => f.Field != null && keys.TryGetValue(f.Field, out var k) ? f with { Summary = FieldTuning.Summary(f.Summary, k) } : f)]),
                    new([.. tunings]), new([.. tuningKeys]), baseAbility?.Id, baseField,
                    new([.. (ability.AdditionalAbilities ?? []).Select(a => a.Id)])));
            }
        }
        return new Export(new([.. records.OrderBy(r => r.Id)]), HeroSegments.Of(entities, ct), buildDate);
    }

    /// <summary>The record's scalar fields, each under <c>Ability</c> and the field's name.</summary>
    private static List<Constant> FieldsOf(AbilityDocument a)
    {
        var fields = new List<Constant>();
        if (a.Icon is { Length: > 0 } icon)
        {
            fields.Add(new Constant("AbilityIcon", "string", Literal(Textures.Url(icon)), $"Game data: the full size image URL of <c>icon</c> of ability {a.Id}."));
        }
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
            whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : Number(v), Field(field, id), field));
    }

    private static void AddBool(List<Constant> fields, string name, string field, bool? value, int id)
    {
        if (value is not { } v) return;
        fields.Add(new Constant("Ability" + name, "bool", v ? "true" : "false", Field(field, id), field));
    }

    /// <summary>The record's tuning keys, in the order of the game data.</summary>
    private static List<TuningEntry> TuningsOf(AbilityDocument ability) =>
        TuningNames.Entries(ability.Tuning, v => ValueOf(v) is { } value ? (value.Type, value.Literal, false) : null, SourceOf);

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
        // A version of another ability is no class's record by its name: a class has it through [Ability].
        var versions = new HashSet<int>(export.Records.SelectMany(r => r.Additional));
        versions.UnionWith(export.Records.Where(r => r.BaseAbility != null).Select(r => r.Id));
        var byClass = export.Records.Where(r => !versions.Contains(r.Id)).ToLookup(r => r.ClassName);
        var byId = export.Records.ToLookup(r => r.Id);
        // Every constant name each class has (written, inherited), so a class deriving from another declares its own with
        // new, and the class of each record and hero.
        var written = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var classOf = new Dictionary<(int Id, string Hero), (Declared Class, Record Record)>();
        var classes = declared.GroupBy(d => (d.Namespace, d.Name)).Select(g => g.First()).ToList();

        // The classes of a name's record, a base class before the classes deriving from it.
        foreach (var d in classes.Where(d => d.RecordId == null)
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
            WriteClass(context, export, d, picked[0], null, null, written);
            classOf[(picked[0].Id, d.Hero)] = (d, picked[0]);
        }

        // The classes of an [Ability] record, the class of a base ability before the class of its version.
        var pending = new List<(Declared Class, Record Record)>();
        foreach (var d in classes.Where(d => d.RecordId != null).OrderBy(d => d.Namespace, StringComparer.Ordinal).ThenBy(d => d.Name, StringComparer.Ordinal))
        {
            if (byId[d.RecordId!.Value].FirstOrDefault(r => r.Heroes.Contains(d.Hero)) is { } record) pending.Add((d, record));
            else context.ReportDiagnostic(Diagnostic.Create(NoAbilityId, d.Location, d.Name, d.RecordId, d.Hero));
        }
        while (pending.Count > 0)
        {
            var ready = pending.Where(p => p.Record.BaseAbility is not { } b || classOf.ContainsKey((b, p.Class.Hero))).ToList();
            if (ready.Count == 0)
            {
                foreach (var (d, record) in pending)
                {
                    context.ReportDiagnostic(Diagnostic.Create(NoBaseClass, d.Location, d.Name, record.Id, record.BaseField, record.BaseAbility, d.Hero));
                }
                break;
            }
            foreach (var (d, record) in ready)
            {
                var (baseClass, baseRecord) = record.BaseAbility is { } b ? classOf[(b, d.Hero)] : default;
                WriteClass(context, export, d, record, baseClass, baseRecord, written);
                classOf[(record.Id, d.Hero)] = (d, record);
                pending.Remove((d, record));
            }
        }
    }

    /// <summary>The ID, name and field constants of an ability record.</summary>
    private static List<Constant> ConstantsOf(Record record)
    {
        string summary = $"{Escape(record.Name)}, ability {record.Id} of the game data"
            + (record.BaseAbility is { } b ? $"; <c>{record.BaseField}</c> ability {b}." : ".");
        var constants = new List<Constant>
        {
            new("AbilityId", "int", record.Id.ToString(CultureInfo.InvariantCulture), summary),
            new("AbilityName", "string", Literal(record.Name), $"Game data: <c>name</c> of ability {record.Id}."),
        };
        constants.AddRange(record.Fields);
        return constants;
    }

    /// <summary>
    /// Writes the class's half of its record. With a base ability's class and record (<paramref name="baseClass"/>), the half derives
    /// from that class and has <c>AbilityId</c>, the constants with another value than the base ability's, and the tuning
    /// values the base ability has no key of with that value; a constant of the class's own half is its own.
    /// </summary>
    private static void WriteClass(SourceProductionContext context, Export export, Declared d, Record record,
        Declared? baseClass, Record? baseRecord, Dictionary<string, HashSet<string>> written)
    {
        var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
        var own = new HashSet<string>(d.Own, StringComparer.Ordinal);
        var constants = ConstantsOf(record);
        var tuningEntries = record.Tunings.ToList();
        if (baseClass != null && baseRecord != null)
        {
            members.UnionWith(baseClass.Members);
            var baseConstants = ConstantsOf(baseRecord).ToDictionary(c => c.Name, StringComparer.Ordinal);
            constants = [.. constants.Where(c => c.Name == "AbilityId"
                || (!own.Contains(c.Name) && !(baseConstants.TryGetValue(c.Name, out var b) && b.Type == c.Type && b.Literal == c.Literal)))];
            // A tuning key of the base ability with the version's value: the key itself, or for a key the base ability owns,
            // its entry's name.
            var inheritedKeys = new HashSet<string>(record.Keys.Where(k => baseRecord.Keys.Any(b => b.Literal == k.Literal
                && (b.Key == k.Key || (k.OwnerAbility == baseRecord.Id && b.Key == k.Name)))).Select(k => k.Key), StringComparer.Ordinal);
            tuningEntries = [.. tuningEntries.Where(t => !inheritedKeys.Contains(t.Key))];
        }
        var shared = new List<(string Name, string First, string Second)>();
        var tunings = TuningNames.Of(tuningEntries, export.Segments, [d.Hero], shared);
        TuningNames.Report(context, d.Location, d.Name, $"ability {record.Id}", shared, d.Fields, tunings);

        var source = new StringBuilder()
            .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
            .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
            .Append("#nullable enable\n")
            .Append("namespace ").Append(d.Namespace).Append(";\n\n")
            .Append("partial class ").Append(d.Name);
        // The class's own half names its base class, or this half does.
        if (baseClass != null && !d.Bases.Any()) source.Append(" : global::").Append(baseClass.FullName);
        source.Append("\n{\n");
        var inherited = new HashSet<string>(d.Bases.Where(written.ContainsKey).SelectMany(b => written[b]), StringComparer.Ordinal);
        if (baseClass != null && written.TryGetValue(baseClass.FullName, out var baseNames)) inherited.UnionWith(baseNames);
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
        if (tunings.Count > 0)
        {
            if (members.Contains("Constants"))
            {
                context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "Constants", record.Id));
            }
            else
            {
                TuningNames.Write(source, $"Game data: the tuning values of ability {record.Id}.",
                    tunings.Select(t => (t, $"Game data: tuning <c>{Escape(t.Keys)}</c> of ability {record.Id}{(t.Source.Length > 0 ? "; " + Escape(t.Source) : "")}.")),
                    inherited.Contains("Constants"), names.Count > 0);
                names.Add("Constants");
            }
        }
        source.Append("}\n");
        names.UnionWith(inherited);
        written[d.FullName] = names;
        context.AddSource($"{d.Namespace.Substring(HeroesNamespace.Length)}.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
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
