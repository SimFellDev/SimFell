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
/// Writes a <c>public partial class &lt;Name&gt;GameDungeon</c> in <c>FellowshipSim.Core.Domain</c> for each dungeon of the
/// game data, deriving from <c>GameDungeon</c>, with the values of its dungeon record. A class name is the dungeon's name in
/// Pascal case (letters and digits only, an apostrophe inside its word) and <c>GameDungeon</c>; the dungeons sharing a name,
/// a dungeon without one and a name starting with a digit have the dungeon's ID after the name. The constants are
/// <c>DungeonId</c> (a FellowshipLogs fight's <c>gameZone</c> ID), <c>DungeonName</c>, one per scalar field of the record
/// under <c>Dungeon</c> and the field's name (<c>DungeonLevelType</c>, <c>DungeonQualifierTime</c>,
/// <c>DungeonRequiredScore</c>...; <c>DungeonIcon</c> and <c>DungeonBackground</c> are full size image URLs), and the record's tuning values in a nested <c>Constants</c> under their
/// <see cref="TuningNames"/> names, with no hero as the record's hero.
/// <c>DungeonDifficulties</c> is a <c>public static readonly int[]</c> of the record's difficulties.
/// <c>DungeonPlacements</c> is a <c>public static readonly CodexPlacement[]</c> of its creature placements, each creature by
/// the <c>ID</c> of its <c>&lt;Name&gt;GameCreature</c> class (<see cref="CreatureClassNames"/>) and a factory
/// <c>() =&gt; new &lt;Name&gt;GameCreature()</c>. The class overrides
/// <c>Id</c>, <c>Name</c> and <c>Placements</c> with them. A dungeon's mechanics go in a hand-written <c>partial</c> of its
/// class, and a member of that part or a base class with a constant's name, or the name <c>Constants</c>, is CODEX036. A <c>partial</c> of
/// <c>GameDungeon</c> has <c>All</c>, an instance of every dungeon class by ID, and <c>ById</c>. The game data is the
/// AdditionalFiles <c>entities.jsonl</c> and <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK;
/// <see cref="CreatureGenerator"/> reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DungeonGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string Namespace = "FellowshipSim.Core.Domain";
    private const string ClassSuffix = "GameDungeon";

    private static readonly DiagnosticDescriptor NoRecord = new(
        "CODEX034", "A partial dungeon class names no dungeon record",
        "The partial class {0}.{1} names no dungeon record in the game data; a dungeon class is in " + Namespace, "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX036", "A dungeon constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, and {1} is a constant of dungeon {2}; rename the member", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NoCreature = new(
        "CODEX037", "A dungeon placement names no creature record",
        "Dungeon {0} places creatures {1}, which the game data has no creature record for; the class has no placement of them", "Codex", DiagnosticSeverity.Warning, true);

    /// <summary>
    /// A constant of a dungeon record: its name, C# type and literal, and its summary. A constant of an array type is a
    /// <c>static readonly</c> field.
    /// </summary>
    private sealed record Constant(string Name, string Type, string Literal, string Summary);

    /// <summary>A creature placement: the creature's class name, its world position's x and y, and its patrol path's index.</summary>
    private sealed record Placement(string CreatureClass, double X, double Y, int? Patrol);

    /// <summary>
    /// A dungeon record: its ID, name, class name, field constants, tuning values, creature placements, and the IDs of the
    /// placed creatures with no creature record.
    /// </summary>
    private sealed record Record(int Id, string Name, string ClassName, EquatableArray<Constant> Fields, EquatableArray<TuningEntry> Tunings,
        EquatableArray<Placement> Placements, EquatableArray<int> UnknownCreatures);

    /// <summary>The game data's dungeon records, the tuning keys' first segments naming a hero, and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, EquatableArray<HeroSegment> Segments, string? BuildDate);

    /// <summary>
    /// A hand-written partial dungeon class: its namespace, name, the member names of the class and its base classes, the
    /// class's constants and static readonly fields, and where it is declared.
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
        // The generated half declares the base class, so a hand-written half is known by its name alone.
        var declared = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax c && c.Modifiers.Any(SyntaxKind.PartialKeyword) && c.Parent is BaseNamespaceDeclarationSyntax
                    && c.Identifier.ValueText.EndsWith(ClassSuffix, StringComparison.Ordinal) && c.Identifier.ValueText != ClassSuffix,
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

    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        for (var t = type; t != null; t = t.BaseType)
        {
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
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

        var dungeons = new List<DungeonDocument>();
        var creatures = new List<(int Id, string Name)>();
        if (entities != null)
        {
            foreach (var line in entities.Lines)
            {
                ct.ThrowIfCancellationRequested();
                string text = line.ToString();
                // Only dungeon and creature lines are read in full; CreatureGenerator reports a line that does not read.
                bool dungeon = text.Contains("\"$type\":\"dungeon\"");
                if (!dungeon && !text.Contains("\"$type\":\"creature\"")) continue;
                try
                {
                    switch (JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options))
                    {
                        case DungeonDocument d:
                            dungeons.Add(d);
                            break;
                        case CreatureDocument c:
                            creatures.Add((c.Id, (c.Name ?? "").Trim()));
                            break;
                    }
                }
                catch (JsonException)
                {
                }
            }
        }

        var creatureClass = CreatureClassNames.Of(creatures.OrderBy(c => c.Id));
        var shared = new HashSet<string>(dungeons.GroupBy(d => CreatureClassNames.PascalCase((d.Name ?? "").Trim())).Where(g => g.Count() > 1).Select(g => g.Key),
            StringComparer.Ordinal);
        var records = new List<Record>();
        foreach (var d in dungeons.OrderBy(d => d.Id))
        {
            string name = (d.Name ?? "").Trim();
            string className = CreatureClassNames.PascalCase(name);
            if (className.Length == 0 || shared.Contains(className) || !char.IsLetter(className[0])) className = $"{(className.Length == 0 ? "Dungeon" : className)}{d.Id}";
            var placements = new List<Placement>();
            var unknown = new SortedSet<int>();
            foreach (var p in d.Placements ?? [])
            {
                if (p.Creature == null || p.Position == null) continue;
                if (creatureClass.TryGetValue(p.Creature.Id, out var c)) placements.Add(new Placement(c, p.Position.X, p.Position.Y, p.Patrol));
                else unknown.Add(p.Creature.Id);
            }
            records.Add(new Record(d.Id, name, className + ClassSuffix, new([.. FieldsOf(d)]), new([.. TuningsOf(d)]), new([.. placements]),
                new([.. unknown])));
        }
        return new Export(new([.. records]), HeroSegments.Of(entities, ct), buildDate);
    }

    /// <summary>The record's scalar fields, each under <c>Dungeon</c> and the field's name, then its difficulties.</summary>
    private static List<Constant> FieldsOf(DungeonDocument d)
    {
        var fields = new List<Constant>();
        AddString(fields, "Description", "description", d.Description, d.Id);
        AddTexture(fields, "Icon", "icon", d.Icon, d.Id);
        AddTexture(fields, "Background", "background", d.Background, d.Id);
        AddNumber(fields, "MaxPlayers", "maxPlayers", d.MaxPlayers, d.Id);
        AddNumber(fields, "QualifierTime", "qualifierTime", d.QualifierTime, d.Id);
        AddNumber(fields, "RequiredScore", "requiredScore", d.RequiredScore, d.Id);
        AddNumber(fields, "MinimumHeroRank", "minimumHeroRank", d.MinimumHeroRank, d.Id);
        AddString(fields, "LevelType", "levelType", d.LevelType, d.Id);
        AddBool(fields, "SeasonalPinnacle", "seasonalPinnacle", d.SeasonalPinnacle, d.Id);
        AddBool(fields, "AvailableForCasual", "availableForCasual", d.AvailableForCasual, d.Id);
        AddNumber(fields, "MinimumDifficulty", "minimumDifficulty", d.MinimumDifficulty, d.Id);
        AddBool(fields, "EnabledInGame", "enabledInGame", d.EnabledInGame, d.Id);
        if (d.Difficulties is { Count: > 0 } difficulties)
        {
            fields.Add(new Constant("DungeonDifficulties", "int[]",
                "[" + string.Join(", ", difficulties.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]", Field("difficulties", d.Id)));
        }
        return fields;
    }

    private static string Field(string field, int id) => $"Game data: <c>{Escape(field)}</c> of dungeon {id}.";

    private static void AddString(List<Constant> fields, string name, string field, string? value, int id)
    {
        if (value is not { Length: > 0 } v) return;
        fields.Add(new Constant("Dungeon" + name, "string", Literal(v), Field(field, id)));
    }

    private static void AddTexture(List<Constant> fields, string name, string field, string? texture, int id)
    {
        if (texture is not { Length: > 0 } t) return;
        fields.Add(new Constant("Dungeon" + name, "string", Literal(Textures.Url(t)), $"Game data: the full size image URL of <c>{Escape(field)}</c> of dungeon {id}."));
    }

    private static void AddBool(List<Constant> fields, string name, string field, bool? value, int id)
    {
        if (value is not { } v) return;
        fields.Add(new Constant("Dungeon" + name, "bool", v ? "true" : "false", Field(field, id)));
    }

    private static void AddNumber(List<Constant> fields, string name, string field, double? value, int id)
    {
        if (value is not { } v) return;
        bool whole = v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;
        fields.Add(new Constant("Dungeon" + name, whole ? "int" : "double",
            whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : Number(v), Field(field, id)));
    }

    /// <summary>The record's tuning keys, in the order of the game data.</summary>
    private static List<TuningEntry> TuningsOf(DungeonDocument d) =>
        TuningNames.Entries(d.Tuning, v => ValueOf(v) is { } value ? (value.Type, value.Literal, false) : null, SourceOf);

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

    private static void Write(SourceProductionContext context, Export export, ImmutableArray<Declared> declared)
    {
        var byClass = export.Records.ToDictionary(r => r.ClassName, StringComparer.Ordinal);
        var parts = new Dictionary<string, Declared>(StringComparer.Ordinal);
        foreach (var d in declared.GroupBy(d => (d.Namespace, d.Name)).Select(g => g.First()))
        {
            if (d.Namespace == Namespace && byClass.ContainsKey(d.Name))
            {
                parts[d.Name] = d;
                continue;
            }
            context.ReportDiagnostic(Diagnostic.Create(NoRecord, d.Location, d.Namespace, d.Name));
        }

        foreach (var record in export.Records)
        {
            parts.TryGetValue(record.ClassName, out var part);
            var location = part?.Location ?? Location.None;
            var members = new HashSet<string>(part?.Members ?? new EquatableArray<string>([]), StringComparer.Ordinal);
            if (record.UnknownCreatures.Any())
            {
                context.ReportDiagnostic(Diagnostic.Create(NoCreature, location, record.Id, string.Join(", ", record.UnknownCreatures)));
            }

            var constants = new List<Constant>
            {
                new("DungeonId", "int", record.Id.ToString(CultureInfo.InvariantCulture),
                    $"{Title(record)} of the game data: a FellowshipLogs fight's gameZone ID."),
                new("DungeonName", "string", Literal(record.Name), $"Game data: <c>name</c> of dungeon {record.Id}."),
            };
            constants.AddRange(record.Fields);
            var shared = new List<(string Name, string First, string Second)>();
            var tunings = TuningNames.Of(record.Tunings, export.Segments, [], shared);
            TuningNames.Report(context, location, record.ClassName, $"dungeon {record.Id}", shared, part?.Fields ?? new EquatableArray<string>([]), tunings);

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(Namespace).Append(";\n\n")
                .Append("/// <summary>").Append(Title(record)).Append(" of the game data.</summary>\n")
                .Append("public partial class ").Append(record.ClassName).Append(" : GameDungeon\n{\n");
            foreach (var constant in constants)
            {
                if (members.Contains(constant.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, location, record.ClassName, constant.Name, record.Id));
                    continue;
                }
                source.Append("    /// <summary>").Append(constant.Summary).Append("</summary>\n")
                    .Append(constant.Type.EndsWith("[]", StringComparison.Ordinal) ? "    public static readonly " : "    public const ")
                    .Append(constant.Type).Append(' ').Append(constant.Name).Append(" = ").Append(constant.Literal).Append(";\n\n");
            }
            if (members.Contains("DungeonPlacements"))
            {
                context.ReportDiagnostic(Diagnostic.Create(DeclaredName, location, record.ClassName, "DungeonPlacements", record.Id));
            }
            else
            {
                source.Append("    /// <summary>Game data: <c>placements</c> of dungeon ").Append(record.Id).Append(", each creature by its class's ID and a factory of the class.</summary>\n")
                    .Append("    public static readonly CodexPlacement[] DungeonPlacements =\n    [\n");
                foreach (var p in record.Placements)
                {
                    source.Append("        new(").Append(p.CreatureClass).Append(".ID, () => new ").Append(p.CreatureClass).Append("(), ").Append(Number(p.X)).Append(", ").Append(Number(p.Y))
                        .Append(", ").Append(p.Patrol?.ToString(CultureInfo.InvariantCulture) ?? "null").Append("),\n");
                }
                source.Append("    ];\n\n");
            }
            if (tunings.Count > 0)
            {
                if (members.Contains("Constants"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, location, record.ClassName, "Constants", record.Id));
                }
                else
                {
                    TuningNames.Write(source, $"Game data: the tuning values of dungeon {record.Id}.",
                        tunings.Select(t => (t, $"Game data: tuning <c>{Escape(t.Keys)}</c> of dungeon {record.Id}{(t.Source.Length > 0 ? "; " + Escape(t.Source) : "")}.")),
                        false, false);
                    source.Append('\n');
                }
            }
            source.Append("    public override int Id => DungeonId;\n\n")
                .Append("    public override string Name => DungeonName;\n\n")
                .Append("    public override IReadOnlyList<CodexPlacement> Placements => DungeonPlacements;\n")
                .Append("}\n");
            context.AddSource($"Dungeons.{record.ClassName}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }
        WriteList(context, export);
    }

    // GameDungeon's list of every dungeon class, by ID.
    private static void WriteList(SourceProductionContext context, Export export)
    {
        var source = new StringBuilder()
            .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data. </auto-generated>\n")
            .Append("#nullable enable\n")
            .Append("namespace ").Append(Namespace).Append(";\n\n")
            .Append("partial class GameDungeon\n{\n")
            .Append("    /// <summary>Every dungeon of the game data, by ID.</summary>\n")
            .Append("    public static IReadOnlyList<GameDungeon> All { get; } =\n    [\n");
        foreach (var record in export.Records)
        {
            source.Append("        new ").Append(record.ClassName).Append("(),\n");
        }
        source.Append("    ];\n\n")
            .Append("    private static readonly Dictionary<int, GameDungeon> _byId = All.ToDictionary(d => d.Id);\n\n")
            .Append("    /// <summary>The dungeon with ID <paramref name=\"id\"/> (a FellowshipLogs fight's gameZone ID); null when there is none.</summary>\n")
            .Append("    public static GameDungeon? ById(int id) => _byId.GetValueOrDefault(id);\n")
            .Append("}\n");
        context.AddSource("GameDungeon.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    /// <summary>The dungeon's name and ID, or its ID alone for a dungeon without a name.</summary>
    private static string Title(Record record) =>
        record.Name.Length > 0 ? $"{Escape(record.Name)}, dungeon {record.Id}" : $"Dungeon {record.Id}";

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
