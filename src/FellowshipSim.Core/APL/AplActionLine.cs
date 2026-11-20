using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

/// <summary>
/// An action argument other than <c>if</c> and <c>target_if</c> (<c>name=aoe</c>, <c>use_off_gcd=1</c>, <c>line_cd=5</c>). The
/// engine reads <see cref="AplActionLine.ListNameKey"/>, <see cref="AplActionLine.UseOffGcdKey"/>,
/// <see cref="AplActionLine.UseWhileCastingKey"/>, <see cref="AplActionLine.InterruptIfKey"/>,
/// <see cref="AplActionLine.InterruptImmediateKey"/> and the <c>variable</c> arguments, and ignores the others.
/// </summary>
public sealed record AplArgument(string Key, IAplExpression Value)
{
    public override string ToString() => $"{Key}={AplFormatter.Format(Value)}";
}

/// <summary>
/// One action line as written: <c>actions.&lt;list&gt;+=/&lt;name&gt;,&lt;key&gt;=&lt;expr&gt;,target_if=&lt;selection&gt;:&lt;expr&gt;,if=&lt;expr&gt;</c>.
/// <see cref="List"/> is the list's name (<c>aoe</c> for <c>actions.aoe</c>, <see cref="DefaultList"/> for <c>actions</c>).
/// <see cref="Name"/> is an ability or an action that casts nothing (<c>run_action_list</c>, <c>variable</c>,
/// <c>switch_target</c>).
/// </summary>
public sealed record AplActionLine(string List, string Name, IAplExpression? Condition, AplTargetIf? TargetIf, IReadOnlyList<AplArgument> Arguments)
{
    /// <summary>Name of the list each decision starts at, written <c>actions</c>.</summary>
    public const string DefaultList = "default";
    public const string ListPrefix = "actions";
    public const string ConditionKey = "if";
    public const string TargetIfKey = "target_if";
    /// <summary>Argument of <c>run_action_list</c> and <c>call_action_list</c> holding the list's name.</summary>
    public const string ListNameKey = "name";

    /// <summary>
    /// Argument of an ability action, 1 when the action is a candidate in a decision while the global cooldown or the
    /// reaction after it runs. Without it, the action waits for both to end.
    /// </summary>
    public const string UseOffGcdKey = "use_off_gcd";

    /// <summary>
    /// Argument of an ability action, 1 when the action is a candidate in a decision while a cast or a channel runs.
    /// Without it, the action waits for both to end.
    /// </summary>
    public const string UseWhileCastingKey = "use_while_casting";

    /// <summary>
    /// Argument of a channeled ability's action: the condition that stops the channel the action started when an action
    /// above it is ready, checked after each tick once the global cooldown has ended.
    /// </summary>
    public const string InterruptIfKey = "interrupt_if";

    /// <summary>
    /// Argument of an action with <see cref="InterruptIfKey"/>, 1 when the channel stops the moment the condition is true,
    /// with no wait for a tick or the global cooldown.
    /// </summary>
    public const string InterruptImmediateKey = "interrupt_immediate";

    /// <summary>The argument <paramref name="key"/>'s expression; null when the line has none.</summary>
    public IAplExpression? Argument(string key) =>
        Arguments.LastOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

    /// <summary>The list's name for <paramref name="prefix"/> (<c>actions.aoe</c> is <c>aoe</c>), or null for a prefix other than <c>actions</c>.</summary>
    public static string? ListOf(string prefix) =>
        prefix == ListPrefix ? DefaultList
            : prefix.StartsWith($"{ListPrefix}.") && prefix.Length > ListPrefix.Length + 1 ? prefix[(ListPrefix.Length + 1)..]
            : null;

    /// <summary>The prefix of <paramref name="list"/>'s lines (<c>aoe</c> is <c>actions.aoe</c>).</summary>
    public static string PrefixOf(string list) => list == DefaultList ? ListPrefix : $"{ListPrefix}.{list}";

    /// <summary>The list a <c>run_action_list</c> or <c>call_action_list</c> line names in its <c>name=</c> argument.</summary>
    public string? ListName =>
        Arguments.LastOrDefault(a => a.Key.Equals(ListNameKey, StringComparison.OrdinalIgnoreCase))?.Value is PropertyExpression list ? list.PropertyPath : null;

    /// <summary>True when the argument <paramref name="key"/> is a number other than 0.</summary>
    public bool Flag(string key) =>
        Arguments.LastOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value is ConstantExpression { Value: not 0 };

    /// <summary>The line in canonical form, with expressions in <see cref="AplFormatter"/> form.</summary>
    public override string ToString()
    {
        string line = $"{PrefixOf(List)}+=/{Name}";
        foreach (var argument in Arguments)
        {
            line += $",{argument}";
        }
        if (TargetIf != null)
        {
            line += $",{TargetIfKey}={AplTargetIf.Prefix(TargetIf.Selection)}:{AplFormatter.Format(TargetIf.Expression)}";
        }
        if (Condition != null)
        {
            line += $",{ConditionKey}={AplFormatter.Format(Condition)}";
        }
        return line;
    }
}
