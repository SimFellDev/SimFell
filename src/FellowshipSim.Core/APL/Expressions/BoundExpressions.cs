namespace FellowshipSim.Core.APL.Expressions;

/// <summary>
/// An expression bound to one hero (<see cref="BoundExpression.Bind"/>): every name is resolved to the ability, resource
/// state, cooldown or aura name it reads, so an evaluation does no string or dictionary work. It gives the same values as
/// the <see cref="IAplExpression"/> it was bound from.
/// </summary>
/// <remarks>
/// Between events a value is a quotient of two polynomials in time (<see cref="TimeCurve"/>): resources, enemies and
/// health change only in events, an aura read changes when the aura runs out, a cooldown read when a charge returns, and a
/// remaining time falls by a second per second; sums, products and quotients of these are such quotients.
/// <see cref="Until"/>, <see cref="Curve"/> and <see cref="NumericAt"/> describe the value up to its next break, and
/// <see cref="NextChange"/> solves the time the expression's truth changes.
/// </remarks>
public abstract class BoundExpression
{
    public abstract bool Evaluate(SimulationContext ctx);
    public abstract double EvaluateNumeric(SimulationContext ctx);

    /// <summary>
    /// The next break of <see cref="EvaluateNumeric"/> with no event before it: the next time an aura it reads runs out, a
    /// charge returns or a remainder wraps. <see cref="SimTime.Infinite"/> when only an event changes it.
    /// </summary>
    internal abstract SimTime Until(SimulationContext ctx);

    /// <summary><see cref="EvaluateNumeric"/> from now to <see cref="Until"/>, by the milliseconds after now.</summary>
    internal virtual TimeCurve Curve(SimulationContext ctx) => TimeCurve.Constant(EvaluateNumeric(ctx));

    /// <summary><see cref="EvaluateNumeric"/> at <paramref name="time"/>, from the current time to <see cref="Until"/>, with no event before it.</summary>
    internal abstract double NumericAt(SimulationContext ctx, SimTime time);

    /// <summary><see cref="Evaluate"/> at <paramref name="time"/>, from the current time to <see cref="Until"/>, with no event before it.</summary>
    internal virtual bool EvaluateAt(SimulationContext ctx, SimTime time) => NumericAt(ctx, time) != 0.0;

    /// <summary>
    /// The earliest time after the current time at which <see cref="Evaluate"/> can give another value with no event before
    /// it, or <see cref="SimTime.Infinite"/>. A value read as a truth changes where its numerator or denominator is 0.
    /// </summary>
    public virtual SimTime NextChange(SimulationContext ctx)
    {
        var curve = Curve(ctx);
        return Crossing.First(ctx, this, Until(ctx), curve.Numerator, curve.Denominator);
    }

    /// <summary>Binds <paramref name="expression"/> to <paramref name="hero"/>.</summary>
    public static BoundExpression Bind(IAplExpression expression, Hero hero) => expression switch
    {
        ConstantExpression c => new BoundConstant(c.Value),
        GroupExpression g => Bind(g.Inner, hero),
        UnaryOpExpression { Operator: UnaryOperator.Not } u => new BoundNot(Bind(u.Operand, hero)),
        UnaryOpExpression { Operator: UnaryOperator.Negate } u => new BoundNegate(Bind(u.Operand, hero)),
        BinaryOpExpression b => new BoundBinary(Bind(b.Left, hero), b.Operator, Bind(b.Right, hero)),
        PropertyExpression p => BindProperty(p, hero),
        _ => throw new ArgumentException($"Unsupported APL expression: '{expression}'", nameof(expression)),
    };

