using System.Collections.Concurrent;
using Superpower;

namespace FellowshipSim.Core.APL;

/// <summary>
/// One action of an action list as the visual editor holds it: expressions are text, and <see cref="Comment"/> is the
/// '#' lines above the action. <see cref="List"/> is the list the action belongs to. <see cref="ActionList"/> is the list
/// a <c>run_action_list</c> or <c>call_action_list</c> action names. <see cref="Variable"/> is the variable a
/// <c>variable</c> action changes. <see cref="TargetSelection"/> and <see cref="TargetExpression"/> are the
/// <c>target_if</c> of an ability action's cast or of a <c>switch_target</c> action (<see cref="AplSwitchTarget"/>).
/// <see cref="UseOffGcd"/> and <see cref="UseWhileCasting"/> are an ability actions
/// <c>use_off_gcd=1</c> and <c>use_while_casting=1</c>. <see cref="InterruptIf"/> is an ability action's <c>interrupt_if</c>
/// condition as text, and <see cref="InterruptImmediate"/> its <c>interrupt_immediate=1</c>. <see cref="Arguments"/> holds
/// the other arguments ("line_cd=5").
/// </summary>
public sealed record AplDocumentAction(
    string Ability,
    string? Condition = null,
    AplTargetSelection? TargetSelection = null,
    string? TargetExpression = null,
    string? Comment = null,
    string List = AplActionLine.DefaultList,
    string? Arguments = null,
    string? ActionList = null,
    AplDocumentVariable? Variable = null,
    bool UseOffGcd = false,
    bool UseWhileCasting = false,
    string? InterruptIf = null,
    bool InterruptImmediate = false);

/// <summary>
/// The variable a <c>variable</c> action changes, as the visual editor holds it: its name, its op, its value as text
/// (none for <see cref="AplVariableOp.Reset"/>), and its value at the start of each fight.
/// </summary>
public sealed record AplDocumentVariable(string Name, AplVariableOp Op = AplVariableOp.Set, string? Value = null, double Default = 0);

/// <summary>A problem with one field of one action: its index in the list, the field ("condition"), and the parse error.</summary>
public sealed record AplDocumentError(int Action, string Field, string Message);

/// <summary>Text of an action list, or of one expression.</summary>
public sealed record AplText(string Text);

/// <summary>The actions of an action list, in order.</summary>
public sealed record AplDocumentActions(IReadOnlyList<AplDocumentAction> Actions);

/// <summary>The text of an action list's actions (null when a field does not parse), and the problem with each field.</summary>
public sealed record AplDocumentText(string? Text, IReadOnlyList<AplDocumentError> Errors);

/// <summary>Converts between action-list text and the actions the visual editor holds.</summary>
public static class AplDocument
{
    /// <summary>The most action lines kept parsed; past it the lines are parsed again as they come.</summary>
    private const int MaxParsedLines = 20_000;

    // Each action line's action, without its comment: an edit changes one line, so a long list reads the others from here.
    private static readonly ConcurrentDictionary<string, AplDocumentAction> ParsedLines = new(StringComparer.Ordinal);

    private static AplDocumentAction ActionOf(string line)
    {
        var syntax = AplParser.ParseActionSyntax(line);
        bool listAction = AplListActions.Parse(syntax.Name) != null;
        bool variableAction = syntax.Name == AplVariable.ActionName;
        // The switches are an ability action's: any other action keeps them among its other arguments.
        bool abilityAction = !listAction && !variableAction && syntax.Name != AplSwitchTarget.ActionName;
        var arguments = syntax.Arguments
            .Where(a => !(listAction && a.Key.Equals(AplActionLine.ListNameKey, StringComparison.OrdinalIgnoreCase)))
            .Where(a => !(variableAction && VariableKeys.Contains(a.Key)))
            .Where(a => !abilityAction || !AbilityKeys.Contains(a.Key))
            .ToList();
        return new AplDocumentAction(
            syntax.Name,
            syntax.Condition is { } condition ? AplFormatter.Format(condition, AplOperatorStyle.Words) : null,
            syntax.TargetIf?.Selection,
            syntax.TargetIf is { } targetIf ? AplFormatter.Format(targetIf.Expression, AplOperatorStyle.Words) : null,
            null,
            syntax.List,
            arguments.Count > 0 ? string.Join(",", arguments) : null,
            listAction ? syntax.ListName : null,
            variableAction ? VariableOf(syntax) : null,
            abilityAction && syntax.Flag(AplActionLine.UseOffGcdKey),
            abilityAction && syntax.Flag(AplActionLine.UseWhileCastingKey),
            abilityAction && syntax.Argument(AplActionLine.InterruptIfKey) is { } interruptIf
                ? AplFormatter.Format(interruptIf, AplOperatorStyle.Words) : null,
            abilityAction && syntax.Flag(AplActionLine.InterruptImmediateKey));
    }

