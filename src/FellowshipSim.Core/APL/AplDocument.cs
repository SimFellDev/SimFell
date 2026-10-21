using Superpower;

namespace FellowshipSim.Core.APL;

/// <summary>
/// One action of an action list as the visual editor holds it: expressions are text, and <see cref="Comment"/> is the
/// '#' lines above the action. <see cref="List"/> is the list the action belongs to. <see cref="ActionList"/> is the list
/// a <c>run_action_list</c> or <c>call_action_list</c> action names. <see cref="Arguments"/> holds the other arguments
/// ("line_cd=5").
/// </summary>
public sealed record AplDocumentAction(
    string Ability,
    string? Condition = null,
    AplTargetSelection? TargetSelection = null,
    string? TargetExpression = null,
    string? Comment = null,
    string List = AplActionLine.DefaultList,
    string? Arguments = null,
    string? ActionList = null);

/// <summary>A problem with one field of one action: its index in the list, the field ("condition"), and the parse error.</summary>
public sealed record AplDocumentError(int Action, string Field, string Message);

/// <summary>Converts between action-list text and the actions the visual editor holds.</summary>
public static class AplDocument
{
    /// <summary>
    /// The actions of <paramref name="script"/>, in order, with expressions in <see cref="AplFormatter"/> form with
    /// <see cref="AplOperatorStyle.Words"/> (the editor writes <c>and</c>, <c>or</c> and <c>not</c>). '#' lines
    /// belong to the action below them; '#' lines after the last action belong to the last action.
    /// </summary>
    public static List<AplDocumentAction> Parse(string script)
    {
        var actions = new List<AplDocumentAction>();
        var comment = new List<string>();
        var lines = script.Split('\n').Select(l => l.Trim()).ToArray();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            if (line.StartsWith('#'))
            {
                comment.Add(line[1..].TrimStart());
                continue;
            }

            AplActionLine syntax;
            try
            {
                syntax = AplParser.ParseActionSyntax(line);
            }
            catch (ParseException e)
            {
                throw new ParseException($"APL line {i + 1}: {e.Message}", e.ErrorPosition, e);
            }
            bool listAction = AplListActions.Parse(syntax.Name) != null;
            var arguments = syntax.Arguments
                .Where(a => !(listAction && a.Key.Equals(AplActionLine.ListNameKey, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            actions.Add(new AplDocumentAction(
                syntax.Name,
                syntax.Condition is { } condition ? AplFormatter.Format(condition, AplOperatorStyle.Words) : null,
                syntax.TargetIf?.Selection,
                syntax.TargetIf is { } targetIf ? AplFormatter.Format(targetIf.Expression, AplOperatorStyle.Words) : null,
                comment.Count > 0 ? string.Join("\n", comment) : null,
                syntax.List,
                arguments.Count > 0 ? string.Join(",", arguments) : null,
                listAction ? syntax.ListName : null));
            comment.Clear();
        }

        if (comment.Count > 0 && actions.Count > 0)
        {
            var last = actions[^1];
            actions[^1] = last with { Comment = string.Join("\n", (last.Comment ?? "").Split('\n').Concat(comment).Where(c => c.Length > 0)) };
        }
        return actions;
    }

    /// <summary>
    /// The action-list text of <paramref name="actions"/>, with expressions in <see cref="AplFormatter"/> form, or the
    /// problems that stop it being written. A blank condition or target expression is left out. The lines of each list
    /// are together: <see cref="AplActionLine.DefaultList"/> first, then the other lists in order of their first action.
    /// </summary>
    public static (string? Text, List<AplDocumentError> Errors) Write(IReadOnlyList<AplDocumentAction> actions)
    {
        var errors = new List<AplDocumentError>();
        var lists = actions.Select(ListOf).Prepend(AplActionLine.DefaultList).Distinct().ToList();
        var blocks = lists.ToDictionary(l => l, _ => new List<string>());
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            string ability = action.Ability.Trim();
            if (!IsName(ability)) errors.Add(new AplDocumentError(i, "ability", "Pick an ability."));
            var condition = ParseField(i, "condition", action.Condition, errors);
            var targetExpression = ParseField(i, "targetExpression", action.TargetExpression, errors);
            if (action.TargetSelection != null && targetExpression == null && string.IsNullOrWhiteSpace(action.TargetExpression))
            {
                errors.Add(new AplDocumentError(i, "targetExpression", "Enter an expression for target selection."));
            }
            string list = ListOf(action);
            if (!IsName(list)) errors.Add(new AplDocumentError(i, "list", $"`{list}` is not a list name."));

            var arguments = ParseArguments(i, action.Arguments, errors).ToList();
            if (AplListActions.Parse(ability) != null)
            {
                string actionList = action.ActionList?.Trim() ?? "";
                if (IsName(actionList) && lists.Contains(actionList))
                {
                    arguments.Insert(0, new AplArgument(AplActionLine.ListNameKey, new Expressions.PropertyExpression(actionList)));
                }
                else
                {
                    errors.Add(new AplDocumentError(i, "actionList", "Pick an action list with at least one action."));
                }
            }

            var lines = blocks[list];
            foreach (string comment in (action.Comment ?? "").Split('\n').Select(c => c.TrimEnd()).Where(c => c.Length > 0))
            {
                lines.Add($"# {comment}");
            }
            var targetIf = targetExpression != null ? new AplTargetIf(action.TargetSelection ?? AplTargetSelection.First, targetExpression) : null;
            lines.Add(new AplActionLine(list, ability, condition, targetIf, arguments).ToString());
        }
        if (errors.Count > 0) return (null, errors);

        var blockTexts = lists.Where(l => blocks[l].Count > 0).Select(l => string.Join("\n", blocks[l]) + "\n");
        return (string.Join("\n", blockTexts), errors);
    }

    private static string ListOf(AplDocumentAction action) =>
        string.IsNullOrWhiteSpace(action.List) ? AplActionLine.DefaultList : action.List.Trim();

    private static IReadOnlyList<AplArgument> ParseArguments(int action, string? text, List<AplDocumentError> errors)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            var syntax = AplParser.ParseActionSyntax($"arguments,{text.Trim().TrimStart(',')}");
            if (syntax.Condition == null && syntax.TargetIf == null) return syntax.Arguments;
            errors.Add(new AplDocumentError(action, "arguments", $"Write `{AplActionLine.ConditionKey}` in Condition and `{AplActionLine.TargetIfKey}` in Target."));
        }
        catch (ParseException e)
        {
            errors.Add(new AplDocumentError(action, "arguments", e.Message));
        }
        return [];
    }

    private static Expressions.IAplExpression? ParseField(int action, string field, string? text, List<AplDocumentError> errors)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return AplParser.ParseExpression(text);
        }
        catch (ParseException e)
        {
            errors.Add(new AplDocumentError(action, field, e.Message));
            return null;
        }
    }

    /// <summary>True when <paramref name="text"/> is one identifier token (an ability or list name).</summary>
    public static bool IsName(string text)
    {
        var tokens = AplParser.Tokenize(text);
        return tokens.HasValue && tokens.Value.Count() == 1 && tokens.Value.First().Kind == AplToken.Identifier;
    }
}
