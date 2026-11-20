using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Fellowship.SDK;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FellowshipSim.Codex.Generators;

/// <summary>
/// Writes <c>FellowshipSim.Core.Import.GameIcons</c>'s tables over every record of a type, each icon its full size image
/// URL. A record's row is the first record of its ID with an icon, in ID order.
/// <list type="bullet">
/// <item><c>Abilities</c>, <c>Effects</c>, <c>StatEnchants</c> (enchant records) and <c>GemItems</c> (gem records): icons
/// by ID.</item>
/// <item><c>Names</c>: icons by name, from the first ability, effect, talent, aspect, sigil, protective enchant, glyph,
/// weapon power or legendary power record of that name with an icon, in that order of types.</item>
/// <item><c>Gems</c>: the icon of the first gem record of each colour, the last word of its name.</item>
/// <item><c>Heroes</c>: each hero's portrait, by hero name.</item>
/// <item><c>WeaponHeroes</c>: the heroes of each weapon category ability, by ID.</item>
/// <item><c>AugmentationMaxRanks</c> and <c>GlyphMaxRanks</c>: each aspect, sigil, protective enchant and glyph's
/// <c>maxRank</c> other than 0, by ID.</item>
/// </list>
/// The game data is the AdditionalFiles <c>entities.jsonl</c> and <c>settings.json</c>, read through
/// FellowshipAnalyzer.Codex.SDK; <see cref="CreatureGenerator"/> reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class GameIconGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    /// <summary>The record types of <c>Names</c>, in the order a name takes its icon from.</summary>
    private static readonly EntityType[] NameTypes =
    [
        EntityType.Ability, EntityType.Effect, EntityType.Talent, EntityType.Aspect, EntityType.Sigil, EntityType.ProtectiveEnchant,
        EntityType.Glyph, EntityType.WeaponPower, EntityType.LegendaryPower,
    ];

    /// <summary>The record types the tables read, each with its document's icon (a hero's portrait) and <c>maxRank</c>.</summary>
    private static readonly IReadOnlyDictionary<EntityType, Func<EntityDocument, (string? Icon, int MaxRank)>> Types =
        new Dictionary<EntityType, Func<EntityDocument, (string? Icon, int MaxRank)>>
        {
            [EntityType.Ability] = d => (((AbilityDocument)d).Icon, 0),
            [EntityType.Effect] = d => (((EffectDocument)d).Icon, 0),
            [EntityType.Talent] = d => (((TalentDocument)d).Icon, 0),
            [EntityType.Aspect] = d => (((AugmentationDocument)d).Icon, ((AugmentationDocument)d).MaxRank ?? 0),
            [EntityType.Sigil] = d => (((AugmentationDocument)d).Icon, ((AugmentationDocument)d).MaxRank ?? 0),
            [EntityType.ProtectiveEnchant] = d => (((AugmentationDocument)d).Icon, ((AugmentationDocument)d).MaxRank ?? 0),
            [EntityType.Enchant] = d => (((AugmentationDocument)d).Icon, 0),
            [EntityType.Glyph] = d => (((GlyphDocument)d).Icon, ((GlyphDocument)d).MaxRank ?? 0),
            [EntityType.WeaponPower] = d => (((WeaponPowerDocument)d).Icon, 0),
            [EntityType.LegendaryPower] = d => (((LegendaryPowerDocument)d).Icon, 0),
            [EntityType.Gem] = d => (((GemDocument)d).Icon, 0),
            [EntityType.Hero] = d => (((HeroDocument)d).Portrait, 0),
        };

    /// <summary>The <c>$type</c> property of each of <see cref="Types"/>'s lines, by the SDK's slug of the type.</summary>
    private static readonly string[] Markers = [.. Types.Keys.Select(t => $"\"{EntityDocuments.Discriminator}\":\"{EntityTypes.Slug(t)}\"")];

    /// <summary>A record: its type, ID, name, icon (empty for none), whether it is a weapon category ability, its heroes and its <c>maxRank</c>.</summary>
    private sealed record Record(EntityType Type, int Id, string Name, string Icon, bool Weapon, EquatableArray<string> Heroes, int MaxRank);

    /// <summary>The game data's tables and its build date.</summary>
    private sealed record Export(EquatableArray<GameTable> Tables, string? BuildDate);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var export = context.AdditionalTextsProvider
            .Where(f => Path.GetFileName(f.Path) is EntitiesName or SettingsName)
            .Collect()
            .Select((all, ct) => Read(all, ct));
        context.RegisterSourceOutput(export, (spc, e) =>
        {
            if (e == null) return;
            spc.AddSource("GameIcons.g.cs", SourceText.From(GameTables.Write("GameIcons",
                "Game icons from the game data, each its full size image URL on the Codex CDN, for display alone: the engine never\n" +
                "reads them. An entity with an engine class has its icon in its generated half; these tables have the rest.",
                e.Tables, e.BuildDate), Encoding.UTF8));
        });
    }

    private static Export? Read(ImmutableArray<AdditionalText> files, CancellationToken ct)
    {
        var entities = files.FirstOrDefault(f => Path.GetFileName(f.Path) == EntitiesName)?.GetText(ct);
        var settings = files.FirstOrDefault(f => Path.GetFileName(f.Path) == SettingsName)?.GetText(ct);
        if (entities == null) return null;

        string? buildDate = null;
        if (settings != null)
        {
            var file = JsonSerializer.Deserialize<SettingsFile>(settings.ToString(), EntityDocuments.Options);
            buildDate = file?.BuildDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var records = new List<Record>();
        foreach (var line in entities.Lines)
        {
            ct.ThrowIfCancellationRequested();
            string text = line.ToString();
            // Only the lines of the tables' types are read in full.
            if (!Markers.Any(text.Contains)) continue;
            EntityDocument? document;
            try
            {
                document = JsonSerializer.Deserialize<EntityDocument>(text, EntityDocuments.Options);
            }
            catch (JsonException)
            {
                continue;
            }
            if (RecordOf(document) is { } record) records.Add(record);
        }

        // Each type's records by ID; the order of the game data among records of one ID.
        var byId = records.OrderBy(r => r.Id).ToList();
        IEnumerable<Record> Of(EntityType type) => byId.Where(r => r.Type == type);
        IEnumerable<Record> WithIcon(EntityType type) => Of(type).Where(r => r.Icon.Length > 0);
        GameTable ById(string name, string summary, IEnumerable<Record> rows) =>
            new(name, summary, "int", "string", false, new([.. rows.Select(r => new TableEntry(Int(r.Id), Url(r.Icon)))]));

        var colours = new List<TableEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gem in WithIcon(EntityType.Gem))
        {
            string colour = gem.Name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
            if (colour.Length > 0 && seen.Add(colour)) colours.Add(new TableEntry(GameTables.Literal(colour), Url(gem.Icon)));
        }

        var tables = new List<GameTable>
        {
            ById("Abilities", "Icon URLs of the game data's ability records, by ID: a sim report's damage sources.", WithIcon(EntityType.Ability)),
            ById("Effects", "Icon URLs of the game data's effect records, by ID: a sim report's auras.", WithIcon(EntityType.Effect)),
            new("Names", "Icon URLs by name, from the first ability, effect, talent or gear record of that name with an icon.", "string", "string", true,
                new([.. NameTypes.SelectMany(WithIcon)
                    .Where(r => r.Name.Trim().Length > 0)
                    .GroupBy(r => r.Name.ToLowerInvariant()).Select(g => g.First())
                    .OrderBy(r => r.Name, StringComparer.Ordinal)
                    .Select(r => new TableEntry(GameTables.Literal(r.Name), Url(r.Icon)))])),
            ById("StatEnchants", "Icon URLs of the game data's enchant records, by ID.", WithIcon(EntityType.Enchant)),
            ById("GemItems", "Icon URLs of the game data's gem records, by ID: each gem of each size (\"Splendid Amethyst\").", WithIcon(EntityType.Gem)),
            new("Gems", "Icon URLs of the gem colours, by name (\"Sapphire\"): the first gem record of each colour.", "string", "string", false,
                new([.. colours.OrderBy(c => c.Key, StringComparer.Ordinal)])),
            new("Heroes", "Hero portrait URLs, by hero name.", "string", "string", true,
                new([.. WithIcon(EntityType.Hero).OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => new TableEntry(GameTables.Literal(r.Name), Url(r.Icon)))])),
            new("WeaponHeroes", "The heroes of each weapon ability's weapon, by ability ID.", "int", "string[]", false,
                new([.. Of(EntityType.Ability).Where(r => r.Weapon)
                    .Select(r => new TableEntry(Int(r.Id), "[" + string.Join(", ", r.Heroes.Select(GameTables.Literal)) + "]"))])),
            new("AugmentationMaxRanks", "The highest rank of each aspect, sigil and protective enchant, by ID.", "int", "int", false,
                new([.. new[] { EntityType.Aspect, EntityType.Sigil, EntityType.ProtectiveEnchant }.SelectMany(Of).OrderBy(r => r.Id)
                    .Where(r => r.MaxRank != 0).Select(r => new TableEntry(Int(r.Id), Int(r.MaxRank)))])),
            new("GlyphMaxRanks", "The highest rank of each glyph, by ID.", "int", "int", false,
                new([.. Of(EntityType.Glyph).Where(r => r.MaxRank != 0).Select(r => new TableEntry(Int(r.Id), Int(r.MaxRank)))])),
        };
        return new Export(new([.. tables]), buildDate);
    }

    /// <summary>The document as a <see cref="Record"/>; a hero's icon is its portrait. Null for a document of no table's type.</summary>
    private static Record? RecordOf(EntityDocument? document)
    {
        if (document == null || !Types.TryGetValue(EntityTypes.TypeOf(document), out var read)) return null;
        var (icon, maxRank) = read(document);
        var ability = document as AbilityDocument;
        return new Record(EntityTypes.TypeOf(document), document.Id, document.Name ?? "", icon ?? "", ability?.Category == AbilityCategory.Weapon,
            new([.. (ability?.Heroes ?? []).Select(h => h.ToString())]), maxRank);
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Url(string texture) => GameTables.Literal(Textures.Url(texture));
}
