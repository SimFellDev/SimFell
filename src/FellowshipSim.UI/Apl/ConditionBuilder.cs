using System.Globalization;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.UI.Apl;

/// <summary>Kinds, paths and text of the condition builder's rows.</summary>
public static class ConditionBuilder
{
    public static readonly IReadOnlyList<string> Comparisons = [">=", ">", "=", "!=", "<=", "<"];

    /// <summary>Arithmetic as action lists write it: '%' divides and '%%' is the remainder.</summary>
    public static readonly IReadOnlyList<string> Operators =
        [.. new[] { BinaryOperator.Add, BinaryOperator.Subtract, BinaryOperator.Multiply, BinaryOperator.Divide, BinaryOperator.Modulo }.Select(o => o.Symbol())];

    /// <summary>The prefixes a term can have: each prefix operator's symbol.</summary>
    public static readonly IReadOnlyList<string> Prefixes = [.. Enum.GetValues<UnaryOperator>().Select(o => o.Symbol())];

    public const string ResourceKind = "resource";
    public const string NamedKind = "named";
    public const string ActionValueKind = "action_value";
    public const string NumberKind = "number";
    /// <summary>A group of rows in a condition, or a condition in parentheses on a side of a row.</summary>
    public const string GroupKind = "group";
    /// <summary>The kind of a <see cref="ValuePick"/> that adds a prefix to its term.</summary>
    public const string PrefixKind = "prefix";

    /// <summary>A resource's property for its current amount, written as the resource's name alone.</summary>
    public const string CurrentProperty = "current";
    /// <summary>A resource's property for the room left in it ("spirit.deficit").</summary>
    public const string DeficitProperty = "deficit";

    /// <summary>
    /// The kinds of value a term reads: each scope of the vocabulary, a resource, a named fight value, the action's own
    /// values, each part of the build, the enemies with a debuff, and the list's variables (<paramref name="variables"/>).
    /// </summary>
    public static IReadOnlyList<ConditionKind> Kinds(AplVocabulary v, IReadOnlyList<string> variables)
    {
        var scopeNames = v.ScopeNames;
        var activeDot = PropertyExpression.ScopeDocs[PropertyExpression.ActiveDotScope];
        var variable = PropertyExpression.ScopeDocs[PropertyExpression.VariableScope];
        return
        [
            .. v.Scopes.Select(scope => new ConditionKind(scope.Key, PropertyExpression.ScopeDocs[scope.Key].Label, scope.Key switch
            {
                PropertyExpression.BuffScope => AplTokenKind.Buff,
                PropertyExpression.DebuffScope or PropertyExpression.DotScope => AplTokenKind.Debuff,
                PropertyExpression.CooldownScope or PropertyExpression.ActionScope => AplTokenKind.Cooldown,
                _ => AplTokenKind.Named,
            }, scopeNames[scope.Key], scope.Value.ToList(), Description: PropertyExpression.ScopeDocs[scope.Key].Description)),
            new ConditionKind(ResourceKind, Resource.Label, AplTokenKind.Resource, v.Resources, [CurrentProperty, DeficitProperty], Description: Resource.Description),
            new ConditionKind(NamedKind, FightValue.Label, AplTokenKind.Named,
                v.Named.Select(n => Described(n, PropertyExpression.Named[n].Doc)).ToList(),
                Description: FightValue.Description),
            new ConditionKind(ActionValueKind, ThisAction.Label, AplTokenKind.Named,
                v.ActionValues.Select(n => Described(n, PropertyExpression.ActionValueDoc(n)!)).ToList(),
                Description: ThisAction.Description),
            .. v.BuildNames.Select(scope => new ConditionKind(scope.Key, PropertyExpression.BuildScopes[scope.Key].Doc.Label, AplTokenKind.Named, scope.Value,
                MatchesStart: scope.Key == PropertyExpression.WeaponScope, Description: PropertyExpression.BuildScopes[scope.Key].Doc.Description)),
            new ConditionKind(PropertyExpression.ActiveDotScope, activeDot.Label, AplTokenKind.Debuff, v.Auras, Description: activeDot.Description),
            new ConditionKind(PropertyExpression.VariableScope, variable.Label, AplTokenKind.Named, variables.Select(n => new AplName(n, n)).ToList(),
                Description: variable.Description),
        ];
    }

    private static AplName Described(string slug, AplDoc doc) => new(slug, doc.Label, doc.Description);

    /// <summary>The resource kind: each resource by its name, or after "resource.".</summary>
    public static readonly AplDoc Resource = new("Resource", "One of the hero's resources, written as its name alone or after resource.");

    /// <summary>The fight value kind: the values in <see cref="PropertyExpression.Named"/>.</summary>
    public static readonly AplDoc FightValue = new("Fight value", "A value of the fight or the hero, written as its whole path.");

