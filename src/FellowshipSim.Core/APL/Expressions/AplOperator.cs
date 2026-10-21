namespace FellowshipSim.Core.APL.Expressions;

public enum BinaryOperator
{
    And,
    Or,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
}

public enum UnaryOperator
{
    Not,
    Negate,
}

public static class AplOperatorSymbols
{
    public static string Symbol(this BinaryOperator op) => op switch
    {
        BinaryOperator.And => "&",
        BinaryOperator.Or => "|",
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
        UnaryOperator.Negate => "-",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };
}
