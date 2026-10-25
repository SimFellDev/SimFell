using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FellowshipSim.Core.Generators;

/// <summary>
/// Writes the constructors <c>Catalog</c> uses: for each hero, talent, augmentation, glyph, weapon, relic passive and legendary class
/// with a public constructor without parameters, a switch from its ID to <c>new</c> of the class. The IDs are read from
/// the class's overrides of <c>Id</c>, <c>WeaponAbilityId</c>, <c>ItemIds</c> and <c>HeroName</c>,
/// which must be constants or lists of constants. An ID of 0 means unknown, and the class has no entry.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class CatalogGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor NotConstant = new(
        "SIMFELL001", "Catalog ID is not a constant",
        "{0}.{1} must be a constant or a list of constants for the catalog", "Catalog", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor SharedId = new(
        "SIMFELL002", "Two classes share a catalog ID",
        "{0} and {1} share the {2} ID {3}", "Catalog", DiagnosticSeverity.Error, true);

    private enum Kind { Hero, Talent, Augmentation, Glyph, Weapon, RelicPassive, Legendary }

    private static readonly (string Base, Kind Kind)[] Bases =
    [
        ("FellowshipSim.Core.Domain.Hero", Kind.Hero),
        ("FellowshipSim.Core.Domain.Talent", Kind.Talent),
        ("FellowshipSim.Core.Customization.Augmentations.Augmentation", Kind.Augmentation),
        ("FellowshipSim.Core.Customization.Glyphs.Glyph", Kind.Glyph),
        ("FellowshipSim.Core.Customization.Weapons.Weapon", Kind.Weapon),
        ("FellowshipSim.Core.Customization.RelicPassives.RelicPassive", Kind.RelicPassive),
        ("FellowshipSim.Core.Customization.Legendaries.Legendary", Kind.Legendary),
    ];

    /// <summary>One catalog class: its kind, full name, IDs, and a name (a hero's class name, a glyph's or legendary hero's name).</summary>
    /// <remarks>A class with value equality, so the generator's cache compares entries by value.</remarks>
    private sealed class Entry(Kind kind, string typeName, string shortName, string ids, string name, string? error, Location? errorLocation)
        : System.IEquatable<Entry>
    {
        public Kind Kind { get; } = kind;
        public string TypeName { get; } = typeName;
        public string ShortName { get; } = shortName;
        public string Ids { get; } = ids;
        public string Name { get; } = name;
        public string? Error { get; } = error;
        public Location? ErrorLocation { get; } = errorLocation;

        public int[] IdValues => Ids.Length == 0 ? [] : Ids.Split(',').Select(int.Parse).ToArray();

        public bool Equals(Entry? other) =>
            other != null && Kind == other.Kind && TypeName == other.TypeName && ShortName == other.ShortName && Ids == other.Ids
            && Name == other.Name && Error == other.Error && Equals(ErrorLocation, other.ErrorLocation);

        public override bool Equals(object? obj) => Equals(obj as Entry);

        public override int GetHashCode() => TypeName.GetHashCode() ^ Ids.GetHashCode();
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entries = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null } c
                                    && !c.Modifiers.Any(SyntaxKind.AbstractKeyword) && c.TypeParameterList == null,
                static (ctx, ct) => EntryOf(ctx, ct))
            .Where(static e => e != null)
            .Collect();
        context.RegisterSourceOutput(entries, static (spc, list) => Emit(spc, list!));
    }

    private static Entry? EntryOf(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol type || type.IsAbstract) return null;
        if (!type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public)) return null;
        Kind? kind = null;
        for (var b = type.BaseType; b != null && kind == null; b = b.BaseType)
        {
            string name = b.ToDisplayString();
            foreach (var (baseName, k) in Bases)
            {
                if (name == baseName) kind = k;
            }
        }
        if (kind == null) return null;

        string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string ids = "", name2 = "";
        string? error = null;
        Location? location = null;
        bool Read(string property, out object[] values)
        {
            values = ConstantsOf(ctx.SemanticModel.Compilation, type, property, ct, out var at);
            if (values.Length > 0 || at == null) return true;
            error = property;
            location = at;
            return false;
        }

        switch (kind.Value)
        {
            case Kind.Hero:
                name2 = type.Name;
                break;
            case Kind.Talent:
            case Kind.Augmentation:
            case Kind.Glyph:
            case Kind.Weapon:
            case Kind.RelicPassive:
            case Kind.Legendary:
                string idProperty = kind switch
                {
                    Kind.Weapon => "WeaponAbilityId",
                    Kind.Legendary => "ItemIds",
                    _ => "Id",
                };
                if (Read(idProperty, out var idValues)) ids = string.Join(",", idValues.Select(v => (int)v));
                if (kind == Kind.Glyph && error == null && Read("Name", out var names)) name2 = (string)names[0];
                if (kind == Kind.Legendary && error == null && Read("HeroName", out var heroes)) name2 = ((string)heroes[0]).ToLowerInvariant();
                break;
        }
        return new Entry(kind.Value, typeName, type.Name, ids, name2, error, location);
    }

    /// <summary>
    /// The constant values of <paramref name="property"/> on <paramref name="type"/> (its most derived override): one value,
    /// or each element of a collection or array. Empty with <paramref name="at"/> set when a value is not a constant.
    /// </summary>
    private static object[] ConstantsOf(Compilation compilation, INamedTypeSymbol type, string property, System.Threading.CancellationToken ct, out Location? at)
    {
        at = type.Locations.FirstOrDefault();
        for (var t = type; t != null; t = t.BaseType)
        {
            var symbol = t.GetMembers(property).OfType<IPropertySymbol>().FirstOrDefault(p => !p.IsAbstract);
            if (symbol == null) continue;
            if (symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct) is not PropertyDeclarationSyntax syntax) return [];
            at = syntax.GetLocation();
            var body = syntax.ExpressionBody?.Expression
                       ?? syntax.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration))?.ExpressionBody?.Expression;
            if (body == null) return [];
            var model = compilation.GetSemanticModel(syntax.SyntaxTree);
            IEnumerable<ExpressionSyntax> elements = body switch
            {
                CollectionExpressionSyntax c => c.Elements.Select(e => e is ExpressionElementSyntax x ? x.Expression : null!),
                ArrayCreationExpressionSyntax { Initializer: { } i } => i.Expressions,
                ImplicitArrayCreationExpressionSyntax i => i.Initializer.Expressions,
                _ => [body],
            };
            var values = new List<object>();
            foreach (var element in elements)
            {
                if (element == null) return [];
                var constant = model.GetConstantValue(element, ct);
                if (!constant.HasValue || constant.Value is not (int or string)) return [];
                values.Add(constant.Value);
            }
            at = null;
            return values.ToArray();
        }
        return [];
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<Entry> found)
    {
        var entries = new List<Entry>();
        var seen = new HashSet<string>();
        foreach (var entry in found)
        {
            if (!seen.Add(entry.TypeName)) continue;
            if (entry.Error != null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(NotConstant, entry.ErrorLocation, entry.ShortName, entry.Error));
                continue;
            }
            entries.Add(entry);
        }

        // Classes by ID, in declaration order; an ID of 0 is unknown.
        var byKind = new Dictionary<Kind, List<(int Id, Entry Entry)>>();
        foreach (var kind in new[] { Kind.Talent, Kind.Augmentation, Kind.Glyph, Kind.Weapon, Kind.RelicPassive })
        {
            var list = new List<(int, Entry)>();
            var owners = new Dictionary<int, Entry>();
            foreach (var entry in entries.Where(e => e.Kind == kind))
            {
                foreach (int id in entry.IdValues)
                {
                    if (id == 0) continue;
                    if (owners.TryGetValue(id, out var other))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(SharedId, null, entry.ShortName, other.ShortName, kind.ToString().ToLowerInvariant(), id));
                        continue;
                    }
                    owners[id] = entry;
                    list.Add((id, entry));
                }
            }
            byKind[kind] = list;
        }
        var legendaries = new List<(string Hero, int ItemId, Entry Entry)>();
        var legendaryKeys = new Dictionary<(string, int), Entry>();
        foreach (var entry in entries.Where(e => e.Kind == Kind.Legendary))
        {
            foreach (int id in entry.IdValues)
            {
                if (legendaryKeys.TryGetValue((entry.Name, id), out var other))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(SharedId, null, entry.ShortName, other.ShortName, "legendary item", id));
                    continue;
                }
                legendaryKeys[(entry.Name, id)] = entry;
                legendaries.Add((entry.Name, id, entry));
            }
        }

        var s = new StringBuilder();
        s.AppendLine("// <auto-generated/> by FellowshipSim.Core.Generators.CatalogGenerator");
        s.AppendLine("#nullable enable");
        s.AppendLine("namespace FellowshipSim.Core;");
        s.AppendLine();
        s.AppendLine("partial class Catalog");
        s.AppendLine("{");

        var heroes = entries.Where(e => e.Kind == Kind.Hero).ToList();
        s.AppendLine("    private static (string Name, global::System.Type Type)[] HeroClasses =>");
        s.AppendLine("    [");
        foreach (var h in heroes) s.AppendLine($"        ({Literal(h.Name)}, typeof({h.TypeName})),");
        s.AppendLine("    ];");
        s.AppendLine();
        s.AppendLine("    private static global::FellowshipSim.Core.Domain.Hero? NewHero(string name)");
        s.AppendLine("    {");
        foreach (var h in heroes)
        {
            s.AppendLine($"        if (string.Equals(name, {Literal(h.Name)}, global::System.StringComparison.OrdinalIgnoreCase)) return new {h.TypeName}();");
        }
        s.AppendLine("        return null;");
        s.AppendLine("    }");
        s.AppendLine();

        Switch(s, "global::FellowshipSim.Core.Domain.Talent", "NewTalent", "int id", byKind[Kind.Talent], "");
        Switch(s, "global::FellowshipSim.Core.Customization.Augmentations.Augmentation", "NewAugmentation", "int id, int rank", byKind[Kind.Augmentation], " { Rank = rank }");
        Switch(s, "global::FellowshipSim.Core.Customization.Glyphs.Glyph", "NewGlyph", "int id, int rank", byKind[Kind.Glyph], " { Rank = rank }");
        Switch(s, "global::FellowshipSim.Core.Customization.Weapons.Weapon", "NewWeapon", "int id", byKind[Kind.Weapon], "");
        Switch(s, "global::FellowshipSim.Core.Customization.RelicPassives.RelicPassive", "NewRelicPassive", "int id", byKind[Kind.RelicPassive], "");

        s.AppendLine("    private static global::FellowshipSim.Core.Customization.Legendaries.Legendary? NewLegendary(string hero, int itemId) => (hero, itemId) switch");
        s.AppendLine("    {");
        foreach (var (hero, itemId, entry) in legendaries) s.AppendLine($"        ({Literal(hero)}, {itemId}) => new {entry.TypeName}(),");
        s.AppendLine("        _ => null,");
        s.AppendLine("    };");
        s.AppendLine();

        s.AppendLine("    private static int GlyphIdOf(string name)");
        s.AppendLine("    {");
        foreach (var (id, entry) in byKind[Kind.Glyph])
        {
            s.AppendLine($"        if (string.Equals(name, {Literal(entry.Name)}, global::System.StringComparison.OrdinalIgnoreCase)) return {id};");
        }
        s.AppendLine("        return 0;");
        s.AppendLine("    }");
        s.AppendLine();

        Types(s, "AugmentationClasses", Distinct(byKind[Kind.Augmentation]));
        Types(s, "GlyphClasses", Distinct(byKind[Kind.Glyph]));
        Types(s, "WeaponClasses", Distinct(byKind[Kind.Weapon]));
        Types(s, "RelicPassiveClasses", Distinct(byKind[Kind.RelicPassive]));
        Instances(s, "NewAugmentations", "global::FellowshipSim.Core.Customization.Augmentations.Augmentation", Distinct(byKind[Kind.Augmentation]));
        Instances(s, "NewGlyphs", "global::FellowshipSim.Core.Customization.Glyphs.Glyph", Distinct(byKind[Kind.Glyph]));
        Instances(s, "NewWeapons", "global::FellowshipSim.Core.Customization.Weapons.Weapon", Distinct(byKind[Kind.Weapon]));
        Instances(s, "NewRelicPassives", "global::FellowshipSim.Core.Customization.RelicPassives.RelicPassive", Distinct(byKind[Kind.RelicPassive]));
        s.AppendLine("    private static (string Hero, global::System.Type Type)[] LegendaryClasses =>");
        s.AppendLine("    [");
        foreach (var (hero, _, entry) in legendaries) s.AppendLine($"        ({Literal(hero)}, typeof({entry.TypeName})),");
        s.AppendLine("    ];");
        s.AppendLine("}");

        spc.AddSource("Catalog.g.cs", SourceText.From(s.ToString(), Encoding.UTF8));
    }

    private static void Switch(StringBuilder s, string returnType, string method, string parameters, List<(int Id, Entry Entry)> classes, string initializer)
    {
        s.AppendLine($"    private static {returnType}? {method}({parameters}) => id switch");
        s.AppendLine("    {");
        foreach (var (id, entry) in classes) s.AppendLine($"        {id} => new {entry.TypeName}(){initializer},");
        s.AppendLine("        _ => null,");
        s.AppendLine("    };");
        s.AppendLine();
    }

    private static void Types(StringBuilder s, string field, IEnumerable<Entry> classes)
    {
        s.AppendLine($"    private static global::System.Type[] {field} =>");
        s.AppendLine("    [");
        foreach (var entry in classes) s.AppendLine($"        typeof({entry.TypeName}),");
        s.AppendLine("    ];");
        s.AppendLine();
    }

    private static void Instances(StringBuilder s, string method, string baseType, IEnumerable<Entry> classes)
    {
        s.AppendLine($"    private static {baseType}[] {method}() =>");
        s.AppendLine("    [");
        foreach (var entry in classes) s.AppendLine($"        new {entry.TypeName}(),");
        s.AppendLine("    ];");
        s.AppendLine();
    }

    private static List<Entry> Distinct(List<(int Id, Entry Entry)> classes)
    {
        var seen = new HashSet<string>();
        return classes.Where(c => seen.Add(c.Entry.TypeName)).Select(c => c.Entry).ToList();
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);
}