    /// <summary>The kind of an action's own values: the values in <see cref="PropertyExpression.ActionValues"/>.</summary>
    public static readonly AplDoc ThisAction = new("This action", "A value of the action's own ability, written with no scope.");

    /// <summary>The doc of a property of a kind: a scope's property, or a resource's current amount or deficit; null for any other.</summary>
    public static AplDoc? PropertyDoc(string kind, string property) => kind == ResourceKind
        ? property switch
        {
            CurrentProperty => PropertyExpression.ResourceCurrent,
            DeficitProperty => PropertyExpression.ResourceDeficit,
            _ => null,
        }
        : PropertyExpression.PropertyDoc(kind, property);

    /// <summary>The names of the variables the lists' variable actions set, in order of their first action.</summary>
    public static IReadOnlyList<string> Variables(IEnumerable<ActionList> lists) =>
        lists.SelectMany(l => l.Actions)
            .Where(a => a.Ability == AplVariable.ActionName && a.Arguments != null)
            .Select(a => a.Arguments!.Split(',').Select(p => p.Split('=', 2))
                .FirstOrDefault(p => p.Length == 2 && p[0].Trim().Equals(AplVariable.NameKey, StringComparison.OrdinalIgnoreCase))?[1].Trim())
            .OfType<string>()
            .Where(n => n.Length > 0)
            .Distinct()
            .ToList();

    /// <summary>
    /// "buff.ice_blitz.up" -> (buff, ice_blitz, up); "talent.slow_burn" -> (talent, slow_burn); "spirit.deficit" ->
    /// (resource, spirit, deficit); an action's own value or a named value is its whole path.
    /// </summary>
    public static PathParts SplitPath(AplVocabulary v, string path)
    {
        string[] parts = path.Split('.');
        if (parts.Length >= 3 && v.Scopes.ContainsKey(parts[0])) return new PathParts(parts[0], parts[1], string.Join(".", parts[2..]));
        if (parts.Length == 2 && IsNameScope(v, parts[0])) return new PathParts(parts[0], parts[1]);
        string resource = path.StartsWith(v.ResourcePrefix, StringComparison.Ordinal) ? path[v.ResourcePrefix.Length..] : path;
        bool deficit = resource.EndsWith(v.DeficitSuffix, StringComparison.Ordinal);
        if (deficit) resource = resource[..^v.DeficitSuffix.Length];
        if (v.Resources.Any(r => r.Slug == resource)) return new PathParts(ResourceKind, resource, deficit ? DeficitProperty : CurrentProperty);
        if (v.ActionValues.Contains(path)) return new PathParts(ActionValueKind, path);
        return new PathParts(NamedKind, path);
    }

    public static string JoinPath(AplVocabulary v, PathParts parts) => parts.Kind switch
    {
        _ when v.Scopes.ContainsKey(parts.Kind) => $"{parts.Kind}.{parts.Name}.{parts.Property}",
        _ when IsNameScope(v, parts.Kind) => $"{parts.Kind}.{parts.Name}",
        ResourceKind when parts.Property == DeficitProperty => parts.Name + v.DeficitSuffix,
        _ => parts.Name,
    };

    /// <summary>Scopes of "&lt;scope&gt;.&lt;name&gt;" paths: the build's parts, the enemies with a debuff, and variables.</summary>
    private static bool IsNameScope(AplVocabulary v, string scope) =>
        v.BuildNames.ContainsKey(scope) || scope is PropertyExpression.ActiveDotScope or PropertyExpression.VariableScope;

    /// <summary>
    /// A term with no value picked yet, parentheses with no picked term, a row with no picked term on its left, and a group
    /// with no such row, are in the builder only: the text has what is picked.
    /// </summary>
    public static bool IsComplete(ConditionRow row) => row.Items is { } items ? items.Any(IsComplete) : row.Left is { } left && left.Any(IsComplete);

    public static bool IsComplete(ConditionTerm term) =>
        term.Items is { } items ? items.Any(IsComplete) : !string.IsNullOrEmpty(term.Path) || term.Number != null;

    /// <summary>
    /// Changes <paramref name="term"/> by a pick: a prefix goes before its prefixes, a number starts at 0, parentheses hold the
    /// term as it was, and a name keeps the property when its kind lists it, else takes the kind's first.
    /// </summary>
    public static void Apply(ConditionTerm term, ValuePick pick, AplVocabulary v, IReadOnlyList<ConditionKind> kinds)
    {
        if (pick.Prefix != null)
        {
            term.Prefix = pick.Prefix + term.Prefix;
            return;
        }
        if (pick.Kind == NumberKind)
        {
            term.Path = null;
            term.Number = 0;
            term.Items = null;
            return;
        }
        if (pick.Kind == GroupKind)
        {
            var held = term.Clone();
            held.Operator = null;
            held.Prefix = null;
            term.Path = null;
            term.Number = null;
            term.Items = [new ConditionRow { Left = [held] }];
            return;
        }
        var kind = kinds.First(k => k.Id == pick.Kind);
        string? property = !string.IsNullOrEmpty(term.Path) && SplitPath(v, term.Path) is { } current && current.Kind == kind.Id
                           && kind.Properties?.Contains(current.Property!) == true
            ? current.Property
            : kind.Properties?.FirstOrDefault();
        term.Path = JoinPath(v, new PathParts(kind.Id, pick.Name ?? "", property));
        term.Number = null;
        term.Items = null;
    }

