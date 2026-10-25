using System.Globalization;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.UI.Apl;

/// <summary>Kinds, paths and text of the condition builder's rows.</summary>
public static class ConditionBuilder
{
    public static readonly IReadOnlyList<string> Comparisons = [">=", ">", "=", "!=", "<=", "<"];

    public const string ResourceKind = "resource";
    public const string NamedKind = "named";
    public const string GroupKind = "group";

    /// <summary>The kinds of value a row reads: each scope of the vocabulary, a resource, and a named fight value.</summary>
    public static IReadOnlyList<ConditionKind> Kinds(AplVocabulary v)
    {
        var scopeNames = v.ScopeNames;
        return
        [
            .. v.Scopes.Select(scope => new ConditionKind(scope.Key, scope.Key switch
            {
                PropertyExpression.BuffScope => "Buff",
                PropertyExpression.DebuffScope => "Debuff on target",
                PropertyExpression.CooldownScope => "Cooldown",
                _ => scope.Key,
            }, scopeNames[scope.Key], scope.Value.ToList())),
            new ConditionKind(ResourceKind, "Resource", v.Resources),
            new ConditionKind(NamedKind, "Fight value", v.Named.Select(n => new AplName(n, n)).ToList()),
        ];
    }

    /// <summary>"buff.ice_blitz.up" -> (buff, ice_blitz, up); a resource or a named value has no property.</summary>
    public static PathParts SplitPath(AplVocabulary v, string path)
    {
        string[] parts = path.Split('.');
        if (parts.Length >= 3 && v.Scopes.ContainsKey(parts[0])) return new PathParts(parts[0], parts[1], string.Join(".", parts[2..]));
        string resource = path.StartsWith(v.ResourcePrefix, StringComparison.Ordinal) ? path[v.ResourcePrefix.Length..] : path;
        if (v.Resources.Any(r => r.Slug == resource)) return new PathParts(ResourceKind, resource);
        return new PathParts(NamedKind, path);
    }

    public static string JoinPath(AplVocabulary v, PathParts parts) =>
        v.Scopes.ContainsKey(parts.Kind) ? $"{parts.Kind}.{parts.Name}.{parts.Property}" : parts.Name;

    /// <summary>A row with a kind and no name yet, and a group with no such row, are in the builder only: the text has what is picked.</summary>
    public static bool IsComplete(ConditionRow row) => row.Items is { } items ? items.Any(IsComplete) : !string.IsNullOrEmpty(row.Path);

    /// <summary>
    /// Rows as text: each join as its word, a group in parentheses, 'not' before a value or a group. 'not' binds to one value,
    /// so a negated comparison is in parentheses.
    /// </summary>
    public static string Text(IReadOnlyList<ConditionRow> rows) =>
        string.Concat(rows.Where(IsComplete).Select((row, i) =>
        {
            string value = row.Items is { } items
                ? $"({Text(items)})"
                : !string.IsNullOrEmpty(row.Comparison)
                    ? $"{row.Path} {row.Comparison} {Number(row.Value ?? 0)}"
                    : row.Path!;
            string negated = !row.Not ? value : !string.IsNullOrEmpty(row.Comparison) ? $"not ({value})" : $"not {value}";
            return i == 0 ? negated : $" {(string.IsNullOrEmpty(row.Join) ? "and" : row.Join)} {negated}";
        }));

    /// <summary>A number as the page writes it.</summary>
    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>The rows of the group at <paramref name="path"/> (indexes of groups from the top).</summary>
    public static List<ConditionRow> GroupAt(List<ConditionRow> rows, IReadOnlyList<int> path) =>
        path.Aggregate(rows, (items, index) => items[index].Items!);

    /// <summary>Aliases share a name ("Winter Orbs" is worb and winter_orbs), so a shared name shows its slug.</summary>
    public static string NameLabel(ConditionKind kind, AplName name) =>
        kind.Names.Count(other => other.Name == name.Name) > 1 ? $"{name.Name} ({name.Slug})" : name.Name;
}
