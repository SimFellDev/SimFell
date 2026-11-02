using System.Runtime.CompilerServices;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.UI.Apl;

/// <summary>The completion list of the code fields: the parts a condition can read after the word at the caret.</summary>
public static class Completions
{
    private static readonly ConditionalWeakTable<AplVocabulary, CompletionNode> Trees = new();

    private static readonly string[] Operators = ["and", "or", "not"];

    /// <summary>
    /// Every path a condition can read, as a tree of its '.'-separated parts: scope, name, property; a resource, with or
    /// without its prefix, and its deficit; a named value; the action's own values; each part of the build; the enemies
    /// with a debuff; a previous action; each math function's name; and the words 'and', 'or' and 'not'. Each node has
    /// the kind its text is coloured by. <see cref="For"/> adds the list's variables.
    /// </summary>
    public static CompletionNode TreeOf(AplVocabulary v) => Trees.GetValue(v, Build);

    private static CompletionNode Build(AplVocabulary v)
    {
        var root = new CompletionNode(null);
        void Add(IEnumerable<string> parts, AplTokenKind kind, string detail = "")
        {
            var node = root;
            var list = parts.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (!node.Children.TryGetValue(list[i], out var child)) node.Children.Add(list[i], child = new CompletionNode(kind));
                node = child;
                if (i == list.Count - 1)
                {
                    node.Leaf = true;
                    node.Detail = detail;
                }
            }
        }

        var scopeNames = v.ScopeNames;
        foreach (var (scope, properties) in v.Scopes)
        {
            var kind = scope switch
            {
                PropertyExpression.BuffScope => AplTokenKind.Buff,
                PropertyExpression.DebuffScope or PropertyExpression.DotScope => AplTokenKind.Debuff,
                PropertyExpression.CooldownScope or PropertyExpression.ActionScope => AplTokenKind.Cooldown,
                _ => AplTokenKind.Named,
            };
            foreach (var name in scopeNames[scope])
            {
                foreach (string property in properties) Add([scope, name.Slug, .. property.Split('.')], kind);
                if (root.Children.TryGetValue(scope, out var scopeNode) && scopeNode.Children.TryGetValue(name.Slug, out var nameNode))
                {
                    nameNode.Detail = name.Name == name.Slug ? "" : name.Name;
                }
            }
        }
        string[] prefix = v.ResourcePrefix.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string deficit = v.DeficitSuffix.TrimStart('.');
        foreach (var resource in v.Resources)
        {
            Add([resource.Slug], AplTokenKind.Resource, resource.Name);
            Add([resource.Slug, deficit], AplTokenKind.Resource);
            Add([.. prefix, resource.Slug], AplTokenKind.Resource, resource.Name);
            Add([.. prefix, resource.Slug, deficit], AplTokenKind.Resource);
        }
        foreach (string named in v.Named) Add(named.Split('.'), AplTokenKind.Named);
        foreach (string value in v.ActionValues) Add([value], AplTokenKind.Named);
        foreach (var (scope, names) in v.BuildNames)
        {
            foreach (var name in names) Add([scope, name.Slug], AplTokenKind.Named, Detail(name));
        }
        foreach (var aura in v.Auras) Add([PropertyExpression.ActiveDotScope, aura.Slug], AplTokenKind.Debuff, Detail(aura));
        foreach (var ability in v.Abilities)
        {
            Add([PropertyExpression.PrevScope, ability.Slug], AplTokenKind.Cooldown, ability.Name);
            Add([PropertyExpression.PrevGcdScope, ability.Slug], AplTokenKind.Cooldown, ability.Name);
        }
        foreach (var function in Enum.GetValues<AplFunction>()) Add([function.Name()], AplTokenKind.Function, function.Doc().Label);
        foreach (string word in Operators) Add([word], AplTokenKind.Operator);
        return root;
    }

    /// <summary>
    /// The parts after <paramref name="word"/>'s last '.', starting with its text after that '.': those starting with it
    /// first, then those containing it.
    /// </summary>
    public static IReadOnlyList<Completion> For(AplVocabulary v, string word, IReadOnlyList<string>? variables = null)
    {
        string[] parts = word.ToLowerInvariant().Split('.');
        string partial = parts[^1];
        var node = WithVariables(TreeOf(v), variables ?? []);
        foreach (string part in parts[..^1])
        {
            if (!node.Children.TryGetValue(part, out var child)) return [];
            node = child;
        }
        var entries = node.Children.Select(c => new Completion(c.Key, c.Value)).ToList();
        var found = entries.Where(e => e.Text.StartsWith(partial, StringComparison.Ordinal))
            .Concat(partial.Length > 0
                ? entries.Where(e => !e.Text.StartsWith(partial, StringComparison.Ordinal) && e.Text.Contains(partial, StringComparison.Ordinal))
                : [])
            .ToList();
        // A finished name has nothing to complete.
        if (found.Count == 1 && found[0].Text == partial && found[0].Node.Children.Count == 0) return [];
        return found;
    }

    private static string Detail(AplName name) => name.Name == name.Slug ? "" : name.Name;

    // The vocabulary's tree with "variable.<name>" for each of the list's variables, before the previous actions.
    private static CompletionNode WithVariables(CompletionNode tree, IReadOnlyList<string> variables)
    {
        if (variables.Count == 0) return tree;
        var variable = new CompletionNode(AplTokenKind.Named);
        foreach (string name in variables) variable.Children.Add(name, new CompletionNode(AplTokenKind.Named) { Leaf = true });
        var root = new CompletionNode(null);
        foreach (var (text, child) in tree.Children)
        {
            if (text == PropertyExpression.PrevScope) root.Children.Add(PropertyExpression.VariableScope, variable);
            root.Children.Add(text, child);
        }
        return root;
    }
}