    // The same order as PropertyExpression.EvaluateNumeric: a named value, then a resource, then a scoped path; else 0.
    private static BoundExpression BindProperty(PropertyExpression p, Hero hero)
    {
        if (PropertyExpression.Named.TryGetValue(p.PropertyPath, out var named)) return new BoundNamed(hero, named);
        if (hero.FindResource(p.ResourceName) is { } resource) return new BoundResource(hero.Resources.Get(resource));
        return p.Scope switch
        {
            PropertyExpression.BuffScope => new BoundAura(hero, debuff: false, p.Name!, BoundAura.PropertyOf(p.Property!)),
            PropertyExpression.DebuffScope => new BoundAura(hero, debuff: true, p.Name!, BoundAura.PropertyOf(p.Property!)),
            // Cooldowns are kept under the ability's own name ("Cold Snap"), not its action-list name.
            PropertyExpression.CooldownScope => BoundCooldown.For(hero, AplNames.FindAbility(hero, p.Name!), p.Name!, p.Property!),
            _ => new BoundConstant(0.0),
        };
    }
}

/// <summary>
/// A value as a function of the milliseconds τ after the current time: <see cref="Numerator"/>(τ) /
/// <see cref="Denominator"/>(τ), each a polynomial with its coefficients from τ⁰ up.
/// </summary>
internal readonly struct TimeCurve(double[] numerator, double[] denominator)
{
    public double[] Numerator { get; } = numerator;
    public double[] Denominator { get; } = denominator;

    public static TimeCurve Constant(double value) => new([value], [1.0]);

    /// <summary>A value of <paramref name="value"/> now that changes by <paramref name="perMillisecond"/> each millisecond.</summary>
    public static TimeCurve Line(double value, double perMillisecond) => new([value, perMillisecond], [1.0]);

    public bool IsConstant => Polynomial.Degree(Numerator) <= 0 && Polynomial.Degree(Denominator) <= 0;

    public static TimeCurve operator -(TimeCurve a) => new(Polynomial.Scale(a.Numerator, -1.0), a.Denominator);

    public static TimeCurve operator +(TimeCurve a, TimeCurve b) => new(
        Polynomial.Add(Polynomial.Multiply(a.Numerator, b.Denominator), Polynomial.Multiply(b.Numerator, a.Denominator)),
        Polynomial.Multiply(a.Denominator, b.Denominator));

    public static TimeCurve operator -(TimeCurve a, TimeCurve b) => a + -b;

    public static TimeCurve operator *(TimeCurve a, TimeCurve b) =>
        new(Polynomial.Multiply(a.Numerator, b.Numerator), Polynomial.Multiply(a.Denominator, b.Denominator));

    public static TimeCurve operator /(TimeCurve a, TimeCurve b) =>
        new(Polynomial.Multiply(a.Numerator, b.Denominator), Polynomial.Multiply(a.Denominator, b.Numerator));

    /// <summary>The numerator of this curve less <paramref name="value"/>: 0 where the curve equals the value.</summary>
    public double[] NumeratorLess(double value) => Polynomial.Add(Numerator, Polynomial.Scale(Denominator, -value));
}

/// <summary>Polynomials as their coefficients from x⁰ up.</summary>
internal static class Polynomial
{
    /// <summary>The highest power with a coefficient that is not 0; -1 for the zero polynomial.</summary>
    public static int Degree(double[] p)
    {
        int degree = p.Length - 1;
        while (degree >= 0 && p[degree] == 0.0) degree--;
        return degree;
    }

    public static double[] Add(double[] a, double[] b)
    {
        var sum = new double[Math.Max(a.Length, b.Length)];
        for (int i = 0; i < a.Length; i++) sum[i] += a[i];
        for (int i = 0; i < b.Length; i++) sum[i] += b[i];
        return sum;
    }

    public static double[] Scale(double[] p, double factor)
    {
        var scaled = new double[p.Length];
        for (int i = 0; i < p.Length; i++) scaled[i] = p[i] * factor;
        return scaled;
    }

    public static double[] Multiply(double[] a, double[] b)
    {
        var product = new double[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++)
        {
            for (int j = 0; j < b.Length; j++) product[i + j] += a[i] * b[j];
        }
        return product;
    }

    public static double Evaluate(double[] p, double x)
    {
        double value = 0.0;
        for (int i = p.Length - 1; i >= 0; i--) value = value * x + p[i];
        return value;
    }

