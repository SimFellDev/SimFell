namespace FellowshipSim.Core.APL.Expressions;

/// <summary>
/// A math function bound to its arguments. Between events it breaks where an argument's curve ends, and where its value
/// jumps or takes another curve: <c>floor</c>, <c>ceil</c> and <c>round</c> where the argument passes a whole number (or a
/// half for <c>round</c>), <c>abs</c> where the argument passes 0, and <c>min</c> and <c>max</c> where the argument they
/// read passes another.
/// </summary>
internal sealed class BoundFunction(AplFunction function, BoundExpression[] arguments) : BoundNumeric
{
    public override double EvaluateNumeric(SimulationContext ctx)
    {
        Span<double> values = stackalloc double[arguments.Length];
        for (int i = 0; i < values.Length; i++) values[i] = arguments[i].EvaluateNumeric(ctx);
        return function.Apply(values);
    }

    internal override double NumericAt(SimulationContext ctx, SimTime time)
    {
        Span<double> values = stackalloc double[arguments.Length];
        for (int i = 0; i < values.Length; i++) values[i] = arguments[i].NumericAt(ctx, time);
        return function.Apply(values);
    }

    private bool IsStep => function is AplFunction.Floor or AplFunction.Ceil or AplFunction.Round;

    // The argument min or max reads now: the first with the smallest or largest value.
    private int Picked(SimulationContext ctx)
    {
        int picked = 0;
        double best = arguments[0].EvaluateNumeric(ctx);
        for (int i = 1; i < arguments.Length; i++)
        {
            double value = arguments[i].EvaluateNumeric(ctx);
            if (function == AplFunction.Min ? value < best : value > best) (picked, best) = (i, value);
        }
        return picked;
    }

    // A step function holds its value until it breaks; abs, min and max follow the argument they read.
    internal override bool IsLevel(SimulationContext ctx) => function switch
    {
        _ when IsStep => true,
        AplFunction.Abs => arguments[0].IsLevel(ctx),
        _ => arguments[Picked(ctx)].IsLevel(ctx),
    };

    internal override TimeCurve Curve(SimulationContext ctx) => function switch
    {
        _ when IsStep => TimeCurve.Constant(EvaluateNumeric(ctx)),
        AplFunction.Abs => arguments[0].EvaluateNumeric(ctx) < 0.0 ? -arguments[0].Curve(ctx) : arguments[0].Curve(ctx),
        _ => arguments[Picked(ctx)].Curve(ctx),
    };

    internal override SimTime Until(SimulationContext ctx)
    {
        var until = SimTime.Infinite;
        bool level = true;
        foreach (var argument in arguments)
        {
            until = Crossing.Earlier(until, argument.Until(ctx));
            level &= argument.IsLevel(ctx);
        }
        if (level) return until;

        var roots = new List<double>();
        double span = Crossing.Later(ctx.CurrentTime, until).Milliseconds - (double)ctx.CurrentTime.Milliseconds;
        // From a millisecond before now: a boundary the argument sits on now is left in the next millisecond.
        void Crossings(TimeCurve curve, double value)
        {
            Polynomial.Roots(curve.NumeratorLess(value), -1.0, span, roots);
            Polynomial.Roots(curve.Denominator, -1.0, span, roots);
        }

        switch (function)
        {
            case AplFunction.Abs:
                Crossings(arguments[0].Curve(ctx), 0.0);
                break;
            case AplFunction.Floor or AplFunction.Ceil or AplFunction.Round:
                var curve = arguments[0].Curve(ctx);
                double value = arguments[0].EvaluateNumeric(ctx);
                double whole = function == AplFunction.Round ? Math.Round(value, MidpointRounding.AwayFromZero) : Math.Floor(value);
                // The value changes where the argument reaches the whole numbers (halves for round) on either side.
                double low = function == AplFunction.Round ? whole - 0.5 : function == AplFunction.Ceil ? Math.Ceiling(value) - 1.0 : whole;
                double high = function == AplFunction.Round ? whole + 0.5 : function == AplFunction.Ceil ? Math.Ceiling(value) : whole + 1.0;
                Crossings(curve, low);
                Crossings(curve, high);
                break;
            default:
                int picked = Picked(ctx);
                var pickedCurve = arguments[picked].Curve(ctx);
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (i != picked) Crossings(pickedCurve - arguments[i].Curve(ctx), 0.0);
                }
                break;
        }

        var now = ctx.CurrentTime;
        if (IsStep)
        {
            // A step function's value jumps: the first millisecond around a crossing that reads another value.
            roots.Sort();
            double current = EvaluateNumeric(ctx);
            var limit = Crossing.Later(now, until);
            foreach (double root in roots)
            {
                if (Crossing.FirstAround(now, limit, root, time => NumericAt(ctx, time) != current) is { } changed) return changed;
            }
            return until;
        }
        foreach (double root in roots)
        {
            until = Crossing.Earlier(until, Crossing.Later(now, SimTime.FromMilliseconds(now.Milliseconds + (long)Math.Floor(root))));
        }
        return until;
    }
}