    // The arguments of an ability action the editor holds as switches and fields of their own.
    private static readonly HashSet<string> AbilityKeys = new(
        [AplActionLine.UseOffGcdKey, AplActionLine.UseWhileCastingKey, AplActionLine.InterruptIfKey, AplActionLine.InterruptImmediateKey],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> VariableKeys = new([AplVariable.NameKey, AplVariable.ValueKey, AplVariable.OpKey, AplVariable.DefaultKey],
        StringComparer.OrdinalIgnoreCase);

    // The variable of a variable action line. A line without a name, or with an op outside the enum, is still read: Write
    // reports the problem with its field.
    private static AplDocumentVariable VariableOf(AplActionLine syntax)
    {
        Expressions.IAplExpression? Argument(string key) =>
            syntax.Arguments.FirstOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
        string name = Argument(AplVariable.NameKey) is Expressions.PropertyExpression path ? path.PropertyPath : "";
        var op = Argument(AplVariable.OpKey) is Expressions.PropertyExpression opPath
                 && Enum.TryParse<AplVariableOp>(opPath.PropertyPath, true, out var parsed) ? parsed : AplVariableOp.Set;
        var value = Argument(AplVariable.ValueKey);
        double @default = Argument(AplVariable.DefaultKey) is Expressions.ConstantExpression c ? c.Value : 0;
        return new AplDocumentVariable(name, op, value != null ? AplFormatter.Format(value, AplOperatorStyle.Words) : null, @default);
    }

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
                // Write puts one space after the '#'; any more is the comment's own indentation.
                comment.Add(line.StartsWith("# ", StringComparison.Ordinal) ? line[2..] : line[1..]);
                continue;
            }

            AplDocumentAction action;
            try
            {
                if (ParsedLines.Count > MaxParsedLines) ParsedLines.Clear();
                action = ParsedLines.GetOrAdd(line, ActionOf);
            }
            catch (ParseException e)
            {
                throw new ParseException($"APL line {i + 1}: {e.Message}", e.ErrorPosition, e);
            }
            actions.Add(comment.Count > 0 ? action with { Comment = string.Join("\n", comment) } : action);
            comment.Clear();
        }

        if (comment.Count > 0 && actions.Count > 0)
        {
            var last = actions[^1];
            actions[^1] = last with { Comment = string.Join("\n", (last.Comment is { } above ? [above] : Array.Empty<string>()).Concat(comment)) };
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
            // A switch_target has a target selection, written or not: its target is the pick.
            if ((action.TargetSelection != null || ability == AplSwitchTarget.ActionName) && targetExpression == null
                && string.IsNullOrWhiteSpace(action.TargetExpression))
            {
                errors.Add(new AplDocumentError(i, "targetExpression", "Enter an expression for target selection."));
            }
            string list = ListOf(action);
            if (!IsName(list)) errors.Add(new AplDocumentError(i, "list", $"`{list}` is not a list name."));

            var arguments = ParseArguments(i, action.Arguments, errors).ToList();
            if (ability == AplVariable.ActionName)
            {
                arguments.InsertRange(0, VariableArguments(i, action.Variable, errors));
            }
            else if (AplListActions.Parse(ability) != null)
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
            else if (ability != AplSwitchTarget.ActionName)
            {
                if (action.UseOffGcd) arguments.Add(new AplArgument(AplActionLine.UseOffGcdKey, new Expressions.ConstantExpression(1)));
                if (action.UseWhileCasting) arguments.Add(new AplArgument(AplActionLine.UseWhileCastingKey, new Expressions.ConstantExpression(1)));
                if (ParseField(i, "interruptIf", action.InterruptIf, errors) is { } interruptIf)
                {
                    arguments.Add(new AplArgument(AplActionLine.InterruptIfKey, interruptIf));
                    if (action.InterruptImmediate) arguments.Add(new AplArgument(AplActionLine.InterruptImmediateKey, new Expressions.ConstantExpression(1)));
                }
            }

            var lines = blocks[list];
            // A blank line inside a comment is a bare '#'; blank lines before and after it are left out.
            var commentLines = (action.Comment ?? "").Split('\n').Select(c => c.TrimEnd()).SkipWhile(c => c.Length == 0).Reverse()
                .SkipWhile(c => c.Length == 0).Reverse();
            foreach (string comment in commentLines)
            {
                lines.Add(comment.Length > 0 ? $"# {comment}" : "#");
            }
            var targetIf = targetExpression != null ? new AplTargetIf(action.TargetSelection ?? AplTargetSelection.First, targetExpression) : null;
            lines.Add(new AplActionLine(list, ability, condition, targetIf, arguments).ToString());
        }
        if (errors.Count > 0) return (null, errors);

        var blockTexts = lists.Where(l => blocks[l].Count > 0).Select(l => string.Join("\n", blocks[l]) + "\n");
        return (string.Join("\n", blockTexts), errors);
    }

    // The name, value, op and default arguments of a variable action. The op is left out when it is set, and the default
    // when it is 0: each is what a line without it means.
    private static IEnumerable<AplArgument> VariableArguments(int action, AplDocumentVariable? variable, List<AplDocumentError> errors)
    {
        if (variable == null || !IsName(variable.Name))
        {
            errors.Add(new AplDocumentError(action, "variable", "Name the variable with letters, digits and '_', starting with a letter."));
        }
        else
        {
            yield return new AplArgument(AplVariable.NameKey, new Expressions.PropertyExpression(variable.Name));
        }
        if (variable == null) yield break;
        if (variable.Op != AplVariableOp.Reset)
        {
            if (string.IsNullOrWhiteSpace(variable.Value)) errors.Add(new AplDocumentError(action, "value", "Enter the variable's value."));
            else if (ParseField(action, "value", variable.Value, errors) is { } value) yield return new AplArgument(AplVariable.ValueKey, value);
        }
        if (variable.Op != AplVariableOp.Set)
        {
            yield return new AplArgument(AplVariable.OpKey, new Expressions.PropertyExpression(variable.Op.ToString().ToLowerInvariant()));
        }
        if (variable.Default != 0) yield return new AplArgument(AplVariable.DefaultKey, new Expressions.ConstantExpression(variable.Default));
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
