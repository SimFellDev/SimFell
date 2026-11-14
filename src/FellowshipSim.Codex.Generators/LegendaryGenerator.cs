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
/// Writes the game data's half of each legendary class: for each <c>partial</c> class in
/// <c>FellowshipSim.Core.Customization.Legendaries</c> named after a legendary power (the power's name in Pascal case,
/// letters and digits only), a <c>partial</c> of the same class with the power's values. These are <c>PowerId</c>,
/// <c>PowerIcon</c> (the full size image URL of the power's icon), an item ID constant and an item icon URL constant per
/// item granting the power (<c>HeadItemId</c> and <c>HeadItemIcon</c>, <c>ChestItemId</c>, <c>LegsItemId</c>: the item's
/// slot), the power's tuning values in a nested <c>Constants</c> under their <see cref="TuningNames"/> names, with the items'
/// hero as the record's hero, and the overrides of <c>Legendary</c>'s <c>LegendaryPowerId</c>, <c>Name</c>,
/// <c>HeroName</c>, <c>ItemIds</c> and <c>Icon</c> (the first item's icon URL, else the power's).
/// <c>GameIcons.LegendaryPowers</c> has the icon URLs of the powers with no class, by ID, <c>GameIcons.LegendaryItems</c> the
/// icon URL of each hero's legendary rarity items, by hero and ID, and <c>GameHeroData.Legendaries</c> the name of what each
/// of those items grants, by hero name and ID. The class's own part derives
/// from <c>Legendary</c> and has the power's behaviour. The game data is the AdditionalFiles <c>entities.jsonl</c> and
/// <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK; <see cref="CreatureGenerator"/> reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class LegendaryGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string Namespace = "FellowshipSim.Core.Customization.Legendaries";

    private static readonly DiagnosticDescriptor NoPower = new(
        "CODEX003", "A legendary class names no legendary power",
        "The partial class {0} names no legendary power of the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralHeroes = new(
        "CODEX005", "A legendary power's items name several heroes",
        "The items granting legendary power {0} name the heroes {1}", "Codex", DiagnosticSeverity.Error, true);

    /// <summary>An item granting a power: its ID, name, slot, hero and icon URL (empty for none).</summary>
    private sealed record Item(int Id, string Name, string Slot, string Hero, string Icon);

    /// <summary>
    /// A legendary power: its ID, name, icon URL (empty for none), class name, the items granting it and its tuning keys.
    /// </summary>
    private sealed record Power(int Id, string Name, string Icon, string ClassName, EquatableArray<Item> Items, EquatableArray<TuningEntry> Tunings);

    /// <summary>
    /// The game data's powers granted by an item, every power's icon URL by ID (in ID order), the legendary item tables,
    /// the tuning keys' first segments naming a hero, and its build date.
    /// </summary>
    private sealed record Export(EquatableArray<Power> Powers, EquatableArray<TableEntry> PowerIcons, EquatableArray<GameTable> ItemTables,
        EquatableArray<GameTable> HeroTables, EquatableArray<HeroSegment> Segments, string? BuildDate);

    /// <summary>
    /// A partial class in the legendaries namespace: its name, the class's constants and static readonly fields, and where
    /// it is declared.
    /// </summary>
    private sealed record Declared(string Name, EquatableArray<string> Fields, Location? Location)
    {
        public bool Equals(Declared? other) => other != null && Name == other.Name && Fields.Equals(other.Fields);

        public override int GetHashCode() => Name.GetHashCode();
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var export = context.AdditionalTextsProvider
            .Where(f => Path.GetFileName(f.Path) is EntitiesName or SettingsName)
            .Collect()
            .Select((all, ct) => Read(all, ct));
        var declared = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax c && c.Modifiers.Any(SyntaxKind.PartialKeyword) && NamespaceOf(c) == Namespace,
                static (ctx, ct) => new Declared(((ClassDeclarationSyntax)ctx.Node).Identifier.ValueText,
                    new([.. ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node, ct) is INamedTypeSymbol type ? TuningNames.FieldsOf(type) : []]),
                    ctx.Node.GetLocation()))
            .Collect();
        context.RegisterSourceOutput(export.Combine(declared), (spc, pair) => Write(spc, pair.Left, pair.Right));
    }

    private static string? NamespaceOf(SyntaxNode node)
    {
        for (var parent = node.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is BaseNamespaceDeclarationSyntax ns) return ns.Name.ToString();
        }
        return null;
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

        var powers = new List<LegendaryPowerDocument>();
        var items = new List<ItemDocument>();
        var legendaryItems = new List<ItemDocument>();
        var heroes = new List<string>();
        if (entities != null)
        {
            foreach (var line in entities.Lines)
            {
                ct.ThrowIfCancellationRequested();
                string text = line.ToString();
                // Only legendary power lines, hero lines and the legendary item lines are read in full.
                bool power = text.Contains("\"$type\":\"legendary-power\"");
                bool hero = text.Contains("\"$type\":\"hero\"");
                bool item = text.Contains("\"$type\":\"item\"")
                    && (text.Contains("\"grants\":{\"type\":\"legendary-power\"") || text.Contains("\"rarity\":\"Legendary\""));
                if (!power && !hero && !item) continue;
                try
                {
                    switch (JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options))
                    {
                        case LegendaryPowerDocument p:
                            powers.Add(p);
                            break;
                        case HeroDocument h when (h.Name ?? "").Length > 0:
                            heroes.Add(h.Name!);
                            break;
                        case ItemDocument i:
                            if (i.Grants is { Type: EntityType.LegendaryPower }) items.Add(i);
                            if (i.Rarity == Rarity.Legendary && i.Hero != null) legendaryItems.Add(i);
                            break;
                    }
                }
                catch (JsonException)
                {
                }
            }
        }

        // Each hero's legendary items: the icon of each by hero and ID, and what each grants.
        var itemIcons = legendaryItems.Where(i => i.Icon is { Length: > 0 })
            .Select(i => new TableEntry(Literal($"{i.Hero}:{i.Id}"), Literal(Textures.Url(i.Icon!))))
            .OrderBy(e => e.Key, StringComparer.Ordinal);
        var legendaries = heroes.OrderBy(h => h, StringComparer.Ordinal).Select(h => new TableEntry(Literal(h),
            "new global::System.Collections.Generic.Dictionary<int, string>\n        {\n"
            + string.Concat(legendaryItems.Where(i => i.Hero.ToString() == h && i.Grants?.Name is { Length: > 0 })
                .GroupBy(i => i.Id).Select(g => g.First()).OrderBy(i => i.Id)
                .Select(i => $"            [{i.Id.ToString(CultureInfo.InvariantCulture)}] = {Literal(i.Grants!.Name!)},\n"))
            + "        }"));
        var powerIcons = powers.OrderBy(p => p.Id).Where(p => p.Icon is { Length: > 0 })
            .Select(p => new TableEntry(p.Id.ToString(CultureInfo.InvariantCulture), Literal(Textures.Url(p.Icon!))));

        var result = new List<Power>();
        foreach (var p in powers.OrderBy(p => p.Id))
        {
            string name = (p.Name ?? "").Trim();
            var granting = items.Where(i => i.Grants!.Id == p.Id).OrderBy(i => i.Id)
                .Select(i => new Item(i.Id, (i.Name ?? "").Trim(), i.Slot ?? "", i.Hero?.ToString() ?? "", UrlOf(i.Icon)))
                .ToList();
            if (name.Length == 0 || granting.Count == 0) continue;
            result.Add(new Power(p.Id, name, UrlOf(p.Icon), PascalCase(name), new([.. granting]), new([.. TuningsOf(p)])));
        }
        return new Export(new([.. result]), new([.. powerIcons]),
            new([new GameTable("LegendaryItems", "Icon URLs of the legendary items, by hero and item ID (\"Rime:5899\").", "string", "string", true, new([.. itemIcons]))]),
            new([new GameTable("Legendaries", "Each hero's legendaries: the name of what each legendary item grants, by item ID, by hero name.", "string",
                "global::System.Collections.Generic.IReadOnlyDictionary<int, string>", true, new([.. legendaries]))]),
            HeroSegments.Of(entities, ct), buildDate);
    }

    /// <summary>The URL of a texture's full size image; empty for no texture.</summary>
    private static string UrlOf(string? texture) => texture is { Length: > 0 } ? Textures.Url(texture) : "";

    /// <summary>The power's tuning keys, in the order of the game data.</summary>
    private static List<TuningEntry> TuningsOf(LegendaryPowerDocument power) =>
        TuningNames.Entries(power.Tuning, v => ValueOf(v) is { } value ? (value.Type, value.Literal, false) : null, SourceOf);

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
            string type = owner.Type switch
            {
                EntityType.LegendaryPower => "legendary power",
                _ => owner.Type.ToString().ToLowerInvariant(),
            };
            parts.Add(owner.Name is { Length: > 0 } ownerName ? $"{ownerName} ({type} {owner.Id})" : $"{type} {owner.Id}");
        }
        if (tuning.Attribute is { Length: > 0 } attribute) parts.Add(tuning.Op is { Length: > 0 } op ? $"{attribute} {op}" : attribute);
        return string.Join(", ", parts);
    }

    private static void Write(SourceProductionContext context, Export export, ImmutableArray<Declared> declared)
    {
        var classes = declared.GroupBy(d => d.Name).ToDictionary(g => g.Key, g => g.First());
        var powers = export.Powers.ToDictionary(p => p.ClassName);
        foreach (var d in classes.Values.Where(d => !powers.ContainsKey(d.Name)).OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(NoPower, d.Location, d.Name));
        }

        foreach (var power in export.Powers.Where(p => classes.ContainsKey(p.ClassName)))
        {
            var heroes = power.Items.Select(i => i.Hero).Distinct().ToList();
            if (heroes.Count > 1) context.ReportDiagnostic(Diagnostic.Create(SeveralHeroes, classes[power.ClassName].Location, power.Id, string.Join(", ", heroes)));

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(Namespace).Append(";\n\n")
                .Append("partial class ").Append(power.ClassName).Append("\n{\n")
                .Append("    /// <summary>").Append(Escape(power.Name)).Append(", legendary power ").Append(power.Id).Append(" of the game data.</summary>\n")
                .Append("    public const int PowerId = ").Append(power.Id).Append(";\n");
            if (power.Icon.Length > 0)
            {
                source.Append("\n    /// <summary>Game data: the full size image URL of <c>icon</c> of legendary power ").Append(power.Id).Append(".</summary>\n")
                    .Append("    public const string PowerIcon = ").Append(Literal(power.Icon)).Append(";\n");
            }
            foreach (var item in power.Items)
            {
                source.Append("\n    /// <summary>").Append(Escape(item.Name)).Append(", item ").Append(item.Id).Append(" of the game data.</summary>\n")
                    .Append("    public const int ").Append(PascalCase(item.Slot)).Append("ItemId = ").Append(item.Id).Append(";\n");
                if (item.Icon.Length > 0)
                {
                    source.Append("\n    /// <summary>Game data: the full size image URL of <c>icon</c> of item ").Append(item.Id).Append(".</summary>\n")
                        .Append("    public const string ").Append(PascalCase(item.Slot)).Append("ItemIcon = ").Append(Literal(item.Icon)).Append(";\n");
                }
            }
            // The legendary's icon is its first item's, else its power's.
            string? icon = power.Items.Where(i => i.Icon.Length > 0).Select(i => PascalCase(i.Slot) + "ItemIcon").FirstOrDefault()
                ?? (power.Icon.Length > 0 ? "PowerIcon" : null);
            var shared = new List<(string Name, string First, string Second)>();
            var tunings = TuningNames.Of(power.Tunings, export.Segments, heroes, shared);
            var part = classes[power.ClassName];
            TuningNames.Report(context, part.Location, power.ClassName, $"legendary power {power.Id}", shared, part.Fields, tunings);
            if (tunings.Count > 0)
            {
                TuningNames.Write(source, $"Game data: the tuning values of legendary power {power.Id}.",
                    tunings.Select(t => (t, $"Game data: tuning <c>{Escape(t.Keys)}</c> of legendary power {power.Id}{(t.Source.Length > 0 ? "; " + Escape(t.Source) : "")}.")),
                    false, true);
            }
            source.Append("\n    public override int LegendaryPowerId => PowerId;\n")
                .Append("    public override string Name => ").Append(Literal(power.Name)).Append(";\n")
                .Append("    public override string HeroName => ").Append(Literal(heroes[0])).Append(";\n")
                .Append("    public override global::System.Collections.Generic.IReadOnlyList<int> ItemIds => [")
                .Append(string.Join(", ", power.Items.Select(i => $"{PascalCase(i.Slot)}ItemId"))).Append("];\n");
            if (icon != null) source.Append("    public override string? Icon => ").Append(icon).Append(";\n");
            source.Append("}\n");
            context.AddSource($"{power.ClassName}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        // The icons of the powers with no class.
        var matched = new HashSet<string>(export.Powers.Where(p => classes.ContainsKey(p.ClassName)).Select(p => p.Id.ToString(CultureInfo.InvariantCulture)));
        context.AddSource("GameIcons.g.cs", SourceText.From(GameTables.Write("GameIcons", null,
        [
            new GameTable("LegendaryPowers", "Icon URLs of the game data's legendary power records with no legendary class, by ID.", "int", "string", false,
                new([.. export.PowerIcons.Where(p => !matched.Contains(p.Key))])),
            .. export.ItemTables,
        ], export.BuildDate), Encoding.UTF8));
        context.AddSource("GameHeroData.g.cs", SourceText.From(GameTables.Write("GameHeroData", null, export.HeroTables, export.BuildDate), Encoding.UTF8));
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
