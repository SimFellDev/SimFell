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

    /// <summary>
    /// True when <see cref="EvaluateNumeric"/> holds its value until <see cref="Until"/>: the curve is constant, and no
    /// crossing needs solving (nor the curve building).
    /// </summary>
    internal virtual bool IsLevel(SimulationContext ctx) => true;

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
        if (IsLevel(ctx)) return Crossing.Later(ctx.CurrentTime, Until(ctx));
        var curve = Curve(ctx);
        return Crossing.First(ctx, this, Until(ctx), curve.Numerator, curve.Denominator);
    }

    /// <summary>Binds <paramref name="expression"/> to <paramref name="hero"/>. A variable reads 0.</summary>
    public static BoundExpression Bind(IAplExpression expression, Hero hero) => Bind(expression, hero, null);

    /// <summary>Binds <paramref name="expression"/> to <paramref name="hero"/>, with its variables read from <paramref name="variables"/>.</summary>
    internal static BoundExpression Bind(IAplExpression expression, Hero hero, BoundVariables? variables) => expression switch
    {
        ConstantExpression c => new BoundConstant(c.Value),
        GroupExpression g => Bind(g.Inner, hero, variables),
        UnaryOpExpression { Operator: UnaryOperator.Not } u => new BoundNot(Bind(u.Operand, hero, variables)),
        UnaryOpExpression { Operator: UnaryOperator.Minus } u => new BoundMinus(Bind(u.Operand, hero, variables)),
        BinaryOpExpression b => new BoundBinary(Bind(b.Left, hero, variables), b.Operator, Bind(b.Right, hero, variables)),
        PropertyExpression p => BindProperty(p, hero, variables),
        _ => throw new ArgumentException($"Unsupported APL expression: '{expression}'", nameof(expression)),
    };

    // The same order as PropertyExpression.EvaluateNumeric: a named value, then a resource, then a scoped path; else 0.
    private static BoundExpression BindProperty(PropertyExpression p, Hero hero, BoundVariables? variables)
    {
        if (p.PropertyPath == "time") return new BoundTime();
        if (p.PropertyPath == "target.cast.remains") return new BoundCastRemains();
        if (PropertyExpression.Countdowns.TryGetValue(p.PropertyPath, out var end)) return new BoundCountdown(end);
        if (PropertyExpression.Named.TryGetValue(p.PropertyPath, out var named)) return new BoundNamed(hero, named.Value);
        if (hero.FindResource(p.ResourceName) is { } resource)
        {
            var state = hero.Resources.Get(resource);
            return p.IsDeficit ? new BoundResourceDeficit(state) : new BoundResource(state);
        }
        switch (p.Scope)
        {
            case PropertyExpression.BuffScope:
                return new BoundAura(hero, debuff: false, p.Name!, BoundAura.PropertyOf(p.Property!));
            case PropertyExpression.DebuffScope:
                return new BoundAura(hero, debuff: true, p.Name!, BoundAura.PropertyOf(p.Property!));
            case PropertyExpression.CooldownScope:
                // Cooldowns are kept under the ability's own name ("Cold Snap"), not its action-list name.
                return BoundCooldown.For(hero, AplNames.FindAbility(hero, p.Name!), p.Name!, p.Property!);
            case PropertyExpression.ActionScope:
                var ability = AplNames.FindAbility(hero, p.Name!);
                var read = PropertyExpression.ActionProperty(p.Property!);
                return new BoundNamed(hero, (_, ctx) => read(ability, ctx.CurrentTime));
            case PropertyExpression.ActiveDotScope:
                return new BoundActiveDot(hero, p.Name!);
            case PropertyExpression.VariableScope:
                return variables != null ? new BoundVariable(variables.Slot(p.Name!)) : new BoundConstant(0.0);
            case { } scope when PropertyExpression.BuildScopes.TryGetValue(scope, out var has):
                // The build does not change once the hero is built.
                return new BoundConstant(has.Value(hero, p.Name!) ? 1.0 : 0.0);
            default:
                return new BoundConstant(0.0);
        }
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

/// <summary>The room left in a resource ("spirit.deficit").</summary>
internal sealed class BoundResourceDeficit(ResourceState state) : BoundEventValue
{
    public override double EvaluateNumeric(SimulationContext ctx) => state.Max - state.Current;
}

/// <summary>The seconds since the fight began, rising a second per second.</summary>
internal sealed class BoundTime : BoundNumeric
{
    public override double EvaluateNumeric(SimulationContext ctx) => ctx.CurrentTime.TotalSeconds;
    internal override SimTime Until(SimulationContext ctx) => SimTime.Infinite;
    internal override bool IsLevel(SimulationContext ctx) => false;
    internal override TimeCurve Curve(SimulationContext ctx) => TimeCurve.Line(EvaluateNumeric(ctx), 0.001);
    internal override double NumericAt(SimulationContext ctx, SimTime time) => time.TotalSeconds;
}

/// <summary>
/// The seconds until a time (<see cref="PropertyExpression.Countdowns"/>), falling a second per second; it breaks at that
/// time, where an event starts the next pull or ends the fight. <see cref="PropertyExpression.NeverIn"/> when the time
/// never comes.
/// </summary>
internal sealed class BoundCountdown(Func<SimulationContext, SimTime> end) : BoundNumeric
{
    public override double EvaluateNumeric(SimulationContext ctx) => PropertyExpression.SecondsUntil(ctx, end(ctx));
    internal override SimTime Until(SimulationContext ctx) => end(ctx);
    internal override bool IsLevel(SimulationContext ctx) => end(ctx) == SimTime.Infinite;
    internal override TimeCurve Curve(SimulationContext ctx) =>
        IsLevel(ctx) ? TimeCurve.Constant(PropertyExpression.NeverIn) : TimeCurve.Line(EvaluateNumeric(ctx), -0.001);
    internal override double NumericAt(SimulationContext ctx, SimTime time)
    {
        var at = end(ctx);
        return at == SimTime.Infinite ? PropertyExpression.NeverIn : (at - time).TotalSeconds;
    }
}

/// <summary>
/// The seconds until the current target's cast ends, falling a second per second to 0 at its end, where an event ends the
/// cast; 0 while the target is not casting.
/// </summary>
internal sealed class BoundCastRemains : BoundNumeric
{
    public override double EvaluateNumeric(SimulationContext ctx) => PropertyExpression.CastRemains(ctx, ctx.CurrentTime);
    internal override SimTime Until(SimulationContext ctx) =>
        ctx.Player.CurrentTarget is { IsCasting: true } target ? target.CastEnd : SimTime.Infinite;
    internal override bool IsLevel(SimulationContext ctx) => ctx.Player.CurrentTarget is not { IsCasting: true };
    internal override TimeCurve Curve(SimulationContext ctx) =>
        IsLevel(ctx) ? TimeCurve.Constant(0.0) : TimeCurve.Line(EvaluateNumeric(ctx), -0.001);
    internal override double NumericAt(SimulationContext ctx, SimTime time) => PropertyExpression.CastRemains(ctx, time);
}

/// <summary>The variables of one bound program, by name.</summary>
internal sealed class BoundVariables
{
    private readonly Dictionary<string, BoundVariableSlot> _slots = new();

    public BoundVariableSlot Slot(string name) =>
        _slots.TryGetValue(name, out var slot) ? slot : _slots[name] = new BoundVariableSlot();
}

/// <summary>
/// One variable's value. It belongs to the fight it was set in: in a new fight (another <see cref="SimulationContext"/>)
/// it reads <see cref="Default"/> until it is set.
/// </summary>
internal sealed class BoundVariableSlot
{
    private SimulationContext? _fight;
    private double _value;

    public double Default { get; set; }

    public double Read(SimulationContext ctx) => ctx == _fight ? _value : Default;

    public void Write(SimulationContext ctx, double value) => (_fight, _value) = (ctx, value);
}

/// <summary>A variable, which changes only when a <c>variable</c> action sets it in a decision.</summary>
internal sealed class BoundVariable(BoundVariableSlot slot) : BoundEventValue
{
    public override double EvaluateNumeric(SimulationContext ctx) => slot.Read(ctx);
}

/// <summary>
/// A property of an aura on the hero (buff) or of an effect on the current target (debuff: an aura, or an effect the hero
/// keeps, <see cref="ITargetEffects"/>). It is found by its action-list name, as <see cref="PropertyExpression"/> finds it:
/// several definitions can share a name (copies with another duration or source).
/// </summary>
internal sealed class BoundAura(Hero hero, bool debuff, string slug, BoundAura.AuraProperty property) : BoundNumeric
{
    public enum AuraProperty { Up, Down, Stack, AtMaxStacks, Remains, Refreshable }

    /// <summary>The property named <paramref name="name"/>; a property auras lack reads as "up".</summary>
    public static AuraProperty PropertyOf(string name) => name switch
    {
        "down" => AuraProperty.Down,
        "stack" or "react" => AuraProperty.Stack,
        "refreshable" => AuraProperty.Refreshable,
        "at_max_stacks" => AuraProperty.AtMaxStacks,
        "remains" => AuraProperty.Remains,
        _ => AuraProperty.Up,
    };

    public override double EvaluateNumeric(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        return Read(Find(ctx, now), now);
    }

    private TargetEffect? Find(SimulationContext ctx, SimTime now)
    {
        if (!debuff) return hero.Auras.FindActive(slug, now) is { } aura ? TargetEffect.Of(aura) : null;
        var target = ctx.Player.CurrentTarget;
        return target == null ? null : AuraLookup.On(hero, target, slug, now);
    }

    private double Read(TargetEffect? aura, SimTime now) => property switch
    {
        AuraProperty.Up => aura != null ? 1.0 : 0.0,
        AuraProperty.Down => aura == null ? 1.0 : 0.0,
        AuraProperty.Stack => aura?.Stacks ?? 0,
        AuraProperty.AtMaxStacks => aura is { } a && a.Stacks >= a.MaxStacks ? 1.0 : 0.0,
        AuraProperty.Refreshable => PropertyExpression.IsRefreshable(aura, now) ? 1.0 : 0.0,
        _ => aura is not { } r ? 0.0 : r.ExpiresAt == SimTime.Infinite ? double.MaxValue : (r.ExpiresAt - now).TotalSeconds,
    };

    // The aura found now runs out at its expiry; another aura of the name appears only in an event. A refreshable read
    // also changes at the first millisecond the aura is in its refresh window.
    internal override SimTime Until(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        if (Find(ctx, now) is not { } aura) return SimTime.Infinite;
        if (property == AuraProperty.Refreshable && aura.ExpiresAt != SimTime.Infinite)
        {
            double window = aura.ExpiresAt.Milliseconds - PropertyExpression.RefreshableFraction * aura.Duration.Milliseconds;
            var refreshable = SimTime.FromMilliseconds((long)Math.Floor(window) + 1);
            if (refreshable > now) return refreshable;
        }
        return aura.ExpiresAt;
    }

    internal override bool IsLevel(SimulationContext ctx) =>
        property != AuraProperty.Remains || Find(ctx, ctx.CurrentTime) is not { } aura || aura.ExpiresAt == SimTime.Infinite;

    internal override TimeCurve Curve(SimulationContext ctx) =>
        property == AuraProperty.Remains && Find(ctx, ctx.CurrentTime) is { } aura && aura.ExpiresAt != SimTime.Infinite
            ? TimeCurve.Line(EvaluateNumeric(ctx), -0.001)
            : TimeCurve.Constant(EvaluateNumeric(ctx));

    internal override double NumericAt(SimulationContext ctx, SimTime time)
    {
        var aura = Find(ctx, ctx.CurrentTime);
        return Read(aura is { } a && time >= a.ExpiresAt ? null : aura, time);
    }
}

/// <summary>
/// The enemies in combat with a debuff (<c>active_dot.&lt;name&gt;</c>): an aura on them, or an effect the hero keeps on
/// them (<see cref="ITargetEffects"/>). It falls when one runs out, and rises only in an event.
/// </summary>
internal sealed class BoundActiveDot(Hero hero, string slug) : BoundNumeric
{
    public override double EvaluateNumeric(SimulationContext ctx) => NumericAt(ctx, ctx.CurrentTime);

    internal override SimTime Until(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        var first = SimTime.Infinite;
        foreach (var enemy in ctx.ActiveEnemies)
        {
            if (AuraLookup.On(hero, enemy, slug, now) is { } effect && effect.ExpiresAt < first) first = effect.ExpiresAt;
        }
        return first;
    }

    internal override double NumericAt(SimulationContext ctx, SimTime time)
    {
        var now = ctx.CurrentTime;
        int count = 0;
        foreach (var enemy in ctx.ActiveEnemies)
        {
            if (AuraLookup.On(hero, enemy, slug, now) is { } effect && time < effect.ExpiresAt) count++;
        }
        return count;
    }
}

/// <summary>
/// A property of an ability's cooldown. The cooldown state is looked up again only when the hero's cooldown states change
/// (<see cref="Hero.CooldownsVersion"/>): a new fight clears them, and a cooldown's first use adds one.
/// </summary>
internal sealed class BoundCooldown : BoundNumeric
{
    private enum CooldownProperty { Ready, Remains, Charges, ChargesFractional, Duration, FullRechargeTime }

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
            "charges_fractional" => CooldownProperty.ChargesFractional,
            "duration" => CooldownProperty.Duration,
            "full_recharge_time" => CooldownProperty.FullRechargeTime,
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

    // A remaining time falls a second per second while the charge it waits for recharges; the fractional charges rise by
    // the part of a cooldown that passes.
    private bool Falls(SimulationContext ctx) => State() is { } state && state.NextChargeAt > ctx.CurrentTime && _property switch
    {
        CooldownProperty.Remains => state.ChargesAt(ctx.CurrentTime) == 0,
        CooldownProperty.FullRechargeTime => state.ChargesAt(ctx.CurrentTime) < state.MaxCharges,
        _ => false,
    };

    private bool Rises(SimulationContext ctx) => _property == CooldownProperty.ChargesFractional && State() is { } state
        && state.RechargePerSecond(ctx.CurrentTime) > 0.0;

    internal override bool IsLevel(SimulationContext ctx) => !Falls(ctx) && !Rises(ctx);

    internal override TimeCurve Curve(SimulationContext ctx) =>
        Falls(ctx) ? TimeCurve.Line(EvaluateNumeric(ctx), -0.001)
        : Rises(ctx) ? TimeCurve.Line(EvaluateNumeric(ctx), 0.001 * State()!.RechargePerSecond(ctx.CurrentTime))
        : TimeCurve.Constant(EvaluateNumeric(ctx));

    internal override double NumericAt(SimulationContext ctx, SimTime time)
    {
        var state = State();
        var now = ctx.CurrentTime;
        return _property switch
        {
            CooldownProperty.Ready => state == null || state.ChargesAt(now) > 0 ? 1.0 : 0.0,
            CooldownProperty.Remains => state != null && state.ChargesAt(now) == 0 ? Math.Max(0.0, (state.NextChargeAt - time).TotalSeconds) : 0.0,
            CooldownProperty.Duration => _ability?.Cooldown.TotalSeconds ?? 0.0,
            CooldownProperty.ChargesFractional => state != null ? state.ChargesAt(now) + state.RechargedPart(now, time) : _ability?.MaxCharges ?? 1,
            CooldownProperty.FullRechargeTime => state != null && state.ChargesAt(now) < state.MaxCharges
                ? Math.Max(0.0, (state.NextChargeAt - time).TotalSeconds) + (state.MaxCharges - state.ChargesAt(now) - 1) * (_ability?.Cooldown.TotalSeconds ?? 0.0)
                : 0.0,
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

internal sealed class BoundMinus(BoundExpression operand) : BoundExpression
{
    public override bool Evaluate(SimulationContext ctx) => operand.EvaluateNumeric(ctx) != 0.0;
    public override double EvaluateNumeric(SimulationContext ctx) => -operand.EvaluateNumeric(ctx);
    internal override SimTime Until(SimulationContext ctx) => operand.Until(ctx);
    internal override TimeCurve Curve(SimulationContext ctx) => -operand.Curve(ctx);
    internal override bool IsLevel(SimulationContext ctx) => operand.IsLevel(ctx);
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

    internal override bool IsLevel(SimulationContext ctx) => !IsArithmetic || (left.IsLevel(ctx) && right.IsLevel(ctx));

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
        if (left.IsLevel(ctx) && right.IsLevel(ctx)) return Crossing.Later(ctx.CurrentTime, until);
        var difference = left.Curve(ctx) - right.Curve(ctx);
        return op is BinaryOperator.Equal or BinaryOperator.NotEqual
            ? Crossing.First(ctx, this, until, difference.NumeratorLess(-0.0001), difference.NumeratorLess(0.0001), difference.Denominator)
            : Crossing.First(ctx, this, until, difference.Numerator, difference.Denominator);
    }
}