    private static double[] Derivative(double[] p)
    {
        if (p.Length <= 1) return [0.0];
        var derivative = new double[p.Length - 1];
        for (int i = 1; i < p.Length; i++) derivative[i - 1] = p[i] * i;
        return derivative;
    }

    /// <summary>
    /// Adds the real roots of <paramref name="p"/> between <paramref name="low"/> and <paramref name="high"/> to
    /// <paramref name="roots"/>. Between two turning points a polynomial rises or falls, so each stretch holds at most one
    /// root, found by halving the stretch; the turning points are the roots of the derivative.
    /// </summary>
    public static void Roots(double[] p, double low, double high, List<double> roots)
    {
        int degree = Degree(p);
        if (degree <= 0) return;
        // Every root lies within Cauchy's bound.
        double bound = 0.0;
        for (int i = 0; i < degree; i++) bound = Math.Max(bound, Math.Abs(p[i] / p[degree]));
        high = Math.Min(high, 1.0 + bound);
        low = Math.Max(low, -1.0 - bound);
        if (!(low < high)) return;
        if (degree == 1)
        {
            double root = -p[0] / p[1];
            if (root > low && root < high) roots.Add(root);
            return;
        }
        var turns = new List<double>();
        Roots(Derivative(p), low, high, turns);
        turns.Sort();
        turns.Add(high);
        double a = low, fa = Evaluate(p, low);
        foreach (double b in turns)
        {
            double fb = Evaluate(p, b);
            if (fb == 0.0) roots.Add(b);
            else if (fa != 0.0 && (fa < 0.0) != (fb < 0.0)) roots.Add(Halve(p, a, fa, b));
            (a, fa) = (b, fb);
        }
    }

    // The root of p between a and b, where p(a) and p(b) have opposite signs.
    private static double Halve(double[] p, double a, double fa, double b)
    {
        for (int i = 0; i < 200 && b - a > 1e-6; i++)
        {
            double middle = 0.5 * (a + b);
            double fm = Evaluate(p, middle);
            if (fm == 0.0) return middle;
            if ((fm < 0.0) == (fa < 0.0)) (a, fa) = (middle, fm);
            else b = middle;
        }
        return 0.5 * (a + b);
    }
}

/// <summary>The first time a truth on a curve of values changes.</summary>
internal static class Crossing
{
    /// <summary>
    /// The first whole millisecond after the current time and before <paramref name="until"/> at which
    /// <paramref name="expression"/>'s truth differs from now, where the truth can change only at a root of one of
    /// <paramref name="polynomials"/> (in the milliseconds after now). Else <paramref name="until"/>: the curve ends there.
    /// </summary>
    public static SimTime First(SimulationContext ctx, BoundExpression expression, SimTime until, params ReadOnlySpan<double[]> polynomials)
    {
        var now = ctx.CurrentTime;
        var limit = Later(now, until);
        var roots = new List<double>();
        foreach (var p in polynomials) Polynomial.Roots(p, -3.0, limit.Milliseconds - (double)now.Milliseconds, roots);
        if (roots.Count == 0) return limit;
        roots.Sort();
        bool current = expression.Evaluate(ctx);
        foreach (double root in roots)
        {
            if (FirstAround(now, limit, root, ms => expression.EvaluateAt(ctx, ms) != current) is { } changed) return changed;
        }
        return limit;
    }

    /// <summary>
    /// The first millisecond after <paramref name="now"/> and before <paramref name="limit"/>, within two of
    /// <paramref name="root"/> (milliseconds after now; rounding moves a change by a millisecond or two), at which
    /// <paramref name="changed"/> holds. Null when none does.
    /// </summary>
    public static SimTime? FirstAround(SimTime now, SimTime limit, double root, Func<SimTime, bool> changed)
    {
        double at = now.Milliseconds + root;
        if (at - 2 >= limit.Milliseconds) return null;
        for (long ms = Math.Max(now.Milliseconds + 1, (long)Math.Floor(at) - 2); ms < limit.Milliseconds && ms <= (long)Math.Ceiling(at) + 2; ms++)
        {
            var time = SimTime.FromMilliseconds(ms);
            if (changed(time)) return time;
        }
        return null;
    }

