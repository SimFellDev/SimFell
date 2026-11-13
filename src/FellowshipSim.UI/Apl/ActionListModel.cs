using System.Text.RegularExpressions;
using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// Names, titles, warnings and checks of the editor's action lists. The engine parses and writes the action-list text, so
/// the editor and the engine read the same grammar.
/// </summary>
public static partial class ActionListModel
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*\z")]
    private static partial Regex ListNamePattern();

    [GeneratedRegex(@"^(and|or|not)\z", RegexOptions.IgnoreCase)]
    private static partial Regex OperatorPattern();

    /// <summary>The distinct problems of the tokens of <paramref name="check"/>, in token order.</summary>
    public static IReadOnlyList<string> ProblemsOf(AplExpressionCheck? check) =>
        (check?.Tokens ?? []).Select(t => t.Problem).OfType<string>().Where(p => p.Length > 0).Distinct().ToList();

    public static string AbilityName(AplVocabulary v, string slug) => v.Abilities.FirstOrDefault(a => a.Slug == slug)?.Name ?? slug;

    public static AplTargetSelectionName? SelectionName(AplVocabulary v, AplTargetSelection selection) =>
        v.TargetSelections.FirstOrDefault(s => s.Selection == selection);

    /// <summary>run_action_list and call_action_list run another list.</summary>
    public static bool IsListAction(AplVocabulary v, EditorAction action) => v.ListActions.Contains(action.Ability);

    /// <summary>variable changes a variable and casts nothing.</summary>
    public static bool IsVariableAction(EditorAction action) => action.Ability == AplVariable.ActionName;

    /// <summary>switch_target sets the hero's current target and casts nothing.</summary>
    public static bool IsSwitchTargetAction(EditorAction action) => action.Ability == AplSwitchTarget.ActionName;

    /// <summary>The op's words in an action's title: "Set", "Add to", "Subtract from" or "Reset".</summary>
    public static string VariableOpName(AplVariableOp op) => op switch
    {
        AplVariableOp.Set => "Set",
        AplVariableOp.Add => "Add to",
        AplVariableOp.Sub => "Subtract from",
        AplVariableOp.Reset => "Reset",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

    /// <summary>"run_action_list" -> "Run action list".</summary>
    public static string ListActionName(string name)
    {
        string words = name.Replace('_', ' ');
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    public static string ActionTitle(AplVocabulary v, EditorAction action)
    {
        if (IsListAction(v, action)) return $"{ListActionName(action.Ability)}: {(string.IsNullOrEmpty(action.ActionList) ? "pick a list" : action.ActionList)}";
        if (IsVariableAction(action))
        {
            var variable = action.Variable ?? new AplDocumentVariable("");
            return $"{VariableOpName(variable.Op)} variable: {(string.IsNullOrWhiteSpace(variable.Name) ? "name it" : variable.Name.Trim())}";
        }
        if (IsSwitchTargetAction(action)) return AplSwitchTarget.Label;
        string name = AbilityName(v, action.Ability);
        return name.Length > 0 ? name : "Pick an ability";
    }

    public static bool IsDefaultList(AplVocabulary v, ActionList list) => list.Name == v.DefaultList;

    public static string ListTitle(AplVocabulary v, ActionList list) => IsDefaultList(v, list) ? "Default list" : $"Action list {list.Name}";

    public static bool IsRunAction(AplVocabulary v, EditorAction action) => action.Ability == v.ListActions[0];

    /// <summary>
    /// True when the list casts on every decision: it has a repeatable ability with no condition or target selection, or a
    /// run_action_list with no condition naming a list that casts on every decision.
    /// </summary>
    private static bool AlwaysCasts(AplVocabulary v, IReadOnlyList<ActionList> lists, string? name, HashSet<string> seen)
    {
        var list = lists.FirstOrDefault(l => l.Name == name);
        if (list == null || name == null || !seen.Add(name)) return false;
        return list.Actions.Any(action =>
        {
            if (!string.IsNullOrWhiteSpace(action.Condition)) return false;
            if (IsRunAction(v, action)) return AlwaysCasts(v, lists, action.ActionList, seen);
            return v.RepeatableAbilities.Contains(action.Ability) && action.TargetSelection == null;
        });
    }

    /// <summary>The warning for a list a run_action_list names, when the list can cast nothing.</summary>
    public static string? RunWarning(AplVocabulary v, IReadOnlyList<ActionList> lists, string? name)
    {
        if (string.IsNullOrEmpty(name) || AlwaysCasts(v, lists, name, [])) return null;
        return $"When nothing in {name} is ready, the hero casts nothing and the actions after this one do not run. " +
               $"End {name} with an action that is always ready{Example(v)}.";
    }

    /// <summary>The warning for a list named by a run_action_list that can cast nothing.</summary>
    public static string? ListWarning(AplVocabulary v, IReadOnlyList<ActionList> lists, ActionList list)
    {
        bool runBy = lists.Any(l => l.Actions.Any(a => IsRunAction(v, a) && a.ActionList == list.Name));
        if (!runBy || AlwaysCasts(v, lists, list.Name, [])) return null;
        return "A Run action list action names this list. When nothing here is ready, the hero casts nothing. " +
               $"End it with an action that is always ready{Example(v)}.";
    }

    private static string Example(AplVocabulary v) =>
        v.RepeatableAbilities.Count > 0 ? $", such as {AbilityName(v, v.RepeatableAbilities[0])} with no condition" : "";

    /// <summary>A list name is one identifier: letters, digits and '_', and none of the words 'and', 'or' and 'not'.</summary>
    public static string? ListNameProblem(IReadOnlyList<ActionList> lists, string name, string? current = null)
    {
        if (!ListNamePattern().IsMatch(name)) return "Name the list with letters, digits and '_', starting with a letter.";
        if (OperatorPattern().IsMatch(name)) return $"`{name}` is an operator.";
        if (name != current && lists.Any(l => l.Name == name)) return $"There is already a list named {name}.";
        return null;
    }

    /// <summary>A variable name is one identifier: letters, digits and '_', and none of the words 'and', 'or' and 'not'.</summary>
    public static string? VariableNameProblem(string name)
    {
        if (!ListNamePattern().IsMatch(name)) return "Name the variable with letters, digits and '_', starting with a letter.";
        if (OperatorPattern().IsMatch(name)) return $"`{name}` is an operator.";
        return null;
    }

    /// <summary>Every action in list order, with its list's name.</summary>
    public static IReadOnlyList<AplDocumentAction> DocumentActions(IReadOnlyList<ActionList> lists) =>
        lists.SelectMany(list => list.Actions.Select(a => new AplDocumentAction(
            a.Ability, a.Condition, a.TargetSelection, a.TargetExpression, a.Comment, list.Name, a.Arguments, a.ActionList, a.Variable,
            a.UseOffGcd, a.UseWhileCasting, a.InterruptIf, a.InterruptImmediate))).ToList();

    public static string ErrorText(AplVocabulary v, IReadOnlyList<ActionList> lists, IReadOnlyList<AplDocumentError> errors)
    {
        var labels = lists.SelectMany(list => list.Actions.Select((_, i) => $"{ListTitle(v, list)} action {i + 1}")).ToList();
        return string.Join(" ", errors.Select(e => $"{(e.Action >= 0 && e.Action < labels.Count ? labels[e.Action] : "undefined")}: {e.Message}"));
    }

    /// <summary>The default list first, then the other lists in order of their first action.</summary>
    public static IReadOnlyList<ActionList> ListsOf(AplVocabulary v, IReadOnlyList<AplDocumentAction> actions)
    {
        var names = new List<string> { v.DefaultList };
        var byName = new Dictionary<string, List<EditorAction>> { [v.DefaultList] = [] };
        foreach (var action in actions)
        {
            if (!byName.TryGetValue(action.List, out var list))
            {
                byName[action.List] = list = [];
                names.Add(action.List);
            }
            list.Add(EditorAction.From(action));
        }
        return names.Select(n => new ActionList(n, byName[n])).ToList();
    }
}
