using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// An expression of a traced action line, bound on its own with the program's variables, and each of its operands. It
/// reads the values a decision read (<see cref="Trace"/>).
/// </summary>
internal sealed class AplProbe
{
    private readonly string _text;
    private readonly BoundExpression _bound;
    private readonly AplProbe[] _operands;
    private readonly BinaryOperator? _logical;
    private readonly bool _truth;

    private AplProbe(string text, BoundExpression bound, AplProbe[] operands, BinaryOperator? logical, bool truth)
    {
        _text = text;
        _bound = bound;
        _operands = operands;
        _logical = logical;
        _truth = truth;
    }

    public static AplProbe Of(IAplExpression expression, Hero hero, BoundVariables variables)
    {
        var inner = expression;
        while (inner is GroupExpression group) inner = group.Inner;
        IAplExpression[] operands = inner switch
        {
            BinaryOpExpression binary => [binary.Left, binary.Right],
            UnaryOpExpression unary => [unary.Operand],
            FunctionExpression call => [.. call.Arguments],
            _ => [],
        };
        BinaryOperator? logical = inner is BinaryOpExpression { Operator: BinaryOperator.And or BinaryOperator.Or } l ? l.Operator : null;
        bool truth = inner switch
        {
            BinaryOpExpression binary => binary.Operator is not (BinaryOperator.Add or BinaryOperator.Subtract
                or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo),
            UnaryOpExpression unary => unary.Operator == UnaryOperator.Not,
            _ => false,
        };
        return new AplProbe(AplFormatter.Format(expression), BoundExpression.Bind(expression, hero, variables),
            operands.Select(o => Of(o, hero, variables)).ToArray(), logical, truth);
    }

    /// <summary>The values at the hero's current target. <paramref name="skipped"/> marks this expression as one the decision never read.</summary>
    public AplNodeTrace Trace(SimulationContext ctx, bool skipped = false)
    {
        double value = _truth ? (_bound.Evaluate(ctx) ? 1.0 : 0.0) : _bound.EvaluateNumeric(ctx);
        var operands = new AplNodeTrace[_operands.Length];
        for (int i = 0; i < operands.Length; i++)
        {
            bool skip = skipped;
            if (i == 1 && _logical is { } op)
            {
                // '&' reads its right side only after a true left side, '|' only after a false one.
                bool left = _operands[0]._bound.Evaluate(ctx);
                skip |= op == BinaryOperator.And ? !left : left;
            }
            operands[i] = _operands[i].Trace(ctx, skip);
        }
        return new AplNodeTrace(_text, value, _truth, skipped, operands);
    }
}