    /// <summary>The first millisecond after <paramref name="time"/>.</summary>
    public static SimTime Next(SimTime time) => SimTime.FromMilliseconds(time.Milliseconds + 1);

    /// <summary><paramref name="time"/>, or the first millisecond after <paramref name="now"/> when it is not later.</summary>
    public static SimTime Later(SimTime now, SimTime time) => time > now ? time : Next(now);

    public static SimTime Earlier(SimTime a, SimTime b) => a < b ? a : b;
}

/// <summary>A value read as a number; it holds when it is not 0.</summary>
internal abstract class BoundNumeric : BoundExpression
{
    public sealed override bool Evaluate(SimulationContext ctx) => EvaluateNumeric(ctx) != 0.0;
}

/// <summary>A value that changes only in events.</summary>
internal abstract class BoundEventValue : BoundNumeric
{
    internal sealed override SimTime Until(SimulationContext ctx) => SimTime.Infinite;
    internal sealed override double NumericAt(SimulationContext ctx, SimTime time) => EvaluateNumeric(ctx);
    public sealed override SimTime NextChange(SimulationContext ctx) => SimTime.Infinite;
}

internal sealed class BoundConstant(double value) : BoundExpression
{
    public override bool Evaluate(SimulationContext ctx) => Math.Abs(value) > 0.00001;
    public override double EvaluateNumeric(SimulationContext ctx) => value;
    internal override SimTime Until(SimulationContext ctx) => SimTime.Infinite;
    internal override double NumericAt(SimulationContext ctx, SimTime time) => value;
    internal override bool EvaluateAt(SimulationContext ctx, SimTime time) => Evaluate(ctx);
    public override SimTime NextChange(SimulationContext ctx) => SimTime.Infinite;
}

/// <summary>A named value: the enemies in combat, the target's health and fixed fight values, which change only in events.</summary>
internal sealed class BoundNamed(Hero hero, Func<Hero, SimulationContext, double> read) : BoundEventValue
{
    public override double EvaluateNumeric(SimulationContext ctx) => read(hero, ctx);
}

internal sealed class BoundResource(ResourceState state) : BoundEventValue
{
    public override double EvaluateNumeric(SimulationContext ctx) => state.Current;
}

/// <summary>
/// A property of an aura on the hero (buff) or on the current target (debuff). The aura is found by its action-list name
/// among the active auras, as <see cref="PropertyExpression"/> finds it: several definitions can share a name (copies with
/// another duration or source).
/// </summary>
internal sealed class BoundAura(Hero hero, bool debuff, string slug, BoundAura.AuraProperty property) : BoundNumeric
{
    public enum AuraProperty { Up, Down, Stack, AtMaxStacks, Remains }

    /// <summary>The property named <paramref name="name"/>; a property auras lack reads as "up".</summary>
    public static AuraProperty PropertyOf(string name) => name switch
    {
        "down" => AuraProperty.Down,
        "stack" => AuraProperty.Stack,
        "at_max_stacks" => AuraProperty.AtMaxStacks,
        "remains" => AuraProperty.Remains,
        _ => AuraProperty.Up,
    };

    public override double EvaluateNumeric(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        return Read(Find(ctx, now), now);
    }

    private Aura? Find(SimulationContext ctx, SimTime now)
    {
        if (!debuff) return hero.Auras.FindActive(slug, now);
        var target = ctx.Player.CurrentTarget;
        return target == null ? null : target.Auras.FindActive(slug, now);
    }

    private double Read(Aura? aura, SimTime now) => property switch
    {
        AuraProperty.Up => aura != null ? 1.0 : 0.0,
        AuraProperty.Down => aura == null ? 1.0 : 0.0,
        AuraProperty.Stack => aura?.Stacks ?? 0,
        AuraProperty.AtMaxStacks => aura != null && aura.Stacks >= aura.MaxStacks ? 1.0 : 0.0,
        _ => aura == null ? 0.0 : aura.ExpiresAt == SimTime.Infinite ? double.MaxValue : (aura.ExpiresAt - now).TotalSeconds,
    };

