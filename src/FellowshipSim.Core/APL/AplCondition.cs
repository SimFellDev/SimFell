using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

/// <summary>
/// One row of a condition in the editor's builder: its join to the row before it (<c>and</c> or <c>or</c>, null on the first
/// row of a group), its <c>not</c>, and either a value (<see cref="Left"/>, compared in turn to each side in
/// <see cref="Comparisons"/>) or a parenthesised group of rows (<see cref="Items"/>).
/// </summary>
public sealed record AplConditionItem(
    string? Join,
    bool Not,
    IReadOnlyList<AplTerm>? Left = null,
    IReadOnlyList<AplComparison>? Comparisons = null,
    IReadOnlyList<AplConditionItem>? Items = null);

/// <summary>
/// The rows of a condition, as the builder shows them. Rows joined by <c>and</c> and <c>or</c> without parentheses stay in
/// one group, terms joined by arithmetic without parentheses stay in one side, and comparisons without parentheses stay
/// on one row, so writing them back with their joins and operators reads the same as the text.
/// </summary>
public static class AplCondition
{
    /// <summary>The rows of <paramref name="expression"/>.</summary>
    public static IReadOnlyList<AplConditionItem> Rows(IAplExpression expression)
    {
        var rows = new List<AplConditionItem>();
        AddRows(expression, null, rows);
        return rows;
    }

    private static void AddRows(IAplExpression expression, string? join, List<AplConditionItem> rows)
    {
        if (expression is BinaryOpExpression { Operator: BinaryOperator.And or BinaryOperator.Or } logical)
        {
            AddRows(logical.Left, join, rows);
            AddRows(logical.Right, logical.Operator == BinaryOperator.And ? "and" : "or", rows);
            return;
        }
        rows.Add(Row(expression, join));
    }

    private static AplConditionItem Row(IAplExpression expression, string? join)
    {
        switch (expression)
        {
            case BinaryOpExpression comparison when IsComparison(comparison.Operator):
                var comparisons = new List<AplComparison>();
                return new AplConditionItem(join, false, Compared(comparison, comparisons), comparisons);
            // 'not' binds to one value, so a negated comparison or sum is written in parentheses: not (worb >= 5).
            case UnaryOpExpression { Operator: UnaryOperator.Not, Operand: GroupExpression { Inner: BinaryOpExpression inner } }
                when IsComparison(inner.Operator) || IsArithmetic(inner.Operator):
                return Row(inner, join) with { Not = true };
            case UnaryOpExpression { Operator: UnaryOperator.Not } not when Row(not.Operand, join) is { Not: false } operand:
                return operand with { Not = true };
            case GroupExpression group:
                var items = new List<AplConditionItem>();
                AddRows(group.Inner, null, items);
                return new AplConditionItem(join, false, Items: items);
            default:
                return new AplConditionItem(join, false, Terms(expression));
        }
    }

    // Comparisons chain to the left: "a < b < c" is (a < b) < c. The terms of the first side, with each comparison after it.
    private static IReadOnlyList<AplTerm> Compared(IAplExpression expression, List<AplComparison> comparisons)
    {
        if (expression is not BinaryOpExpression comparison || !IsComparison(comparison.Operator)) return Terms(expression);
        var left = Compared(comparison.Left, comparisons);
        comparisons.Add(new AplComparison(comparison.Operator.Symbol(), Terms(comparison.Right)));
        return left;
    }

    /// <summary>The terms of one side of a row, in the order written.</summary>
    private static IReadOnlyList<AplTerm> Terms(IAplExpression expression)
    {
        var terms = new List<AplTerm>();
        AddTerms(expression, null, terms);
        return terms;
    }

    // The parser chains each level to the left and writes a looser operator under a tighter one only in parentheses, so
    // the terms in written order, with each group's parentheses, read back as the same expression.
    private static void AddTerms(IAplExpression expression, string? op, List<AplTerm> terms)
    {
        if (expression is BinaryOpExpression arithmetic && IsArithmetic(arithmetic.Operator))
        {
            AddTerms(arithmetic.Left, op, terms);
            AddTerms(arithmetic.Right, arithmetic.Operator.Symbol(), terms);
            return;
        }
        terms.Add(Term(expression, op));
    }

    // A prefix operator binds to one primary: a value, a number, a group or another prefix operator.
    private static AplTerm Term(IAplExpression expression, string? op) => expression switch
    {
        _ when Number(expression) is { } value => new AplTerm(op, Number: value),
        PropertyExpression property => new AplTerm(op, Path: property.PropertyPath),
        GroupExpression group => new AplTerm(op, Items: Rows(group.Inner)),
        FunctionExpression call => new AplTerm(op, Function: call.Function.Name(), Arguments: call.Arguments.Select(Rows).ToList()),
        UnaryOpExpression unary => Prefixed(unary.Operator, Term(unary.Operand, op)),
        _ => throw new ArgumentOutOfRangeException(nameof(expression), expression.GetType().Name, null)
    };

    private static AplTerm Prefixed(UnaryOperator op, AplTerm operand) => operand with { Prefix = op.Symbol() + operand.Prefix };

    private static bool IsComparison(BinaryOperator op) => op is BinaryOperator.Equal or BinaryOperator.NotEqual
        or BinaryOperator.Less or BinaryOperator.LessEqual or BinaryOperator.Greater or BinaryOperator.GreaterEqual;

    private static bool IsArithmetic(BinaryOperator op) => op is BinaryOperator.Add or BinaryOperator.Subtract
        or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo;

    private static double? Number(IAplExpression expression) => expression switch
    {
        ConstantExpression constant => constant.Value,
        UnaryOpExpression { Operator: UnaryOperator.Minus, Operand: ConstantExpression constant } => -constant.Value,
        _ => null
    };
}
