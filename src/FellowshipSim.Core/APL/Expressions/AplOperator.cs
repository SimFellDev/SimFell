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
}