    // The aura found now runs out at its expiry; another aura of the name appears only in an event.
    internal override SimTime Until(SimulationContext ctx) => Find(ctx, ctx.CurrentTime)?.ExpiresAt ?? SimTime.Infinite;

    internal override TimeCurve Curve(SimulationContext ctx) =>
        property == AuraProperty.Remains && Find(ctx, ctx.CurrentTime) is { } aura && aura.ExpiresAt != SimTime.Infinite
            ? TimeCurve.Line(EvaluateNumeric(ctx), -0.001)
            : TimeCurve.Constant(EvaluateNumeric(ctx));

    internal override double NumericAt(SimulationContext ctx, SimTime time)
    {
        var aura = Find(ctx, ctx.CurrentTime);
        return Read(aura != null && time >= aura.ExpiresAt ? null : aura, time);
    }
}

/// <summary>
/// A property of an ability's cooldown. The cooldown state is looked up again only when the hero's cooldown states change
/// (<see cref="Hero.CooldownsVersion"/>): a new fight clears them, and a cooldown's first use adds one.
/// </summary>
internal sealed class BoundCooldown : BoundNumeric
{
    private enum CooldownProperty { Ready, Remains, Charges }

    private readonly Hero _hero;
    private readonly Ability? _ability;
    private readonly string _name;
    private readonly CooldownProperty _property;
    private CooldownState? _state;
    private int _version = -1;

    private BoundCooldown(Hero hero, Ability? ability, string name, CooldownProperty property) =>
        (_hero, _ability, _name, _property) = (hero, ability, name, property);

    /// <summary>The cooldown of <paramref name="ability"/> (or of <paramref name="slug"/> when the hero has no such ability); a property cooldowns lack reads as "ready".</summary>
    public static BoundCooldown For(Hero hero, Ability? ability, string slug, string property) =>
        new(hero, ability, ability?.Name ?? slug, property switch
        {
            "remains" => CooldownProperty.Remains,
            "charges" => CooldownProperty.Charges,
            _ => CooldownProperty.Ready,
        });

    private CooldownState? State()
    {
        if (_version != _hero.CooldownsVersion)
        {
            _hero.Cooldowns.TryGetValue(_name, out _state);
            _version = _hero.CooldownsVersion;
        }
        return _state;
    }

    public override double EvaluateNumeric(SimulationContext ctx) => NumericAt(ctx, ctx.CurrentTime);

    // A charge returns at NextChargeAt; a cooldown first used, or one that starts recharging, does so in an event.
    internal override SimTime Until(SimulationContext ctx) =>
        State() is { } state && state.ChargesAt(ctx.CurrentTime) < state.MaxCharges ? state.NextChargeAt : SimTime.Infinite;

    internal override TimeCurve Curve(SimulationContext ctx) =>
        _property == CooldownProperty.Remains && State() is { } state && state.ChargesAt(ctx.CurrentTime) == 0 && state.NextChargeAt > ctx.CurrentTime
            ? TimeCurve.Line(EvaluateNumeric(ctx), -0.001)
            : TimeCurve.Constant(EvaluateNumeric(ctx));

    internal override double NumericAt(SimulationContext ctx, SimTime time)
    {
        var state = State();
        var now = ctx.CurrentTime;
        return _property switch
        {
            CooldownProperty.Ready => state == null || state.ChargesAt(now) > 0 ? 1.0 : 0.0,
            CooldownProperty.Remains => state != null && state.ChargesAt(now) == 0 ? Math.Max(0.0, (state.NextChargeAt - time).TotalSeconds) : 0.0,
            _ => state != null ? state.ChargesAt(now) : _ability?.MaxCharges ?? 1,
        };
    }
}

