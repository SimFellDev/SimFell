namespace FellowshipSim.Core.APL.Expressions;

/// <summary>A math function of a condition, written as its name and its arguments in parentheses: <c>floor(x)</c>, <c>max(a, b)</c>.</summary>
public enum AplFunction
{
    Abs,
    Ceil,
    Floor,
    Round,
    Min,
    Max,
}

public static class AplFunctions
{
    /// <summary>The functions by the name an action list writes ("floor").</summary>
    public static IReadOnlyDictionary<string, AplFunction> ByName { get; } =
        Enum.GetValues<AplFunction>().ToDictionary(Name, f => f, StringComparer.OrdinalIgnoreCase);

    /// <summary>The name an action list writes for <paramref name="function"/>: lower case ("floor").</summary>
    public static string Name(this AplFunction function) => function.ToString().ToLowerInvariant();

    /// <summary>The fewest arguments <paramref name="function"/> takes.</summary>
    public static int MinArguments(this AplFunction function) => function is AplFunction.Min or AplFunction.Max ? 2 : 1;

    /// <summary>The most arguments <paramref name="function"/> takes.</summary>
    public static int MaxArguments(this AplFunction function) => function is AplFunction.Min or AplFunction.Max ? int.MaxValue : 1;

    /// <summary>True when <paramref name="count"/> arguments fit <paramref name="function"/>.</summary>
    public static bool Takes(this AplFunction function, int count) => count >= function.MinArguments() && count <= function.MaxArguments();

    /// <summary>The arguments <paramref name="function"/> takes, as an error message says it ("1 argument").</summary>
    public static string ArgumentsText(this AplFunction function) =>
        function.MaxArguments() == 1 ? "1 argument" : $"{function.MinArguments()} or more arguments";

    /// <summary>The value of <paramref name="function"/> on <paramref name="arguments"/>.</summary>
    public static double Apply(this AplFunction function, ReadOnlySpan<double> arguments)
    {
        switch (function)
        {
            case AplFunction.Abs: return Math.Abs(arguments[0]);
            case AplFunction.Ceil: return Math.Ceiling(arguments[0]);
            case AplFunction.Floor: return Math.Floor(arguments[0]);
            case AplFunction.Round: return Math.Round(arguments[0], MidpointRounding.AwayFromZero);
            case AplFunction.Min:
            {
                double min = arguments[0];
                for (int i = 1; i < arguments.Length; i++) min = Math.Min(min, arguments[i]);
                return min;
            }
            default:
            {
                double max = arguments[0];
                for (int i = 1; i < arguments.Length; i++) max = Math.Max(max, arguments[i]);
                return max;
            }
        }
    }

    /// <summary>The function's call and meaning in the condition reference.</summary>
    public static AplDoc Doc(this AplFunction function) => function switch
    {
        AplFunction.Abs => new("abs(x)", "The value of x without its sign. Example: abs(-2.5) is 2.5."),
        AplFunction.Ceil => new("ceil(x)", "The smallest whole number that is x or more. Example: ceil(2.1) is 3."),
        AplFunction.Floor => new("floor(x)", "The largest whole number that is x or less. Example: floor(2.9) is 2."),
        AplFunction.Round => new("round(x)", "x rounded to the nearest whole number. A value halfway between two whole numbers goes away from 0. Example: round(2.5) is 3."),
        AplFunction.Min => new("min(a, b, ...)", "The smallest of the values. It takes 2 or more values."),
        AplFunction.Max => new("max(a, b, ...)", "The largest of the values. It takes 2 or more values."),
        _ => throw new ArgumentOutOfRangeException(nameof(function), function, null),
    };
}
