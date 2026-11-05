using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

/// <summary>The parts of an action line, each as written and with its <see cref="AplDoc"/>, for the condition reference.</summary>
public static class AplActionOptions
{
    public static IReadOnlyList<(string Syntax, AplDoc Doc)> All { get; } =
    [
        ($"{AplActionLine.ListPrefix}+=/<ability>", new("Action", $"Casts the ability. Each decision starts at the top of the {AplActionLine.DefaultList} list.")),
        ($"{AplActionLine.ListPrefix}.<list>+=/<ability>", new("Action in a list", "An action in the named list.")),
        ($"{AplActionLine.ConditionKey}=<expression>", new("Condition", "The action runs only if the expression is true (not 0).")),
        .. Enum.GetValues<AplTargetSelection>().Select(s =>
            ($"{AplActionLine.TargetIfKey}={AplTargetIf.Prefix(s)}:<expression>", new AplDoc(AplTargetIf.Label(s), AplTargetIf.Description(s)))),
        ($"{AplActionLine.UseOffGcdKey}=1", new("Use off the global cooldown",
            "The action is also a candidate while the global cooldown runs. Without it, the action waits for the global cooldown to end.")),
        ($"{AplActionLine.UseWhileCastingKey}=1", new("Use while casting",
            "The action is also a candidate while a cast or a channel runs, for an ability off the global cooldown. Without it, the action waits for the cast or channel to end.")),
        .. Enum.GetValues<AplListAction>().Select(a =>
            ($"{a.Name()},{AplActionLine.ListNameKey}=<list>", new AplDoc(a == AplListAction.Run ? "Run action list" : "Call action list", a.Description()))),
        ($"{AplVariable.ActionName},{AplVariable.NameKey}=<name>", new("Set a variable", $"Changes the variable {PropertyExpression.VariableScope}.<name> and casts nothing. In the editor, the action type Set a variable has the name, op, value and default as fields.")),
        ($"{AplVariable.ValueKey}=<expression>", new("Variable value", "The value the variable action sets, adds or subtracts.")),
        .. Enum.GetValues<AplVariableOp>().Select(op =>
            ($"{AplVariable.OpKey}={op.ToString().ToLowerInvariant()}", new AplDoc($"Variable op: {op.ToString().ToLowerInvariant()}", AplVariable.Description(op)))),
        ($"{AplVariable.DefaultKey}=<number>", new("Variable default", "The value of the variable at the start of each fight. 0 if the action has no default.")),
    ];
}