internal sealed class BoundNot(BoundExpression operand) : BoundExpression
{
    public override bool Evaluate(SimulationContext ctx) => !operand.Evaluate(ctx);
    public override double EvaluateNumeric(SimulationContext ctx) => operand.Evaluate(ctx) ? 0.0 : 1.0;
    internal override SimTime Until(SimulationContext ctx) => operand.NextChange(ctx);
    internal override double NumericAt(SimulationContext ctx, SimTime time) => operand.EvaluateAt(ctx, time) ? 0.0 : 1.0;
    internal override bool EvaluateAt(SimulationContext ctx, SimTime time) => !operand.EvaluateAt(ctx, time);
    public override SimTime NextChange(SimulationContext ctx) => operand.NextChange(ctx);
}

internal sealed class BoundNegate(BoundExpression operand) : BoundExpression
{
    public override bool Evaluate(SimulationContext ctx) => operand.EvaluateNumeric(ctx) != 0.0;
    public override double EvaluateNumeric(SimulationContext ctx) => -operand.EvaluateNumeric(ctx);
    internal override SimTime Until(SimulationContext ctx) => operand.Until(ctx);
    internal override TimeCurve Curve(SimulationContext ctx) => -operand.Curve(ctx);
    internal override double NumericAt(SimulationContext ctx, SimTime time) => -operand.NumericAt(ctx, time);
    internal override bool EvaluateAt(SimulationContext ctx, SimTime time) => operand.NumericAt(ctx, time) != 0.0;
}

internal sealed class BoundBinary(BoundExpression left, BinaryOperator op, BoundExpression right) : BoundExpression
{
    public override bool Evaluate(SimulationContext ctx) => op switch
    {
        BinaryOperator.And => left.Evaluate(ctx) && right.Evaluate(ctx),
        BinaryOperator.Or => left.Evaluate(ctx) || right.Evaluate(ctx),
        BinaryOperator.Less => left.EvaluateNumeric(ctx) < right.EvaluateNumeric(ctx),
        BinaryOperator.LessEqual => left.EvaluateNumeric(ctx) <= right.EvaluateNumeric(ctx),
        BinaryOperator.Greater => left.EvaluateNumeric(ctx) > right.EvaluateNumeric(ctx),
        BinaryOperator.GreaterEqual => left.EvaluateNumeric(ctx) >= right.EvaluateNumeric(ctx),
        BinaryOperator.Equal => Math.Abs(left.EvaluateNumeric(ctx) - right.EvaluateNumeric(ctx)) < 0.0001,
        BinaryOperator.NotEqual => Math.Abs(left.EvaluateNumeric(ctx) - right.EvaluateNumeric(ctx)) >= 0.0001,
        _ => EvaluateNumeric(ctx) != 0.0,
    };

    public override double EvaluateNumeric(SimulationContext ctx)
    {
        switch (op)
        {
            case BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo:
                double l = left.EvaluateNumeric(ctx);
                double r = right.EvaluateNumeric(ctx);
                return Arithmetic(l, r);
            default:
                return Evaluate(ctx) ? 1.0 : 0.0;
        }
    }

    private bool IsArithmetic => op is BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo;

    private bool IsComparison => op is BinaryOperator.Less or BinaryOperator.LessEqual or BinaryOperator.Greater
        or BinaryOperator.GreaterEqual or BinaryOperator.Equal or BinaryOperator.NotEqual;

    private double Arithmetic(double l, double r) => op switch
    {
        BinaryOperator.Add => l + r,
        BinaryOperator.Subtract => l - r,
        BinaryOperator.Multiply => l * r,
        BinaryOperator.Divide => r == 0.0 ? 0.0 : l / r,
        _ => r == 0.0 ? 0.0 : l % r
    };

