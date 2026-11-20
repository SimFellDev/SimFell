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
/// Writes the game data's half of each dungeon affix class: for each <c>partial</c> class deriving from
/// <c>FellowshipSim.Core.Dungeons.Affixes.DungeonAffix</c>, a <c>partial</c> of the same class with the values of its
/// dungeon affix record. The record is the affix whose name in Pascal case is the class name. The affix's creatures are
/// the creatures with a passive effect the affix applies (the Anomalous Orb of Anomalous Orbs). The constants are
/// <c>AffixId</c>, <c>AffixName</c> and <c>AffixAbilityId</c> (the ability the affix grants); for each creature ability
/// <c>&lt;Ability&gt;AbilityId</c>, <c>&lt;Ability&gt;CastTime</c> and the creature record's fixed
/// <c>&lt;Ability&gt;InitialCooldown</c>, <c>&lt;Ability&gt;Cooldown</c>, <c>&lt;Ability&gt;InterruptCooldown</c> and
/// <c>&lt;Ability&gt;CcCooldown</c>; and for each effect of the affix, its ability and its creatures' abilities with a name no
/// other of them has, <c>&lt;Effect&gt;EffectId</c>, <c>&lt;Effect&gt;Duration</c>, <c>&lt;Effect&gt;MaxStacks</c> and one
/// <c>&lt;Effect&gt;&lt;Attribute&gt;</c> per modifier value. An ability with no name takes the name its effects share before
/// their ':' ("Anomalous Residue"). The nested <c>Constants</c> has the tuning values of the affix's ability under their
/// <see cref="TuningNames"/> names, and those of each creature ability with the ability's name first. Effects sharing one
/// name are CODEX050, and a member of the class with a constant's name, or the name <c>Constants</c>, is CODEX051.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DungeonAffixGenerator : IIncrementalGenerator
{
    private const string EntitiesName = "entities.jsonl";
    private const string SettingsName = "settings.json";
    private const string AffixType = "FellowshipSim.Core.Dungeons.Affixes.DungeonAffix";
    private const string AffixNamespace = "FellowshipSim.Core.Dungeons.Affixes";

    private static readonly DiagnosticDescriptor NoRecord = new(
        "CODEX048", "A partial dungeon affix class names no dungeon affix record",
        "The partial class {0} names no dungeon affix record in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SeveralRecords = new(
        "CODEX049", "A partial dungeon affix class names several dungeon affix records",
        "The partial class {0} names the dungeon affix records {1} in the game data", "Codex", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SharedEffectName = new(
        "CODEX050", "Several effects of a dungeon affix have one name",
        "Dungeon affix {0} has effects {1} of one name; the class has no constants {2}", "Codex", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor DeclaredName = new(
        "CODEX051", "A dungeon affix constant's name is a member of the class",
        "The class {0} or a base class has a member {1}, and {1} is a constant of dungeon affix {2}; rename the member", "Codex", DiagnosticSeverity.Error, true);

    /// <summary>A constant of a dungeon affix: its name, C# type and literal, and its summary.</summary>
    private sealed record Constant(string Name, string Type, string Literal, string Summary);

    /// <summary>A dungeon affix record: its ID, name, class name, constants, tuning values, and the effect names of several effects.</summary>
    private sealed record Record(int Id, string Name, string ClassName, EquatableArray<Constant> Fields, EquatableArray<TuningEntry> Tunings,
        EquatableArray<string> SharedNames);

    /// <summary>The game data's dungeon affix records and its build date.</summary>
    private sealed record Export(EquatableArray<Record> Records, string? BuildDate);

    /// <summary>A partial dungeon affix class: its namespace, name, the member names of the class and its base classes, its fields and where it is declared.</summary>
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
                    && NamespaceOf(c) is { } ns && ns.StartsWith(AffixNamespace, StringComparison.Ordinal),
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

    /// <summary>The partial class as a <see cref="Declared"/>, or null for a class not deriving from <c>DungeonAffix</c>.</summary>
    private static Declared? DeclaredOf(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var node = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol type) return null;
        bool affix = false;
        var members = new HashSet<string>(StringComparer.Ordinal) { type.Name };
        for (var t = type; t != null; t = t.BaseType)
        {
            if (!SymbolEqualityComparer.Default.Equals(t, type) && t.ToDisplayString() == AffixType) affix = true;
            foreach (var member in t.GetMembers()) members.Add(member.Name);
        }
        if (!affix) return null;
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

        var affixes = new List<DungeonAffixDocument>();
        var abilities = new Dictionary<int, AbilityDocument>();
        var effects = new Dictionary<int, EffectDocument>();
        var creatures = new List<CreatureDocument>();
        if (entities != null)
        {
            foreach (var line in entities.Lines)
            {
                ct.ThrowIfCancellationRequested();
                string text = line.ToString();
                // Only affix, ability, effect and creature lines are read in full.
                if (!text.StartsWith("{\"$type\":\"dungeon-affix\"", StringComparison.Ordinal)
                    && !text.StartsWith("{\"$type\":\"ability\"", StringComparison.Ordinal)
                    && !text.StartsWith("{\"$type\":\"effect\"", StringComparison.Ordinal)
                    && !text.StartsWith("{\"$type\":\"creature\"", StringComparison.Ordinal)) continue;
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
                    case DungeonAffixDocument a when (a.Name ?? "").Trim().Length > 0: affixes.Add(a); break;
                    case AbilityDocument b: abilities[b.Id] = b; break;
                    case EffectDocument e: effects[e.Id] = e; break;
                    case CreatureDocument c: creatures.Add(c); break;
                }
            }
        }

        var records = new List<Record>();
        foreach (var affix in affixes.OrderBy(a => a.Id))
        {
            string name = affix.Name!.Trim();
            var fields = new List<Constant>
            {
                new("AffixId", "int", Int(affix.Id), $"{Escape(name)}, dungeon affix {affix.Id} of the game data."),
                new("AffixName", "string", Literal(name), $"Game data: <c>name</c> of dungeon affix {affix.Id}."),
            };
            var tunings = new List<TuningEntry>();
            var effectIds = new List<int>();
            void AddEffects(IEnumerable<EntityReference>? references)
            {
                foreach (var r in references ?? [])
                {
                    if (r.Type == EntityType.Effect && !effectIds.Contains(r.Id)) effectIds.Add(r.Id);
                }
            }
            AddEffects(affix.Applies);

            var granted = (affix.Grants ?? []).Where(g => g.Type == EntityType.Ability).ToList();
            if (granted is [{ } grant] && abilities.TryGetValue(grant.Id, out var ability))
            {
                fields.Add(new("AffixAbilityId", "int", Int(ability.Id), $"Game data: <c>grants</c> of dungeon affix {affix.Id}, ability {ability.Id}."));
                AddEffects(ability.Applies);
                AddEffects(ability.Parts);
                tunings.AddRange(TuningsOf(ability, "", $"ability {ability.Id}"));
            }

            // The affix's creatures: a passive effect of theirs is an effect the affix applies.
            var applied = new HashSet<int>((affix.Applies ?? []).Where(r => r.Type == EntityType.Effect).Select(r => r.Id));
            foreach (var creature in creatures.Where(c => (c.Passives ?? []).Any(p => p.Ability is { Type: EntityType.Effect } e && applied.Contains(e.Id))).OrderBy(c => c.Id))
            {
                foreach (var entry in (creature.Abilities ?? []).Concat(creature.Passives ?? []))
                {
                    if (entry.Ability is not { } reference) continue;
                    if (reference.Type == EntityType.Effect)
                    {
                        if (!effectIds.Contains(reference.Id)) effectIds.Add(reference.Id);
                        continue;
                    }
                    if (reference.Type != EntityType.Ability || !abilities.TryGetValue(reference.Id, out var creatureAbility)) continue;
                    AddEffects(creatureAbility.Applies);
                    string abilityName = AbilityName(creatureAbility, effects);
                    tunings.AddRange(TuningsOf(creatureAbility, abilityName + ".", $"ability {creatureAbility.Id} of creature {creature.Id}"));
                    // Only an ability the creature casts has cast and cooldown constants.
                    if (!(creature.Abilities ?? []).Contains(entry)) continue;
                    string prefix = PascalCase(abilityName);
                    fields.Add(new(prefix + "AbilityId", "int", Int(creatureAbility.Id),
                        $"{Escape(abilityName)}, ability {creatureAbility.Id} of creature {creature.Id} ({Escape(creature.Name ?? "")}) of the game data."));
                    AddNumber(fields, prefix + "CastTime", creatureAbility.CastTime, $"Game data: <c>castTime</c> of ability {creatureAbility.Id}.");
                    string phase = entry.Phase is { Length: > 0 } p ? $", phase {Escape(p)}" : "";
                    AddCooldown(fields, prefix + "InitialCooldown", entry.InitialCooldown, $"Game data: <c>initialCooldown</c> of ability {creatureAbility.Id} of creature {creature.Id}{phase}.");
                    AddCooldown(fields, prefix + "Cooldown", entry.Cooldown, $"Game data: <c>cooldown</c> of ability {creatureAbility.Id} of creature {creature.Id}{phase}.");
                    AddCooldown(fields, prefix + "InterruptCooldown", entry.InterruptCooldown, $"Game data: <c>interruptCooldown</c> of ability {creatureAbility.Id} of creature {creature.Id}{phase}.");
                    AddCooldown(fields, prefix + "CcCooldown", entry.CCCooldown, $"Game data: <c>ccCooldown</c> of ability {creatureAbility.Id} of creature {creature.Id}{phase}.");
                }
            }

            var named = effectIds.Where(effects.ContainsKey).Select(id => effects[id]).Where(e => (e.Name ?? "").Trim().Length > 0).ToList();
            var shared = new List<string>();
            foreach (var group in named.GroupBy(e => PascalCase(e.Name!.Trim())))
            {
                if (group.Count() > 1)
                {
                    shared.Add($"{group.Key}|{string.Join(", ", group.Select(e => e.Id))}");
                    continue;
                }
                var effect = group.First();
                string prefix = group.Key;
                fields.Add(new(prefix + "EffectId", "int", Int(effect.Id), $"{Escape(effect.Name!.Trim())}, effect {effect.Id} of the game data."));
                if (effect.Infinite != true) AddNumber(fields, prefix + "Duration", effect.Duration, $"Game data: <c>duration</c> of effect {effect.Id}.");
                if (effect.MaxStacks is { } stacks) fields.Add(new(prefix + "MaxStacks", "int", Int(stacks), $"Game data: <c>maxStacks</c> of effect {effect.Id}."));
                var modifiers = (effect.Modifiers ?? []).Where(m => m.Value != null && (m.Attribute ?? "").Length > 0).ToList();
                foreach (var modifier in modifiers.GroupBy(m => m.Attribute!).Where(g => g.Count() == 1).Select(g => g.First()))
                {
                    AddNumber(fields, prefix + PascalCase(modifier.Attribute!), modifier.Value,
                        $"Game data: modifier <c>{Escape(modifier.Attribute!)}</c> ({Escape(modifier.Op ?? "")}) of effect {effect.Id}.");
                }
            }
            records.Add(new Record(affix.Id, name, PascalCase(name), new([.. fields]), new([.. tunings]), new([.. shared])));
        }
        return new Export(new([.. records]), buildDate);
    }

    /// <summary>The ability's name, or the name its applied effects share before their ':'; <c>Ability&lt;Id&gt;</c> when neither is.</summary>
    private static string AbilityName(AbilityDocument ability, Dictionary<int, EffectDocument> effects)
    {
        if ((ability.Name ?? "").Trim() is { Length: > 0 } own) return own;
        var heads = (ability.Applies ?? []).Where(r => r.Type == EntityType.Effect && effects.ContainsKey(r.Id))
            .Select(r => effects[r.Id].Name ?? "").Where(n => n.Contains(':')).Select(n => n.Substring(0, n.IndexOf(':')).Trim()).Distinct().ToList();
        return heads is [{ } head] ? head : $"Ability{ability.Id}";
    }

    /// <summary>The ability's tuning keys, each after <paramref name="prefix"/>, in the order of the game data.</summary>
    private static List<TuningEntry> TuningsOf(AbilityDocument ability, string prefix, string owner) =>
        TuningNames.Entries(ability.Tuning, v => ValueOf(v) is { } value ? (value.Type, value.Literal, false) : null, t => SourceOf(t, owner))
            .Select(e => e with { NameKey = prefix + e.NameKey, Source = e.Source })
            .ToList();

    /// <summary>The C# type and literal of a tuning value; null for a value of no constant type.</summary>
    private static (string Type, string Literal)? ValueOf(object? value)
    {
        if (value is not JsonElement element) return null;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                string raw = element.GetRawText();
                if (element.TryGetInt32(out int whole) && raw.IndexOfAny(['.', 'e', 'E']) < 0) return ("int", Int(whole));
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

    /// <summary>The ability holding the tuning value, its owner, and its attribute and operation where it has them.</summary>
    private static string SourceOf(TuningReference tuning, string ability)
    {
        var parts = new List<string> { ability };
        if (tuning.Owner is { } owner)
        {
            string type = owner.Type.ToString().ToLowerInvariant();
            parts.Add(owner.Name is { Length: > 0 } ownerName ? $"{ownerName} ({type} {owner.Id})" : $"{type} {owner.Id}");
        }
        if (tuning.Attribute is { Length: > 0 } attribute) parts.Add(tuning.Op is { Length: > 0 } op ? $"{attribute} {op}" : attribute);
        return string.Join(", ", parts);
    }

    private static void AddNumber(List<Constant> fields, string name, double? value, string summary)
    {
        if (value is not { } v) return;
        bool whole = v == Math.Floor(v) && Math.Abs(v) <= int.MaxValue;
        fields.Add(new Constant(name, whole ? "int" : "double", whole ? Int((int)v) : Number(v), summary));
    }

    /// <summary>A creature ability's cooldown of a fixed value.</summary>
    private static void AddCooldown(List<Constant> fields, string name, PhaseCooldown? cooldown, string summary)
    {
        if (cooldown is { Type: "FixedValue", Seconds: { } seconds }) AddNumber(fields, name, seconds, summary);
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
            foreach (string shared in record.SharedNames)
            {
                var parts = shared.Split('|');
                context.ReportDiagnostic(Diagnostic.Create(SharedEffectName, d.Location, record.Id, parts[1], parts[0] + "EffectId"));
            }
            var members = new HashSet<string>(d.Members, StringComparer.Ordinal);
            var sharedTunings = new List<(string Name, string First, string Second)>();
            var tunings = TuningNames.Of(record.Tunings, [], [], sharedTunings);
            TuningNames.Report(context, d.Location, d.Name, $"dungeon affix {record.Id}", sharedTunings, d.Fields, tunings);

            var source = new StringBuilder()
                .Append("// <auto-generated> by FellowshipSim.Codex.Generators from the game data")
                .Append(export.BuildDate != null ? $" of {export.BuildDate}" : "").Append(". </auto-generated>\n")
                .Append("#nullable enable\n")
                .Append("namespace ").Append(d.Namespace).Append(";\n\n")
                .Append("partial class ").Append(d.Name).Append("\n{\n");
            bool first = true;
            foreach (var constant in record.Fields)
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
            if (tunings.Count > 0)
            {
                if (members.Contains("Constants"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DeclaredName, d.Location, d.Name, "Constants", record.Id));
                }
                else
                {
                    TuningNames.Write(source, $"Game data: the tuning values of dungeon affix {record.Id}'s abilities.",
                        tunings.Select(t => (t, $"Game data: tuning <c>{Escape(t.Keys)}</c> of {Escape(t.Source)}.")),
                        false, !first);
                }
            }
            source.Append("}\n");
            context.AddSource($"DungeonAffixes.{d.Name}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
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
            // An apostrophe stays inside its word: Storm's is Storms.
            else if (c != '\'') start = true;
        }
        return result.ToString();
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
