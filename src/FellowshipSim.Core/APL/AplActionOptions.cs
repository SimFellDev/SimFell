using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

/// <summary>The parts of an action line, each as written and with its <see cref="AplDoc"/>, for the condition reference.</summary>
public static class AplActionOptions
{
    public static IReadOnlyList<(string Syntax, AplDoc Doc)> All { get; } =
    [
        ($"{AplActionLine.ListPrefix}+=/<ability>", new("Action", $"Casts the ability. Each decision starts at the top of the {AplActionLine.DefaultList} list.")),
        ($"{AplActionLine.ListPrefix}.<list>+=/<ability>", new("Action in a list", "An action of the named list.")),
        ($"{AplActionLine.ConditionKey}=<expression>", new("Condition", "The action runs only while the expression is true (not 0).")),
        .. Enum.GetValues<AplTargetSelection>().Select(s =>
            ($"{AplActionLine.TargetIfKey}={AplTargetIf.Prefix(s)}:<expression>", new AplDoc(AplTargetIf.Label(s), AplTargetIf.Description(s)))),
        .. Enum.GetValues<AplListAction>().Select(a =>
            ($"{a.Name()},{AplActionLine.ListNameKey}=<list>", new AplDoc(a == AplListAction.Run ? "Run action list" : "Call action list", a.Description()))),
        ($"{AplVariable.ActionName},{AplVariable.NameKey}=<name>", new("Variable", $"Changes the variable {PropertyExpression.VariableScope}.<name> and casts nothing.")),
        ($"{AplVariable.ValueKey}=<expression>", new("Variable value", "The value the variable action uses.")),
        .. Enum.GetValues<AplVariableOp>().Select(op =>
            ($"{AplVariable.OpKey}={op.ToString().ToLowerInvariant()}", new AplDoc($"Variable op: {op.ToString().ToLowerInvariant()}", AplVariable.Description(op)))),
        ($"{AplVariable.DefaultKey}=<number>", new("Variable default", "The variable's value at the start of each fight. 0 when the action has no default.")),
    ];
}
