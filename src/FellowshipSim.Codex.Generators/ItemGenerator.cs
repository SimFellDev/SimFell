using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Fellowship.SDK;
using Fellowship.SDK.Calculations;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FellowshipSim.Codex.Generators;

/// <summary>
/// Writes <c>FellowshipSim.Core.Import.GameGearData</c>, every hero's gear from the game data. <c>Items</c> has each hero's
/// items by hero name, in <c>GearKind</c> order and then by name. An item is in it when the game has it enabled, its slot is
/// a wearable slot, and it has a dungeon source or grants a relic passive. A wearable slot is a name of
/// <c>items.slotNames</c> with a slot weight in <c>statCalculation.slotWeights</c>. Its gear kind is the <c>GearKind</c>
/// member of the slot's name or of its singular (the game's <c>Wrists</c> is <c>GearKind.Wrist</c>). Each item has its stats
/// untempered at its drop item level (<c>dropItemLevels</c>), from FellowshipAnalyzer.Codex.SDK's stat calculation, its
/// most tempers, the Stamina a temper adds (<c>items.temperStaminaMultiplierAtItemLevel</c>), its dungeon sources, its weapon power's ability and its icon URL. <c>EmptySlotIcons</c> has each gear
/// kind's empty slot icon URL from <c>slotIcons</c>. A stat no build holds is CODEX040, an item at its most tempers with
/// stats other than its untempered stats and the tempers is CODEX041, a wearable slot with no <c>GearKind</c> member is
/// CODEX042, two items of one hero with one ID are CODEX043, a weapon power granting no ability is CODEX044, and a wearable
/// slot with no <c>slotIcons</c> icon is CODEX045. The game
/// data is the AdditionalFiles <c>entities.jsonl</c> and <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK;
/// <see cref="CreatureGenerator"/> reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ItemGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string GearKindType = "FellowshipSim.Core.Import.GearKind";
    private const string Namespace = "FellowshipSim.Core.Import";

    /// <summary>The position of Stamina in an item's stats.</summary>
    private const int StaminaIndex = 0;

    private static readonly DiagnosticDescriptor UnheldStat = new(
        "CODEX040", "An item has a stat no build holds",
        "Item {0} ({1}) of {2} has the stat {3}, and no build holds it", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor TemperCheck = new(
        "CODEX041", "An item at its most tempers has other stats than its untempered stats and the tempers",
        "Item {0} ({1}) of {2} at its most tempers has the stats {3}, and its untempered stats and the tempers are {4}", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NoGearKind = new(
        "CODEX042", "A wearable slot has no GearKind member",
        "The wearable slot {0} has no GearKind member of its name or of its singular", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SharedId = new(
        "CODEX043", "Two items of one hero have one ID",
        "{0} has two items with the ID {1}: {2}", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NoWeaponAbility = new(
        "CODEX044", "A weapon power grants no ability",
        "Item {0} ({1}) of {2} grants weapon power {3}, and the weapon power grants no ability", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NoSlotIcon = new(
        "CODEX045", "A wearable slot has no empty slot icon",
        "The wearable slot {0} has no icon in slotIcons", "Codex", DiagnosticSeverity.Error, true);

    /// <summary>
    /// A hero's item: its hero, ID, name and slot, its item level, most tempers and Stamina per temper, whether it is
    /// legendary, its weapon ability, its untempered stats, its dungeon sources and its icon URL.
    /// </summary>
    private sealed record Item(string Hero, int Id, string Name, string Slot, int ItemLevel, int MaxTempers, int StaminaPerTemper, bool Legendary,
        int WeaponAbilityId, EquatableArray<int> Stats, EquatableArray<string> Dungeons, string Icon);

    /// <summary>A wearable slot and its empty slot icon URL; an empty icon for a slot with no <c>slotIcons</c> entry.</summary>
    private sealed record Slot(string Name, string Icon);

    /// <summary>A problem in the game data: its diagnostic ID and the diagnostic's arguments.</summary>
    private sealed record Problem(string Id, EquatableArray<string> Arguments);

    /// <summary>The game data's items, its wearable slots, its problems and its build date.</summary>
    private sealed record Export(EquatableArray<Item> Items, EquatableArray<Slot> Slots, EquatableArray<Problem> Problems, string? BuildDate);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var export = context.AdditionalTextsProvider
            .Where(f => Path.GetFileName(f.Path) is EntitiesName or SettingsName)
            .Collect()
            .Select((all, ct) => Read(all, ct));
        // The GearKind members in the enum's order; none in a compilation without GearKind.
        var kinds = context.CompilationProvider.Select((compilation, _) =>
            compilation.GetTypeByMetadataName(GearKindType) is { TypeKind: TypeKind.Enum } type
                ? new EquatableArray<string>([.. type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue)
                    .OrderBy(f => Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture)).Select(f => f.Name)])
                : new EquatableArray<string>([]));
        context.RegisterSourceOutput(export.Combine(kinds), (spc, pair) => Write(spc, pair.Left, pair.Right));
    }

    private static Export? Read(ImmutableArray<AdditionalText> files, CancellationToken ct)
    {
        var entities = files.FirstOrDefault(f => Path.GetFileName(f.Path) == EntitiesName)?.GetText(ct);
        var settingsText = files.FirstOrDefault(f => Path.GetFileName(f.Path) == SettingsName)?.GetText(ct);
        if (entities == null || settingsText == null) return null;

        var settings = ExportReader.ReadSettings(settingsText.ToString());
        string? buildDate = settings.BuildDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var documents = new List<ItemDocument>();
        var powers = new Dictionary<int, WeaponPowerDocument>();
        foreach (var line in entities.Lines)
        {
            ct.ThrowIfCancellationRequested();
            string text = line.ToString();
            // Only item and weapon power lines are read in full.
            if (!text.Contains("\"$type\":\"item\"") && !text.Contains("\"$type\":\"weapon-power\"")) continue;
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
                case ItemDocument item:
                    documents.Add(item);
                    break;
                case WeaponPowerDocument power:
                    powers[power.Id] = power;
                    break;
            }
        }

        var wearable = settings.Items.SlotNames.Values.Where(settings.StatCalculation.SlotWeights.ContainsKey).Distinct().ToList();
        var slots = wearable.Select(s => new Slot(s, settings.SlotIcons.TryGetValue(s, out string? icon) ? Textures.Url(icon) : "")).ToList();

        // The stat calculation reads the settings and the item alone.
        var export = new FellowshipExport(settings, [.. documents]);
        var problems = new List<Problem>();
        var items = new List<Item>();
        foreach (var document in documents)
        {
            ct.ThrowIfCancellationRequested();
            if (document.EnabledInGame is false || document.Hero is not { } heroOf || document.Slot is not { } slot || !wearable.Contains(slot)) continue;
            var dungeons = (document.Sources ?? []).Where(s => s.Kind == SourceKind.Dungeon).Select(s => s.Name).ToList();
            if (dungeons.Count == 0 && document.Grants is not { Type: EntityType.RelicPassive }) continue;

            string hero = heroOf.ToString();
            string name = (document.Name ?? "").Trim();
            string id = document.Id.ToString(CultureInfo.InvariantCulture);
            int level = export.DropItemLevel(document);
            int maxTempers = export.MaxUpgrades(document, new ItemScaling { ItemLevel = level }) ?? 0;
            // A multiplier with a fraction fails the temper check.
            int staminaPerTemper = (int)Math.Round(Tempering.StaminaMultiplier(settings.Items, level));
            int[]? stats = StatsOf(export, document, level, 0, hero, problems);
            if (stats == null) continue;
            if (maxTempers > 0 && StatsOf(export, document, level, maxTempers, hero, problems) is { } most)
            {
                // Stamina gains the Stamina per temper, and each other stat one, per temper.
                int[] expected = [.. stats.Select((s, i) => s == 0 ? 0 : s + maxTempers * (i == StaminaIndex ? staminaPerTemper : 1))];
                if (!most.SequenceEqual(expected))
                {
                    problems.Add(new Problem(TemperCheck.Id, new([id, name, hero, $"[{string.Join(", ", most)}]", $"[{string.Join(", ", expected)}]"])));
                }
            }

            int weaponAbility = 0;
            if (document.Grants is { Type: EntityType.WeaponPower } granted)
            {
                if (powers.TryGetValue(granted.Id, out var power) && power.Grants?.FirstOrDefault(g => g.Type == EntityType.Ability) is { } ability)
                {
                    weaponAbility = ability.Id;
                }
                else
                {
                    problems.Add(new Problem(NoWeaponAbility.Id, new([id, name, hero, granted.Id.ToString(CultureInfo.InvariantCulture)])));
                }
            }
            string icon = document.Icon is { Length: > 0 } texture ? Textures.Url(texture) : "";
            items.Add(new Item(hero, document.Id, name, slot, level, maxTempers, staminaPerTemper, document.Rarity == Rarity.Legendary, weaponAbility,
                new([.. stats]), new([.. dungeons]), icon));
        }
        return new Export(new([.. items]), new([.. slots]), new([.. problems]), buildDate);
    }

    /// <summary>
    /// The item's Stamina, primary stat, CritChance, Expertise, Haste and Spirit at an item level and temper count, in the
    /// order of <c>GearStats</c>; null, with a problem, for an item with a stat no build holds. Armor is no stat of a build.
    /// </summary>
    private static int[]? StatsOf(FellowshipExport export, ItemDocument item, int level, int tempers, string hero, List<Problem> problems)
    {
        var stats = new int[6];
        foreach (var line in export.Stats(item, new ItemScaling { ItemLevel = level, Upgrades = tempers }))
        {
            int at = line.Stat switch
            {
                "Stamina" => StaminaIndex,
                "CritChance" => 2,
                "Expertise" => 3,
                "Haste" => 4,
                "Spirit" => 5,
                "Armor" => -1,
                { } stat when export.PrimaryAttributes.Contains(stat) => 1,
                _ => -2,
            };
            if (at == -2)
            {
                problems.Add(new Problem(UnheldStat.Id, new([item.Id.ToString(CultureInfo.InvariantCulture), (item.Name ?? "").Trim(), hero, line.Stat ?? line.DisplayName])));
                return null;
            }
            if (at >= 0) stats[at] += (int)line.Value;
        }
        return stats;
    }

    private static void Write(SourceProductionContext context, Export? export, EquatableArray<string> kinds)
    {
        if (export == null || !kinds.Any()) return;
        var descriptors = new[] { UnheldStat, TemperCheck, NoGearKind, SharedId, NoWeaponAbility }.ToDictionary(d => d.Id);
        foreach (var problem in export.Problems)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptors[problem.Id], Location.None, [.. problem.Arguments]));
        }

        // A slot's gear kind is the GearKind member of its name, or of its singular.
        var kindList = kinds.ToList();
        var kindOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in export.Slots)
        {
            string? kind = kindList.Contains(slot.Name) ? slot.Name : kindList.FirstOrDefault(k => k + "s" == slot.Name);
            if (kind == null) context.ReportDiagnostic(Diagnostic.Create(NoGearKind, Location.None, slot.Name));
            else kindOf[slot.Name] = kind;
            if (slot.Icon.Length == 0) context.ReportDiagnostic(Diagnostic.Create(NoSlotIcon, Location.None, slot.Name));
        }

        var source = new StringBuilder()
            .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
            .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
            .Append("#nullable enable\n")
            .Append("namespace ").Append(Namespace).Append(";\n\n")
            .Append("/// <summary>\n")
            .Append("/// Every hero's gear from the game data: each enabled item of a wearable slot with a dungeon source or a relic passive, with\n")
            .Append("/// its stats untempered at its drop item level, by hero name. <see cref=\"GearItem.StatsAt\"/> adds the tempers.\n")
            .Append("/// </summary>\n")
            .Append("public static class GameGearData\n{\n")
            .Append("    /// <summary>Game data: <c>slotIcons</c>, each gear kind's empty slot icon URL.</summary>\n")
            .Append("    public static IReadOnlyDictionary<GearKind, string> EmptySlotIcons { get; } = new Dictionary<GearKind, string>\n    {\n");
        foreach (var slot in export.Slots.Where(s => kindOf.ContainsKey(s.Name) && s.Icon.Length > 0).OrderBy(s => kindList.IndexOf(kindOf[s.Name])))
        {
            source.Append("        [GearKind.").Append(kindOf[slot.Name]).Append("] = ").Append(Literal(slot.Icon)).Append(",\n");
        }
        source.Append("    };\n\n")
            .Append("    /// <summary>Each hero's gear, by hero name, in slot order and then by name.</summary>\n")
            .Append("    public static IReadOnlyDictionary<string, GearItem[]> Items { get; } = new Dictionary<string, GearItem[]>(StringComparer.OrdinalIgnoreCase)\n    {\n");
        foreach (var hero in export.Items.Where(i => kindOf.ContainsKey(i.Slot)).GroupBy(i => i.Hero).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            foreach (var shared in hero.GroupBy(i => i.Id).Where(g => g.Count() > 1))
            {
                context.ReportDiagnostic(Diagnostic.Create(SharedId, Location.None, hero.Key, shared.Key, string.Join(", ", shared.Select(i => i.Name))));
            }
            source.Append("        [").Append(Literal(hero.Key)).Append("] =\n        [\n");
            foreach (var item in hero.OrderBy(i => kindList.IndexOf(kindOf[i.Slot])).ThenBy(i => i.Name, StringComparer.Ordinal))
            {
                source.Append("            new(").Append(item.Id).Append(", ").Append(Literal(item.Name)).Append(", GearKind.").Append(kindOf[item.Slot])
                    .Append(", ").Append(item.ItemLevel).Append(", ").Append(item.MaxTempers).Append(", ").Append(item.StaminaPerTemper).Append(", ").Append(item.Legendary ? "true" : "false")
                    .Append(", ").Append(item.WeaponAbilityId).Append(", new(").Append(string.Join(", ", item.Stats)).Append("), [")
                    .Append(string.Join(", ", item.Dungeons.Select(Literal))).Append("], ").Append(Literal(item.Icon)).Append("),\n");
            }
            source.Append("        ],\n");
        }
        source.Append("    };\n}\n");
        context.AddSource("GameGearData.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
