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
/// Writes the game data's half of each relic passive class: for each <c>partial</c> class deriving from
/// <c>FellowshipSim.Core.Customization.RelicPassives.RelicPassive</c>, a <c>partial</c> of the same class with the values of
/// its relic passive record. The record is the relic passive whose name in Pascal case (letters and digits only, an
/// apostrophe inside its word) is the class name. The constants are <c>RelicPassiveId</c>, <c>RelicPassiveName</c>, one per
/// scalar field of the record under <c>RelicPassive</c> and the field's name (<c>RelicPassiveCooldown</c>, and
/// <c>RelicPassiveProcsPerMinute</c> from its one proc roll), one per tuning value of the record under the tuning value's
/// name, and <c>&lt;Slot&gt;ItemId</c> and <c>&lt;Slot&gt;ItemName</c> for each item granting the passive (one item line
/// per hero, one constant pair per item ID). <c>RelicPassiveItems</c> is a <c>public static readonly RelicItem[]</c> of
/// those items. The class's own part has the passive's behaviour and its <c>Id</c>, <c>Name</c> and <c>Items</c> overrides.
/// A constant whose name the class or a base class already has is left out. The game data is the AdditionalFiles
/// <c>entities.jsonl</c> and <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK; <see cref="CreatureGenerator"/>
/// reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class RelicPassiveGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string RelicPassiveType = "FellowshipSim.Core.Customization.RelicPassives.RelicPassive";
    private const string RelicPassiveNamespace = "FellowshipSim.Core.Customization.RelicPassives";
    private const string RelicItemType = "global::FellowshipSim.Core.Customization.RelicPassives.RelicItem";

    private static readonly DiagnosticDescriptor NoRecord = new(
        "CODEX026", "A partial relic passive class names no relic passive record",
        "The partial class {0} names no relic passive record in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralRecords = new(
        "CODEX027", "A partial relic passive class names several relic passive records",
        "The partial class {0} names the relic passive records {1} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SharedName = new(
        "CODEX028", "Two values of a relic passive share a name",
        "Relic passive {0} has values {1} that differ under the name {2}; the class has no constant {2}", "Codex", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX029", "A relic passive constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, so the constant {1} of relic passive {2} is left out", "Codex", DiagnosticSeverity.Warning, true);

    /// <summary>
    /// A constant of a relic passive record: its name, C# type and literal, and its summary. A constant with no type has
    /// several values under its name, and its summary names them.
    /// </summary>
    private sealed record Constant(string Name, string Type, string Literal, string Summary);

    /// <summary>An item granting a relic passive: its ID, name and slot.</summary>
    private sealed record Item(int Id, string Name, string Slot);

    /// <summary>A relic passive record: its ID, name, class name, the items granting it, its field constants and its tuning values.</summary>
    private sealed record Record(int Id, string Name, string ClassName, EquatableArray<Item> Items, EquatableArray<Constant> Fields,
        EquatableArray<Constant> Tunings);

    /// <summary>The game data's relic passive records, and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, string? BuildDate);

    /// <summary>A partial relic passive class: its namespace, name, the member names of the class and its base classes, and where it is declared.</summary>
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
                    && NamespaceOf(c) is { } ns && ns.StartsWith(RelicPassiveNamespace, StringComparison.Ordinal),
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

    /// <summary>The partial class as a <see cref="Declared"/>, or null for a class not deriving from <c>RelicPassive</c>.</summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        bool relicPassive = false;
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        for (var t = type; t != null; t = t.BaseType)
        {
            if (!SymbolEqualityComparer.Default.Equals(t, type) && t.ToDisplayString() == RelicPassiveType) relicPassive = true;
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
        if (!relicPassive) return null;
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

        var passives = new List<RelicPassiveDocument>();
        var items = new List<(int PassiveId, Item Item)>();
        if (entities != null)
        {
            foreach (var line in entities.Lines)
            {
                ct.ThrowIfCancellationRequested();
                string text = line.ToString();
                // Only relic passive lines and the item lines granting a relic passive are read in full.
                bool passive = text.Contains("\"$type\":\"relic-passive\"");
                bool item = text.Contains("\"$type\":\"item\"") && text.Contains("\"grants\":{\"type\":\"relic-passive\"");
                if (!passive && !item) continue;
                EntityDocument? document;
                try
                {
                    document = JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options);
                }
                catch (JsonException)
                {
                    continue;
                }
                switch (document)
                {
                    case RelicPassiveDocument p when (p.Name ?? "").Trim().Length > 0:
                        passives.Add(p);
                        break;
                    case ItemDocument { Grants: { Type: EntityType.RelicPassive } grants } i when (i.Name ?? "").Trim().Length > 0:
                        items.Add((grants.Id, new Item(i.Id, i.Name!.Trim(), i.Slot ?? "")));
                        break;
                }
            }
        }

        var records = new List<Record>();
        foreach (var p in passives.OrderBy(p => p.Id))
        {
            string name = p.Name!.Trim();
            // Each item has a line per hero: one item per ID.
            var granting = items.Where(i => i.PassiveId == p.Id).Select(i => i.Item).Distinct().OrderBy(i => i.Id).ToList();
            records.Add(new Record(p.Id, name, PascalCase(name), new([.. granting]), new([.. FieldsOf(p)]), new([.. TuningsOf(p)])));
        }
        return new Export(new([.. records]), buildDate);
    }

    /// <summary>
    /// The record's scalar fields, each under <c>RelicPassive</c> and the field's name. A record with one proc roll has its
    /// procs per minute as <c>RelicPassiveProcsPerMinute</c>.
    /// </summary>
    private static List<Constant> FieldsOf(RelicPassiveDocument p)
    {
        var fields = new List<Constant>();
        AddNumber(fields, "RelicPassiveCooldown", p.Cooldown, $"Game data: <c>cooldown</c> of relic passive {p.Id}.");
        if (p.ProcRolls is [{ ProcsPerMinute: { } ppm } roll])
        {
            string formula = roll.Formula is { Length: > 0 } f ? $" ({Escape(f)})" : "";
            AddNumber(fields, "RelicPassiveProcsPerMinute", ppm,
                $"Game data: <c>procsPerMinute</c> of proc roll <c>{Escape(roll.Row ?? "")}</c> of relic passive {p.Id}{formula}.");
        }
        return fields;
    }

    private static void AddNumber(List<Constant> fields, string name, double? value, string summary)
    {
        if (value is not { } v) return;
        bool whole = v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;
        fields.Add(new Constant(name, whole ? "int" : "double", whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : Number(v), summary));
    }

    /// <summary>The record's tuning values, one per name, in the order of the game data.</summary>
    private static List<Constant> TuningsOf(RelicPassiveDocument p)
    {
        var tunings = new List<Constant>();
        foreach (var group in (p.Tuning ?? new Dictionary<string, TuningReference>()).GroupBy(t => t.Value.Name ?? ""))
        {
            if (group.Key.Length == 0) continue;
            var values = group.Select(t => ValueOf(t.Value.Value)).Distinct().ToList();
            string keys = string.Join(", ", group.Select(t => t.Key).OrderBy(k => k.Length).ThenBy(k => k, StringComparer.Ordinal));
            if (values.Count != 1 || values[0] is not { } value)
            {
                // Two values under one name: no constant, and the diagnostic names the keys.
                tunings.Add(new Constant(group.Key, "", "", "tuning " + keys));
                continue;
            }
            string source = SourceOf(group.First().Value);
            tunings.Add(new Constant(group.Key, value.Type, value.Literal,
                $"Game data: tuning <c>{Escape(keys)}</c> of relic passive {p.Id}{(source.Length > 0 ? "; " + Escape(source) : "")}."));
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

    /// <summary>
    /// The item constants: <c>&lt;Slot&gt;ItemId</c> and <c>&lt;Slot&gt;ItemName</c> per item ID. An item ID with several
    /// names, or a slot with several items, is a constant with no type, and its summary names them.
    /// </summary>
    private static List<Constant> ItemsOf(Record record)
    {
        var constants = new List<Constant>();
        foreach (var bySlot in record.Items.GroupBy(i => i.Slot))
        {
            string prefix = PascalCase(bySlot.Key) + "Item";
            var ids = bySlot.Select(i => i.Id).Distinct().ToList();
            var names = bySlot.Select(i => i.Name).Distinct().ToList();
            if (ids.Count != 1 || names.Count != 1)
            {
                string all = string.Join(", ", bySlot.Select(i => $"item {i.Id} ({i.Name})"));
                constants.Add(new Constant(prefix + "Id", "", "", all));
                constants.Add(new Constant(prefix + "Name", "", "", all));
                continue;
            }
            var item = bySlot.First();
            constants.Add(new Constant(prefix + "Id", "int", item.Id.ToString(CultureInfo.InvariantCulture),
                $"{Escape(item.Name)}, item {item.Id} of the game data, granting relic passive {record.Id}."));
            constants.Add(new Constant(prefix + "Name", "string", Literal(item.Name), $"Game data: <c>name</c> of item {item.Id}."));
        }
        return constants;
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
            var all = new List<Constant>
            {
                new("RelicPassiveId", "int", record.Id.ToString(CultureInfo.InvariantCulture), $"{Escape(record.Name)}, relic passive {record.Id} of the game data."),
                new("RelicPassiveName", "string", Literal(record.Name), $"Game data: <c>name</c> of relic passive {record.Id}."),
            };
            all.AddRange(record.Fields);
            var names = new HashSet<string>(all.Select(c => c.Name), StringComparer.Ordinal);
            foreach (var constant in ItemsOf(record).Concat(record.Tunings))
            {
                if (constant.Type.Length == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(SharedName, d.Location, record.Id, constant.Summary, constant.Name));
                    continue;
                }
                if (!names.Add(constant.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, constant.Name, record.Id));
                    continue;
                }
                all.Add(constant);
            }

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(d.Namespace).Append(";\n\n")
                .Append("partial class ").Append(d.Name).Append("\n{\n");
            bool first = true;
            foreach (var constant in all)
            {
                if (members.Contains(constant.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, constant.Name, record.Id));
                    continue;
                }
                if (!first) source.Append('\n');
                first = false;
                source.Append("    /// <summary>").Append(constant.Summary).Append("</summary>\n")
                    .Append("    public const ").Append(constant.Type).Append(' ').Append(constant.Name).Append(" = ").Append(constant.Literal).Append(";\n");
            }
            if (members.Contains("RelicPassiveItems"))
            {
                context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "RelicPassiveItems", record.Id));
            }
            else
            {
                if (!first) source.Append('\n');
                source.Append("    /// <summary>Game data: the items granting relic passive ").Append(record.Id).Append(".</summary>\n")
                    .Append("    public static readonly ").Append(RelicItemType).Append("[] RelicPassiveItems =\n    [\n");
                foreach (var item in record.Items)
                {
                    source.Append("        new(").Append(item.Id).Append(", ").Append(Literal(item.Name)).Append("),\n");
                }
                source.Append("    ];\n");
            }
            source.Append("}\n");
            context.AddSource($"RelicPassives.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
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
            // An apostrophe stays inside its word: Drakheim's is Drakheims.
            else if (c != '\'') start = true;
        }
        return result.ToString();
    }

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