    internal override SimTime Until(SimulationContext ctx)
    {
        if (!IsArithmetic) return NextChange(ctx);
        var now = ctx.CurrentTime;
        var until = Crossing.Earlier(left.Until(ctx), right.Until(ctx));
        if (op is not (BinaryOperator.Divide or BinaryOperator.Modulo)) return until;
        var divisor = right.Curve(ctx);
        // A quotient or remainder by a divisor of exactly 0 reads 0; a divisor that moves leaves 0 the next millisecond.
        if (right.EvaluateNumeric(ctx) == 0.0) return divisor.IsConstant ? until : Crossing.Earlier(until, Crossing.Next(now));
        if (op == BinaryOperator.Divide) return until;
        // A remainder wraps where the quotient passes a whole number, and where the divisor passes 0.
        var dividend = left.Curve(ctx);
        double whole = Math.Truncate(left.EvaluateNumeric(ctx) / right.EvaluateNumeric(ctx));
        var roots = new List<double>();
        double span = Crossing.Later(now, until).Milliseconds - (double)now.Milliseconds;
        for (double k = whole - 1; k <= whole + 1; k++) Polynomial.Roots((dividend - TimeCurve.Constant(k) * divisor).Numerator, 0.0, span, roots);
        Polynomial.Roots(divisor.Numerator, 0.0, span, roots);
        foreach (double root in roots)
        {
            until = Crossing.Earlier(until, Crossing.Later(now, SimTime.FromMilliseconds(now.Milliseconds + (long)Math.Floor(root))));
        }
        return until;
    }

    internal override TimeCurve Curve(SimulationContext ctx)
    {
        if (!IsArithmetic) return TimeCurve.Constant(EvaluateNumeric(ctx));
        var l = left.Curve(ctx);
        var r = right.Curve(ctx);
        if (op is BinaryOperator.Divide or BinaryOperator.Modulo && right.EvaluateNumeric(ctx) == 0.0) return TimeCurve.Constant(0.0);
        return op switch
        {
            BinaryOperator.Add => l + r,
            BinaryOperator.Subtract => l - r,
            BinaryOperator.Multiply => l * r,
            BinaryOperator.Divide => l / r,
            // Until the remainder wraps, it is the dividend less a whole number of divisors.
            _ => l - TimeCurve.Constant(Math.Truncate(left.EvaluateNumeric(ctx) / right.EvaluateNumeric(ctx))) * r,
        };
    }

    internal override double NumericAt(SimulationContext ctx, SimTime time) =>
        IsArithmetic ? Arithmetic(left.NumericAt(ctx, time), right.NumericAt(ctx, time)) : EvaluateAt(ctx, time) ? 1.0 : 0.0;

    internal override bool EvaluateAt(SimulationContext ctx, SimTime time) => op switch
    {
        BinaryOperator.And => left.EvaluateAt(ctx, time) && right.EvaluateAt(ctx, time),
        BinaryOperator.Or => left.EvaluateAt(ctx, time) || right.EvaluateAt(ctx, time),
        BinaryOperator.Less => left.NumericAt(ctx, time) < right.NumericAt(ctx, time),
        BinaryOperator.LessEqual => left.NumericAt(ctx, time) <= right.NumericAt(ctx, time),
        BinaryOperator.Greater => left.NumericAt(ctx, time) > right.NumericAt(ctx, time),
        BinaryOperator.GreaterEqual => left.NumericAt(ctx, time) >= right.NumericAt(ctx, time),
        BinaryOperator.Equal => Math.Abs(left.NumericAt(ctx, time) - right.NumericAt(ctx, time)) < 0.0001,
        BinaryOperator.NotEqual => Math.Abs(left.NumericAt(ctx, time) - right.NumericAt(ctx, time)) >= 0.0001,
        _ => NumericAt(ctx, time) != 0.0,
    };

    public override SimTime NextChange(SimulationContext ctx)
    {
        if (op is BinaryOperator.And or BinaryOperator.Or) return Crossing.Earlier(left.NextChange(ctx), right.NextChange(ctx));
        if (!IsComparison) return base.NextChange(ctx);
        // A comparison changes where the difference of its sides passes 0 (or the equality tolerance), where it has a pole,
        // or where a side's curve ends.
        var until = Crossing.Earlier(left.Until(ctx), right.Until(ctx));
        var difference = left.Curve(ctx) - right.Curve(ctx);
        return op is BinaryOperator.Equal or BinaryOperator.NotEqual
            ? Crossing.First(ctx, this, until, difference.NumeratorLess(-0.0001), difference.NumeratorLess(0.0001), difference.Denominator)
            : Crossing.First(ctx, this, until, difference.Numerator, difference.Denominator);
    }
}