    /// <summary>The row a group's empty row becomes by a pick: a group, a number, or a name with its kind's first property.</summary>
    public static ConditionRow RowFor(ValuePick pick, string join, bool not, AplVocabulary v, IReadOnlyList<ConditionKind> kinds) => pick.Kind switch
    {
        GroupKind => new ConditionRow { Join = join, Not = not, Items = [] },
        NumberKind => new ConditionRow { Join = join, Not = not, Left = [new ConditionTerm { Number = 0 }] },
        _ => new ConditionRow
        {
            Join = join, Not = not,
            Left = [new ConditionTerm { Path = JoinPath(v, new PathParts(pick.Kind, pick.Name ?? "", kinds.First(k => k.Id == pick.Kind).Properties?.FirstOrDefault())) }],
        },
    };

    /// <summary>Every group in the rows, at every depth, in the rows and in each condition in parentheses on a side.</summary>
    public static IEnumerable<ConditionRow> Groups(IEnumerable<ConditionRow> rows) => rows.SelectMany(row =>
        row.Items is { } items
            ? Groups(items).Prepend(row)
            : (row.Left ?? []).Concat(row.Comparisons.SelectMany(c => c.Terms)).SelectMany(t => t.Items is { } inner ? Groups(inner) : []));

    /// <summary>
    /// Rows as text: each join as its word, a group in parentheses, 'not' before a value or a group. 'not' binds to one value,
    /// so a negated comparison or arithmetic is in parentheses. A comparison with no terms on its side yet compares to 0.
    /// </summary>
    public static string Text(IReadOnlyList<ConditionRow> rows) =>
        string.Concat(rows.Where(IsComplete).Select((row, i) =>
        {
            bool compared = row.Items == null && row.Comparisons.Count > 0;
            string value = row.Items is { } items
                ? $"({Text(items)})"
                : Text(row.Left!) + string.Concat(row.Comparisons.Select(c =>
                    $" {c.Comparison} {(c.Terms.Any(IsComplete) ? Text(c.Terms) : Number(0))}"));
            string negated = !row.Not ? value : compared || row.Left?.Count(IsComplete) > 1 ? $"not ({value})" : $"not {value}";
            return i == 0 ? negated : $" {(string.IsNullOrEmpty(row.Join) ? "and" : row.Join)} {negated}";
        }));

    /// <summary>
    /// Terms as text: each operator with a space on each side, each prefix before its term ('!' as "not "), a condition in
    /// parentheses. The first term written has no operator.
    /// </summary>
    public static string Text(IReadOnlyList<ConditionTerm> terms) =>
        string.Concat(terms.Where(IsComplete).Select((term, i) =>
        {
            string value = term.Items is { } items ? $"({Text(items)})" : !string.IsNullOrEmpty(term.Path) ? term.Path : Number(term.Number!.Value);
            string prefixed = string.Concat(PrefixOperators(term.Prefix).Select(op => AplFormatter.Prefix(op, AplOperatorStyle.Words))) + value;
            return i == 0 ? prefixed : $" {(string.IsNullOrEmpty(term.Operator) ? "+" : term.Operator)} {prefixed}";
        }));

    /// <summary>The prefix operators of a term's prefix, outermost first.</summary>
    public static IEnumerable<UnaryOperator> PrefixOperators(string? prefix) =>
        (prefix ?? "").Select(symbol => Enum.GetValues<UnaryOperator>().First(op => op.Symbol() == symbol.ToString()));

    /// <summary>A prefix as the editor shows it: "!-" is "not -".</summary>
    public static string PrefixLabel(string prefix) =>
        string.Join(" ", PrefixOperators(prefix).Select(op => AplFormatter.Prefix(op, AplOperatorStyle.Words).Trim()));

    /// <summary>A number as the page writes it.</summary>
    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>True when <paramref name="kind"/> lists <paramref name="name"/>, or a name it starts where the kind matches starts.</summary>
    public static bool Knows(ConditionKind kind, string name) =>
        kind.Names.Any(n => kind.MatchesStart ? n.Slug.StartsWith(name, StringComparison.Ordinal) : n.Slug == name);

    /// <summary>Aliases share a name ("Winter Orbs" is worb and winter_orbs), so a shared name shows its slug.</summary>
    public static string NameLabel(ConditionKind kind, AplName name) =>
        kind.Names.Count(other => other.Name == name.Name) > 1 ? $"{name.Name} ({name.Slug})" : name.Name;
}
