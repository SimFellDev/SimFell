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
/// Writes the game data's half of each weapon class and each weapon ability class, from the weapon power records.
/// <list type="bullet">
/// <item>A <c>partial</c> class deriving from <c>FellowshipSim.Core.Customization.Weapons.Weapon</c> names the weapon power
/// whose name, or the name of an item granting it, in Pascal case (letters and digits only, an apostrophe inside its word)
/// is the class name. Its constants are <c>WeaponPowerId</c>, <c>WeaponPowerName</c>, <c>WeaponPowerIcon</c> (the full size
/// image URL of the power's icon), <c>WeaponPowerAbilityId</c>, the ability the power grants under the power's name, and
/// <c>WeaponPowerAbilityIcon</c>, that ability's icon URL and the <c>Icon</c> override of <c>Weapon</c>. The class's own part
/// has its <c>Name</c>, its <c>WeaponAbilityId</c> override and <c>Equip</c>.</item>
/// <item>A <c>partial</c> class deriving from <c>FellowshipSim.Core.Domain.Ability</c> in
/// <c>FellowshipSim.Core.Customization.WeaponAbilities</c> names the weapon power whose name in Pascal case is the class
/// name, and the ability that power grants under its name. Its constants are <c>WeaponPowerId</c>,
/// <c>WeaponPowerName</c>, <c>WeaponPowerIcon</c>, <c>AbilityId</c>, <c>AbilityName</c>, <c>AbilityIcon</c> (the full size
/// image URL of the ability's icon), one per scalar field of the ability record under
/// <c>Ability</c> and the field's name (<c>AbilityCategory</c>, <c>AbilityCooldown</c>, <c>AbilityCastTime</c>...), and the
/// weapon power's tuning values in a nested <c>Constants</c> under their <see cref="TuningNames"/> names, with no hero as the
/// record's hero. A tuning key of the power with the row of one of the ability's fields (<see cref="FieldTuning"/>) has no
/// constant in <c>Constants</c>, and the field's summary names it. The class's own part has the ability's behaviour.</item>
/// </list>
/// <c>GameIcons.WeaponAbilities</c> has the icon URLs of the weapon category abilities with no weapon class, by ID, and
/// <c>GameHeroData.WeaponAbilityNames</c> the name of every weapon category ability. A member of the class or a base class
/// with a constant's name, or the name <c>Constants</c>, is CODEX025. The game data is the AdditionalFiles
/// <c>entities.jsonl</c> and <c>settings.json</c>, read through FellowshipAnalyzer.Codex.SDK; <see cref="CreatureGenerator"/>
/// reports missing game data.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class WeaponGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string CustomizationNamespace = "FellowshipSim.Core.Customization.";
    private const string WeaponNamespacePrefix = "FellowshipSim.Core.Customization.Weapon";
    private const string WeaponAbilitiesNamespace = "FellowshipSim.Core.Customization.WeaponAbilities";
    private const string WeaponType = "FellowshipSim.Core.Customization.Weapons.Weapon";
    private const string AbilityType = "FellowshipSim.Core.Domain.Ability";

    private static readonly DiagnosticDescriptor NoRecord = new(
        "CODEX022", "A partial weapon or weapon ability class names no weapon power record",
        "The partial class {0} names no {1} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralRecords = new(
        "CODEX023", "A partial weapon or weapon ability class names several weapon power records",
        "The partial class {0} names the weapon power records {1} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX025", "A weapon constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, and {1} is a constant of weapon power {2}; rename the member", "Codex", DiagnosticSeverity.Error, true);

    /// <summary>A constant of a weapon power record: its name, C# type and literal, and its summary.</summary>
    private sealed record Constant(string Name, string Type, string Literal, string Summary, string? Field = null);

    /// <summary>An item granting a weapon power: its ID, name, hero and class name.</summary>
    private sealed record Item(int Id, string Name, string Hero, string ClassName);

    /// <summary>
    /// A weapon power record: its ID, name, icon URL (empty for none), class name, the items granting it, the ability it
    /// grants under its name (ID 0 for none), that ability's icon URL and field constants, and the power's tuning values.
    /// </summary>
    private sealed record Power(int Id, string Name, string Icon, string ClassName, EquatableArray<Item> Items, int AbilityId, string AbilityName,
        string AbilityIcon, EquatableArray<Constant> AbilityFields, EquatableArray<TuningEntry> Tunings);

    /// <summary>A weapon category ability record: its ID, name and icon URL (empty for none).</summary>
    private sealed record WeaponAbility(int Id, string Name, string Icon);

    /// <summary>
    /// The game data's weapon power records, its weapon category ability records, the tuning keys' first segments naming a hero,
    /// and its build date.
    /// </summary>
    private sealed record Export(EquatableArray<Power> Powers, EquatableArray<WeaponAbility> WeaponAbilities, EquatableArray<HeroSegment> Segments, string? BuildDate);

    /// <summary>
    /// A partial weapon or weapon ability class: its namespace, name, whether it is a weapon ability, the member names of
    /// the class and its base classes, the class's constants and static readonly fields, and where it is declared.
    /// </summary>
    private sealed record Declared(string Namespace, string Name, bool IsAbility, EquatableArray<string> Members, EquatableArray<string> Fields, Location? Location)
    {
        public bool Equals(Declared? other) => other != null && Namespace == other.Namespace && Name == other.Name && IsAbility == other.IsAbility
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
                    && NamespaceOf(c) is { } ns && ns.StartsWith(WeaponNamespacePrefix, StringComparison.Ordinal),
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
    /// The partial class as a <see cref="Declared"/>, or null for a class deriving neither from <c>Weapon</c> nor, in the
    /// weapon abilities namespace, from <c>Ability</c>.
    /// </summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        string ns = NamespaceOf(node)!;
        bool weapon = false, ability = false;
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        for (var t = type; t != null; t = t.BaseType)
        {
            string name = t.ToDisplayString();
            if (!SymbolEqualityComparer.Default.Equals(t, type))
            {
                if (name == WeaponType) weapon = true;
                if (name == AbilityType && ns == WeaponAbilitiesNamespace) ability = true;
            }
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
        if (!weapon && !ability) return null;
        return new Declared(ns, type.Name, ability, new([.. members.OrderBy(m => m, StringComparer.Ordinal)]), new([.. TuningNames.FieldsOf(type)]),
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

        var powers = new List<WeaponPowerDocument>();
        var items = new List<(int PowerId, Item Item)>();
        var abilities = new Dictionary<int, AbilityDocument>();
        var weaponAbilities = new List<AbilityDocument>();
        if (entities != null)
        {
            foreach (var line in entities.Lines)
            {
                ct.ThrowIfCancellationRequested();
                string text = line.ToString();
                // Only weapon power, ability and weapon power item lines are read in full.
                bool power = text.Contains("\"$type\":\"weapon-power\"");
                bool ability = text.Contains("\"$type\":\"ability\"");
                bool item = text.Contains("\"$type\":\"item\"") && text.Contains("\"grants\":{\"type\":\"weapon-power\"");
                if (!power && !ability && !item) continue;
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
                    case WeaponPowerDocument p when (p.Name ?? "").Trim().Length > 0:
                        powers.Add(p);
                        break;
                    case AbilityDocument a:
                        abilities[a.Id] = a;
                        if (a.Category == AbilityCategory.Weapon) weaponAbilities.Add(a);
                        break;
                    case ItemDocument { Grants: { } grants } i when (i.Name ?? "").Trim().Length > 0:
                        string name = i.Name!.Trim();
                        items.Add((grants.Id, new Item(i.Id, name, i.Hero?.ToString() ?? "", PascalCase(name))));
                        break;
                }
            }
        }

        var records = new List<Power>();
        foreach (var p in powers)
        {
            string name = p.Name!.Trim();
            // The ability the power grants under its name.
            var granted = (p.Grants ?? []).FirstOrDefault(g => g.Type.ToString() == "Ability" && (g.Name ?? "").Trim() == name);
            AbilityDocument? ability = granted != null ? abilities.GetValueOrDefault(granted.Id) : null;
            var powerItems = items.Where(i => i.PowerId == p.Id).Select(i => i.Item).OrderBy(i => i.Id).ThenBy(i => i.Hero, StringComparer.Ordinal).ToList();
            // The ability's fields with the value of a tuning key of the power: the field and the key have one row.
            var fields = ability != null ? FieldsOf(ability) : [];
            var keys = ability != null ? FieldTuning.KeysOf(ability, fields.Select(f => f.Field).OfType<string>(), p) : [];
            var merged = new HashSet<string>(keys.Values.SelectMany(k => k), StringComparer.Ordinal);
            records.Add(new Power(p.Id, name, UrlOf(p.Icon), PascalCase(name), new([.. powerItems]), ability?.Id ?? 0, (ability?.Name ?? "").Trim(),
                UrlOf(ability?.Icon),
                new([.. fields.Select(f => f.Field != null && keys.TryGetValue(f.Field, out var k) ? f with { Summary = FieldTuning.Summary(f.Summary, k, $"weapon power {p.Id}") } : f)]),
                new([.. TuningsOf(p).Where(t => !merged.Contains(t.Key))])));
        }
        // Every weapon category ability record, in ID order.
        var weapons = weaponAbilities.OrderBy(a => a.Id).Select(a => new WeaponAbility(a.Id, a.Name ?? "", UrlOf(a.Icon))).ToList();
        return new Export(new([.. records.OrderBy(r => r.Id)]), new([.. weapons]), HeroSegments.Of(entities, ct), buildDate);
    }

    /// <summary>The URL of a texture's full size image; empty for no texture.</summary>
    private static string UrlOf(string? texture) => texture is { Length: > 0 } ? Textures.Url(texture) : "";

    /// <summary>The ability record's scalar fields, each under <c>Ability</c> and the field's name.</summary>
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

    /// <summary>The weapon power's tuning keys, in the order of the game data.</summary>
    private static List<TuningEntry> TuningsOf(WeaponPowerDocument power) =>
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
            string type = Words(owner.Type.ToString());
            parts.Add(owner.Name is { Length: > 0 } ownerName ? $"{ownerName} ({type} {owner.Id})" : $"{type} {owner.Id}");
        }
        if (tuning.Attribute is { Length: > 0 } attribute) parts.Add(tuning.Op is { Length: > 0 } op ? $"{attribute} {op}" : attribute);
        return string.Join(", ", parts);
    }

    /// <summary>A Pascal case type name as lower case words: <c>WeaponPower</c> is <c>weapon power</c>.</summary>
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
        // The weapon abilities of the weapon classes with an Icon override.
        var withIcon = new HashSet<int>();
        foreach (var d in declared.GroupBy(d => (d.Namespace, d.Name)).Select(g => g.First())
                     .OrderBy(d => d.Namespace, StringComparer.Ordinal).ThenBy(d => d.Name, StringComparer.Ordinal))
        {
            var candidates = export.Powers
                .Where(p => p.ClassName == d.Name || (!d.IsAbility && p.Items.Any(i => i.ClassName == d.Name)))
                .ToList();
            if (candidates.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoRecord, d.Location, d.Name, d.IsAbility ? "weapon power record" : "weapon power record or weapon item"));
                continue;
            }
            if (candidates.Count != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(SeveralRecords, d.Location, d.Name, string.Join(", ", candidates.Select(p => p.Id))));
                continue;
            }

            var power = candidates[0];
            if (power.AbilityId == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoRecord, d.Location, d.Name, $"ability granted by weapon power {power.Id} under its name"));
                continue;
            }
            string owner = $"weapon power {power.Id}";
            var matched = power.Items.Where(i => i.ClassName == d.Name).ToList();
            string granted = matched.Count > 0
                ? "; granted by " + string.Join(", ", matched.Select(i => $"item {i.Id} ({Escape(i.Name)}{(i.Hero.Length > 0 ? ", " + i.Hero : "")})"))
                : "";
            var all = new List<Constant>
            {
                new("WeaponPowerId", "int", power.Id.ToString(CultureInfo.InvariantCulture), $"{Escape(power.Name)}, {owner} of the game data{granted}."),
                new("WeaponPowerName", "string", Literal(power.Name), $"Game data: <c>name</c> of {owner}."),
            };
            if (power.Icon.Length > 0)
            {
                all.Add(new("WeaponPowerIcon", "string", Literal(power.Icon), $"Game data: the full size image URL of <c>icon</c> of {owner}."));
            }
            if (d.IsAbility)
            {
                all.Add(new("AbilityId", "int", power.AbilityId.ToString(CultureInfo.InvariantCulture),
                    $"{Escape(power.AbilityName)}, ability {power.AbilityId} of the game data, in <c>grants</c> of {owner}."));
                all.Add(new("AbilityName", "string", Literal(power.AbilityName), $"Game data: <c>name</c> of ability {power.AbilityId}."));
                all.AddRange(power.AbilityFields);
            }
            else
            {
                all.Add(new("WeaponPowerAbilityId", "int", power.AbilityId.ToString(CultureInfo.InvariantCulture),
                    $"{Escape(power.AbilityName)}, ability {power.AbilityId} of the game data, in <c>grants</c> of {owner}."));
                if (power.AbilityIcon.Length > 0)
                {
                    all.Add(new("WeaponPowerAbilityIcon", "string", Literal(power.AbilityIcon),
                        $"Game data: the full size image URL of <c>icon</c> of ability {power.AbilityId}."));
                }
            }
            var shared = new List<(string Name, string First, string Second)>();
            var tunings = d.IsAbility ? TuningNames.Of(power.Tunings, export.Segments, [], shared) : [];
            TuningNames.Report(context, d.Location, d.Name, owner, shared, d.Fields, tunings);

            var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(d.Namespace).Append(";\n\n")
                .Append("partial class ").Append(d.Name).Append("\n{\n");
            bool first = true;
            var written = new HashSet<string>(StringComparer.Ordinal);
            foreach (var constant in all)
            {
                if (members.Contains(constant.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, constant.Name, power.Id));
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
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "Constants", power.Id));
                }
                else
                {
                    TuningNames.Write(source, $"Game data: the tuning values of {owner}.",
                        tunings.Select(t => (t, $"Game data: tuning <c>{Escape(t.Keys)}</c> of {owner}{(t.Source.Length > 0 ? "; " + Escape(t.Source) : "")}.")),
                        false, !first);
                }
            }
            if (written.Contains("WeaponPowerAbilityIcon"))
            {
                source.Append("\n    public override string? Icon => WeaponPowerAbilityIcon;\n");
                withIcon.Add(power.AbilityId);
            }
            source.Append("}\n");
            context.AddSource($"{d.Namespace.Substring(CustomizationNamespace.Length)}.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        string Key(WeaponAbility a) => a.Id.ToString(CultureInfo.InvariantCulture);
        context.AddSource("GameIcons.g.cs", SourceText.From(GameTables.Write("GameIcons", null,
        [
            new GameTable("WeaponAbilities", "Icon URLs of the game data's weapon category ability records with no weapon class, by ID.", "int", "string", false,
                new([.. export.WeaponAbilities.Where(a => a.Icon.Length > 0 && !withIcon.Contains(a.Id)).Select(a => new TableEntry(Key(a), Literal(a.Icon)))])),
        ], export.BuildDate), Encoding.UTF8));
        context.AddSource("GameHeroData.g.cs", SourceText.From(GameTables.Write("GameHeroData", null,
        [
            new GameTable("WeaponAbilityNames", "The name of each weapon ability (weapon category ability records), by ID.", "int", "string", false,
                new([.. export.WeaponAbilities.Select(a => new TableEntry(Key(a), Literal(a.Name)))])),
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
            // An apostrophe stays inside its word: Sahril's is Sahrils.
            else if (c != '\'') start = true;
        }
        return result.ToString();
    }

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
