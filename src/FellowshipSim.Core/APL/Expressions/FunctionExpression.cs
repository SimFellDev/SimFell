namespace FellowshipSim.Core.APL.Expressions;

/// <summary>A call of a math function on its arguments: <c>floor(buff.x.remains)</c>, <c>max(a, b)</c>.</summary>
public class FunctionExpression(AplFunction function, IReadOnlyList<IAplExpression> arguments) : IAplExpression
{
    public AplFunction Function { get; } = function;
    public IReadOnlyList<IAplExpression> Arguments { get; } = arguments;

    public bool Evaluate(Hero hero, SimulationContext ctx) => EvaluateNumeric(hero, ctx) != 0.0;

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        Span<double> values = stackalloc double[Arguments.Count];
        for (int i = 0; i < values.Length; i++) values[i] = Arguments[i].EvaluateNumeric(hero, ctx);
        return Function.Apply(values);
    }

    public override string ToString() => $"{Function.Name()}({string.Join(", ", Arguments)})";
}
