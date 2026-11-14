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
/// Writes the game data's half of each hero class: for each <c>partial</c> class deriving from
/// <c>FellowshipSim.Core.Domain.Hero</c> in a namespace <c>FellowshipSim.Core.Heroes.&lt;Hero&gt;</c> or one inside it, a
/// <c>partial</c> of the same class with the values of its hero record. The record is the hero whose name in Pascal case
/// (letters and digits only, an apostrophe inside its word) is the class name. The constants are <c>HeroId</c>,
/// <c>HeroName</c>, one per scalar field of the record under <c>Hero</c> and the field's name (<c>HeroTitle</c>,
/// <c>HeroMainStat</c>, <c>HeroRole</c>...; <c>HeroPortrait</c> and <c>HeroRoleIcon</c> are full size image URLs), one per base attribute under <c>Hero</c> and the attribute's name
/// (<c>HeroBalancingDamageAura</c>, <c>HeroMaxResources</c>, <c>HeroMaxMana</c>...), and the hero's tuning values in a nested
/// <c>Constants</c> under their <see cref="TuningNames"/> names, with the record itself as the record's hero. A tuning value
/// of the record is the hero's when it has no owner or the hero is its owner. <c>HeroTalentTree</c> has the hero's talent
/// slot records row by row and column by column, and <c>HeroWeapons</c> each weapon item's name by the ability its weapon
/// power grants, also the <c>WeaponNames</c> override of <c>Hero</c>. The class's own part has the hero's behaviour.
/// <c>GameHeroData</c>'s <c>MainStats</c>, <c>TalentTrees</c> and <c>Weapons</c> have the same for each hero record with
/// no class, by hero name. A member of the class or a base class with a constant's name, or the name <c>Constants</c>, is
/// CODEX033.
/// The game data is the AdditionalFiles <c>entities.jsonl</c> and <c>settings.json</c>, read through
/// FellowshipAnalyzer.Codex.SDK; <see cref="CreatureGenerator"/> reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class HeroGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string HeroesNamespace = "FellowshipSim.Core.Heroes.";
    private const string HeroType = "FellowshipSim.Core.Domain.Hero";
    private const string MainStatType = "global::FellowshipSim.Core.Domain.MainStat";

    private static readonly DiagnosticDescriptor NoHero = new(
        "CODEX030", "A partial hero class names no hero record",
        "The partial class {0} names no hero record in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralHeroes = new(
        "CODEX031", "A partial hero class names several hero records",
        "The partial class {0} names the hero records {1} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX033", "A hero constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, and {1} is a constant of hero {2}; rename the member", "Codex", DiagnosticSeverity.Error, true);

    /// <summary>A constant of a hero record: its name, C# type and literal, and its summary.</summary>
    private sealed record Constant(string Name, string Type, string Literal, string Summary);

    /// <summary>
    /// A hero record: its ID, name, class name, its field constants, its tuning values, its main stat's literal (empty for
    /// none), its talent slots' <c>TalentSlot</c> literals row by row and column by column, and its weapons (each weapon
    /// item's name by the ability its weapon power grants, in ability order).
    /// </summary>
    private sealed record Record(int Id, string Name, string ClassName, EquatableArray<Constant> Fields, EquatableArray<TuningEntry> Tunings,
        string MainStat, EquatableArray<string> TalentTree, EquatableArray<TableEntry> Weapons);

    /// <summary>The game data's hero records, the tuning keys' first segments naming a hero, and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, EquatableArray<HeroSegment> Segments, string? BuildDate);

    /// <summary>
    /// A partial hero class: its namespace, name, the member names of the class and its base classes, the class's constants
    /// and static readonly fields, and where it is declared.
    /// </summary>
    private sealed record Declared(string Namespace, string Name, EquatableArray<string> Members, EquatableArray<string> Fields, Location? Location)
    {
        public bool Equals(Declared? other) => other != null && Namespace == other.Namespace && Name == other.Name && Members.Equals(other.Members)
            && Fields.Equals(other.Fields);

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

    /// <summary>The partial class as a <see cref="Declared"/>, or null for a class not deriving from <c>Hero</c>.</summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        bool hero = false;
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        for (var t = type; t != null; t = t.BaseType)
        {
            if (!SymbolEqualityComparer.Default.Equals(t, type) && t.ToDisplayString() == HeroType) hero = true;
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
        if (!hero) return null;
        return new Declared(NamespaceOf(node)!, type.Name, new([.. members.OrderBy(m => m, StringComparer.Ordinal)]), new([.. TuningNames.FieldsOf(type)]),
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

        var heroes = new List<HeroDocument>();
        var slots = new List<TalentSlotDocument>();
        var powerAbilities = new Dictionary<int, int>();
        var weaponItems = new List<ItemDocument>();
        if (entities != null)
        {
            foreach (var line in entities.Lines)
            {
                ct.ThrowIfCancellationRequested();
                string text = line.ToString();
                // Only hero, talent slot and weapon power lines, and the item lines granting a weapon power, are read in full.
                if (!text.Contains("\"$type\":\"hero\"") && !text.Contains("\"$type\":\"talent-slot\"") && !text.Contains("\"$type\":\"weapon-power\"")
                    && !(text.Contains("\"$type\":\"item\"") && text.Contains("\"grants\":{\"type\":\"weapon-power\""))) continue;
                try
                {
                    switch (JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options))
                    {
                        case HeroDocument h when (h.Name ?? "").Trim().Length > 0:
                            heroes.Add(h);
                            break;
                        case TalentSlotDocument s:
                            slots.Add(s);
                            break;
                        case WeaponPowerDocument p when p.Grants?.FirstOrDefault(g => g.Type == EntityType.Ability) is { } ability:
                            powerAbilities[p.Id] = ability.Id;
                            break;
                        case ItemDocument { Grants.Type: EntityType.WeaponPower, Hero: not null } i:
                            weaponItems.Add(i);
                            break;
                    }
                }
                catch (JsonException)
                {
                }
            }
        }

        var records = new List<Record>();
        foreach (var h in heroes.OrderBy(h => h.Id))
        {
            string name = h.Name!.Trim();
            var fields = FieldsOf(h);
            var tree = slots.Where(s => s.Hero.ToString() == h.Name).OrderBy(s => s.Row).ThenBy(s => s.Column)
                .Select(s => $"new({s.Row}, {s.Column}, {s.Cost}, {s.Talent.Id})");
            // A weapon item grants a weapon power, and the power its ability.
            var weapons = weaponItems.Where(i => i.Hero.ToString() == h.Name && powerAbilities.ContainsKey(i.Grants!.Id))
                .Select(i => (Ability: powerAbilities[i.Grants!.Id], Name: i.Name ?? ""))
                .GroupBy(w => w.Ability).Select(g => g.First()).OrderBy(w => w.Ability)
                .Select(w => new TableEntry(w.Ability.ToString(CultureInfo.InvariantCulture), Literal(w.Name)));
            records.Add(new Record(h.Id, name, PascalCase(name), new([.. fields]), new([.. TuningsOf(h)]),
                fields.FirstOrDefault(f => f.Name == "HeroMainStat")?.Literal ?? "", new([.. tree]), new([.. weapons])));
        }
        return new Export(new([.. records]), HeroSegments.Of(entities, ct), buildDate);
    }

    /// <summary>
    /// The record's scalar fields, each under <c>Hero</c> and the field's name, then its base attributes, each under
    /// <c>Hero</c> and the attribute's name, in the order of the game data.
    /// </summary>
    private static List<Constant> FieldsOf(HeroDocument h)
    {
        var fields = new List<Constant>();
        AddString(fields, "Title", "title", h.Title, h.Id);
        if (h.MainStat is { } mainStat)
        {
            fields.Add(new Constant("HeroMainStat", MainStatType, MainStatType + "." + mainStat, Field("mainStat", h.Id)));
        }
        AddString(fields, "Color", "color", h.Color, h.Id);
        AddTexture(fields, "Portrait", "portrait", h.Portrait, h.Id);
        AddString(fields, "Role", "role", h.Role, h.Id);
        AddTexture(fields, "RoleIcon", "roleIcon", h.RoleIcon, h.Id);
        AddNumber(fields, "OverallDifficulty", "overallDifficulty", h.OverallDifficulty, h.Id);
        AddNumber(fields, "Offense", "offense", h.Offense, h.Id);
        AddNumber(fields, "Defense", "defense", h.Defense, h.Id);
        AddNumber(fields, "Control", "control", h.Control, h.Id);
        if (h.EnabledInGame is { } enabled) fields.Add(new Constant("HeroEnabledInGame", "bool", enabled ? "true" : "false", Field("enabledInGame", h.Id)));
        foreach (var attribute in h.BaseAttributes ?? new Dictionary<string, double>())
        {
            string name = PascalCase(attribute.Key);
            if (name.Length == 0) continue;
            AddNumber(fields, name, "baseAttributes." + attribute.Key, attribute.Value, h.Id);
        }
        return fields;
    }

    private static string Field(string field, int id) => $"Game data: <c>{Escape(field)}</c> of hero {id}.";

    private static void AddString(List<Constant> fields, string name, string field, string? value, int id)
    {
        if (value is not { Length: > 0 } v) return;
        fields.Add(new Constant("Hero" + name, "string", Literal(v), Field(field, id)));
    }

    private static void AddTexture(List<Constant> fields, string name, string field, string? texture, int id)
    {
        if (texture is not { Length: > 0 } t) return;
        fields.Add(new Constant("Hero" + name, "string", Literal(Textures.Url(t)), $"Game data: the full size image URL of <c>{Escape(field)}</c> of hero {id}."));
    }

    private static void AddNumber(List<Constant> fields, string name, string field, double? value, int id)
    {
        if (value is not { } v) return;
        bool whole = v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;
        fields.Add(new Constant("Hero" + name, whole ? "int" : "double",
            whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : Number(v), Field(field, id)));
    }

    /// <summary>
    /// The record's tuning keys, in the order of the game data. A tuning value owned by another record (an ability or talent
    /// of the hero) is that record's class's.
    /// </summary>
    private static List<TuningEntry> TuningsOf(HeroDocument hero)
    {
        var own = (hero.Tuning ?? new Dictionary<string, TuningReference>())
            .Where(t => t.Value.Owner is null || (t.Value.Owner.Type == EntityType.Hero && t.Value.Owner.Id == hero.Id));
        return TuningNames.Entries(own, v => ValueOf(v) is { } value ? (value.Type, value.Literal, false) : null, SourceOf);
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

    /// <summary>The tuning value's attribute and operation where it has them.</summary>
    private static string SourceOf(TuningReference tuning) =>
        tuning.Attribute is { Length: > 0 } attribute ? (tuning.Op is { Length: > 0 } op ? $"{attribute} {op}" : attribute) : "";

    private static void Write(SourceProductionContext context, Export export, ImmutableArray<Declared> declared)
    {
        var byClass = export.Records.ToLookup(r => r.ClassName);
        // The IDs of the hero records with a class.
        var matched = new HashSet<int>();
        foreach (var d in declared.GroupBy(d => (d.Namespace, d.Name)).Select(g => g.First())
                     .OrderBy(d => d.Namespace, StringComparer.Ordinal).ThenBy(d => d.Name, StringComparer.Ordinal))
        {
            var candidates = byClass[d.Name].ToList();
            if (candidates.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoHero, d.Location, d.Name));
                continue;
            }
            if (candidates.Count != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(SeveralHeroes, d.Location, d.Name, string.Join(", ", candidates.Select(r => r.Id))));
                continue;
            }

            var record = candidates[0];
            matched.Add(record.Id);
            var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
            var constants = new List<Constant>
            {
                new("HeroId", "int", record.Id.ToString(CultureInfo.InvariantCulture), $"{Escape(record.Name)}, hero {record.Id} of the game data."),
                new("HeroName", "string", Literal(record.Name), $"Game data: <c>name</c> of hero {record.Id}."),
            };
            constants.AddRange(record.Fields);
            var shared = new List<(string Name, string First, string Second)>();
            var tunings = TuningNames.Of(record.Tunings, export.Segments, [record.Name], shared);
            TuningNames.Report(context, d.Location, d.Name, $"hero {record.Id}", shared, d.Fields, tunings);

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(d.Namespace).Append(";\n\n")
                .Append("partial class ").Append(d.Name).Append("\n{\n");
            bool first = true;
            foreach (var constant in constants)
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
            if (members.Contains("HeroTalentTree"))
            {
                context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "HeroTalentTree", record.Id));
            }
            else
            {
                if (!first) source.Append('\n');
                first = false;
                source.Append("    /// <summary>Game data: the talent slot records of hero ").Append(record.Id).Append(", row by row and column by column.</summary>\n")
                    .Append("    public static readonly global::FellowshipSim.Core.Domain.TalentSlot[] HeroTalentTree =\n    [\n")
                    .Append(string.Concat(record.TalentTree.Select(s => "        " + s + ",\n"))).Append("    ];\n");
            }
            bool weapons = !members.Contains("HeroWeapons");
            if (!weapons)
            {
                context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "HeroWeapons", record.Id));
            }
            else
            {
                if (!first) source.Append('\n');
                first = false;
                source.Append("    /// <summary>Game data: the weapons of hero ").Append(record.Id)
                    .Append(", each weapon item's name by the ability its weapon power grants.</summary>\n")
                    .Append("    public static readonly global::System.Collections.Generic.IReadOnlyDictionary<int, string> HeroWeapons = ")
                    .Append(WeaponsLiteral(record, "    ")).Append(";\n");
            }
            if (tunings.Count > 0)
            {
                if (members.Contains("Constants"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "Constants", record.Id));
                }
                else
                {
                    TuningNames.Write(source, $"Game data: the tuning values of hero {record.Id}.",
                        tunings.Select(t => (t, $"Game data: tuning <c>{Escape(t.Keys)}</c> of hero {record.Id}{(t.Source.Length > 0 ? "; " + Escape(t.Source) : "")}.")),
                        false, !first);
                }
            }
            if (weapons) source.Append("\n    public override global::System.Collections.Generic.IReadOnlyDictionary<int, string> WeaponNames => HeroWeapons;\n");
            source.Append("}\n");
            context.AddSource($"Heroes.{d.Namespace.Substring(HeroesNamespace.Length)}.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        // The heroes with no class: their main stats, talent trees and weapons.
        var unmatched = export.Records.Where(r => !matched.Contains(r.Id)).OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
        context.AddSource("GameHeroData.g.cs", SourceText.From(GameTables.Write("GameHeroData",
            "The heroes of the game data with no hero class: each one's main stat, talent tree and weapons. <see cref=\"BuildHeroes\"/>\n" +
            "takes them for a hero the engine does not have yet; a hero class has its own in its generated half.",
        [
            new GameTable("MainStats", "Each hero's main stat, by hero name.", "string", MainStatType, true,
                new([.. unmatched.Where(r => r.MainStat.Length > 0).Select(r => new TableEntry(Literal(r.Name), r.MainStat))])),
            new GameTable("TalentTrees", "Each hero's talent tree, row by row and column by column, by hero name.", "string",
                "global::FellowshipSim.Core.Domain.TalentSlot[]", true,
                new([.. unmatched.Select(r => new TableEntry(Literal(r.Name),
                    "\n        [\n" + string.Concat(r.TalentTree.Select(s => "            " + s + ",\n")) + "        ]"))])),
            new GameTable("Weapons", "Each hero's weapons: each weapon item's name by the ability its weapon power grants, by hero name.", "string",
                "global::System.Collections.Generic.IReadOnlyDictionary<int, string>", true,
                new([.. unmatched.Select(r => new TableEntry(Literal(r.Name), WeaponsLiteral(r, "        ")))])),
        ], export.BuildDate), Encoding.UTF8));
    }

    /// <summary>A <c>Dictionary&lt;int, string&gt;</c> expression of the hero's weapons, its braces at <paramref name="indent"/>.</summary>
    private static string WeaponsLiteral(Record record, string indent) =>
        "new global::System.Collections.Generic.Dictionary<int, string>\n" + indent + "{\n"
        + string.Concat(record.Weapons.Select(w => $"{indent}    [{w.Key}] = {w.Value},\n")) + indent + "}";

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
