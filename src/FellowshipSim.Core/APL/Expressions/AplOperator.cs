namespace FellowshipSim.Core.APL.Expressions;

public enum BinaryOperator
{
    And,
    Or,
    Add,
    Subtract,
    Multiply,
    /// <summary>Written '%', as in SimulationCraft. Division by zero gives 0.</summary>
    Divide,
    /// <summary>Written '%%', as in SimulationCraft. A zero divisor gives 0.</summary>
    Modulo,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
}

public enum UnaryOperator
{
    /// <summary>Written '!': the negation, 1 where its operand is 0 and 0 elsewhere.</summary>
    Not,
    /// <summary>Written '-' before one operand: its value with the opposite sign.</summary>
    Minus,
}

public static class AplOperatorSymbols
{
    public static string Symbol(this BinaryOperator op) => op switch
    {
        BinaryOperator.And => "&",
        BinaryOperator.Or => "|",
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "%",
        BinaryOperator.Modulo => "%%",
        BinaryOperator.Equal => "=",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.Less => "<",
        BinaryOperator.LessEqual => "<=",
        BinaryOperator.Greater => ">",
        BinaryOperator.GreaterEqual => ">=",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

    public static string Symbol(this UnaryOperator op) => op switch
    {
        UnaryOperator.Not => "!",
        UnaryOperator.Minus => "-",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

    /// <summary>The operator's name and meaning in the condition reference.</summary>
    public static AplDoc Doc(this BinaryOperator op) => op switch
    {
        BinaryOperator.And => new("and", "1 when both sides are true, else 0. Also written as the word and."),
        BinaryOperator.Or => new("or", "1 when either side is true, else 0. Also written as the word or."),
        BinaryOperator.Add => new("Add", "The sum of both sides."),
        BinaryOperator.Subtract => new("Subtract", "The left side minus the right side."),
        BinaryOperator.Multiply => new("Multiply", "The product of both sides."),
        BinaryOperator.Divide => new("Divide", "The left side divided by the right side. A zero divisor gives 0."),
        BinaryOperator.Modulo => new("Remainder", "The remainder of the left side divided by the right side. A zero divisor gives 0."),
        BinaryOperator.Equal => new("Equal", "1 when the sides are within 0.0001 of each other, else 0. Also written ==."),
        BinaryOperator.NotEqual => new("Not equal", "1 when the sides differ by 0.0001 or more, else 0."),
        BinaryOperator.Less => new("Less than", "1 when the left side is less than the right side, else 0."),
        BinaryOperator.LessEqual => new("At most", "1 when the left side is less than or equal to the right side, else 0."),
        BinaryOperator.Greater => new("Greater than", "1 when the left side is greater than the right side, else 0."),
        BinaryOperator.GreaterEqual => new("At least", "1 when the left side is greater than or equal to the right side, else 0."),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

    /// <summary>The prefix operator's name and meaning in the condition reference.</summary>
    public static AplDoc Doc(this UnaryOperator op) => op switch
    {
        UnaryOperator.Not => new("not", "1 when the value is 0, else 0. Also written as the word not."),
        UnaryOperator.Minus => new("Negate", "The value with the opposite sign."),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };
}
