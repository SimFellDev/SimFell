namespace FellowshipSim.Core.APL.Expressions;

/// <summary>
/// An expression bound to one hero (<see cref="BoundExpression.Bind"/>): every name is resolved to the ability, resource
/// state, cooldown or aura name it reads, so an evaluation does no string or dictionary work. It gives the same values as
/// the <see cref="IAplExpression"/> it was bound from.
/// </summary>
/// <remarks>
/// Between events a value is linear in time: resources, enemies and health change only in events, an aura read changes
/// when the aura runs out, a cooldown read when a charge returns, and a remaining time falls by a second per second.
/// <see cref="Until"/>, <see cref="Slope"/> and <see cref="NumericAt"/> describe that line, and <see cref="NextChange"/>
/// solves the time the expression's truth changes on it.
/// </remarks>
public abstract class BoundExpression
{
    public abstract bool Evaluate(SimulationContext ctx);
    public abstract double EvaluateNumeric(SimulationContext ctx);

    /// <summary>
    /// The end of the current line of <see cref="EvaluateNumeric"/> with no event before it: the next time an aura it reads
    /// runs out or a charge returns. <see cref="SimTime.Infinite"/> when only an event changes it.
    /// </summary>
    internal abstract SimTime Until(SimulationContext ctx);

    /// <summary>Change of <see cref="EvaluateNumeric"/> per millisecond until <see cref="Until"/>; NaN when it is not linear.</summary>
    internal virtual double Slope(SimulationContext ctx) => 0.0;

    /// <summary><see cref="EvaluateNumeric"/> at <paramref name="time"/>, from the current time to <see cref="Until"/>, with no event before it.</summary>
    internal abstract double NumericAt(SimulationContext ctx, SimTime time);

    /// <summary><see cref="Evaluate"/> at <paramref name="time"/>, from the current time to <see cref="Until"/>, with no event before it.</summary>
    internal virtual bool EvaluateAt(SimulationContext ctx, SimTime time) => NumericAt(ctx, time) != 0.0;

    /// <summary>
    /// The earliest time after the current time at which <see cref="Evaluate"/> can give another value with no event before
    /// it, or <see cref="SimTime.Infinite"/>. A value read as a truth changes when it crosses 0.
    /// </summary>
    public virtual SimTime NextChange(SimulationContext ctx) =>
        Crossing.First(ctx, this, Until(ctx), EvaluateNumeric(ctx), Slope(ctx), 0.0);

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

/// <summary>The first time a truth on a line of values changes.</summary>
internal static class Crossing
{
    /// <summary>
    /// The first whole millisecond after the current time and before <paramref name="until"/> at which
    /// <paramref name="expression"/>'s truth differs from now, where the truth depends on a value that starts at
    /// <paramref name="value"/> and changes by <paramref name="slope"/> per millisecond and changes truth only where the
    /// value passes one of <paramref name="thresholds"/>. Else <paramref name="until"/>: the line ends there.
    /// </summary>
    public static SimTime First(SimulationContext ctx, BoundExpression expression, SimTime until, double value, double slope,
        params ReadOnlySpan<double> thresholds)
    {
        var now = ctx.CurrentTime;
        var limit = Later(now, until);
        // A value that is not linear in time can change truth at any millisecond.
        if (double.IsNaN(slope)) return Earlier(limit, Next(now));
        if (slope == 0.0) return limit;
        bool current = expression.Evaluate(ctx);
        var first = limit;
        foreach (double threshold in thresholds)
        {
            double at = now.Milliseconds + (threshold - value) / slope;
            if (!(at > now.Milliseconds - 1) || at - 2 >= first.Milliseconds) continue;
            // The line crosses the threshold at `at`; rounding moves the change of truth by a millisecond or two.
            for (long ms = Math.Max(now.Milliseconds + 1, (long)Math.Floor(at) - 2); ms < first.Milliseconds && ms <= (long)Math.Ceiling(at) + 2; ms++)
            {
                if (expression.EvaluateAt(ctx, SimTime.FromMilliseconds(ms)) == current) continue;
                first = SimTime.FromMilliseconds(ms);
                break;
            }
        }
        return first;
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

    internal override double Slope(SimulationContext ctx) =>
        property == AuraProperty.Remains && Find(ctx, ctx.CurrentTime) is { } aura && aura.ExpiresAt != SimTime.Infinite ? -0.001 : 0.0;

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

    internal override double Slope(SimulationContext ctx) =>
        _property == CooldownProperty.Remains && State() is { } state && state.ChargesAt(ctx.CurrentTime) == 0 ? -0.001 : 0.0;

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
    internal override double Slope(SimulationContext ctx) => -operand.Slope(ctx);
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

    internal override SimTime Until(SimulationContext ctx) =>
        IsArithmetic ? Crossing.Earlier(left.Until(ctx), right.Until(ctx)) : NextChange(ctx);

    internal override double Slope(SimulationContext ctx)
    {
        if (!IsArithmetic) return 0.0;
        double sl = left.Slope(ctx), sr = right.Slope(ctx);
        if (sl == 0.0 && sr == 0.0) return 0.0;
        return op switch
        {
            BinaryOperator.Add => sl + sr,
            BinaryOperator.Subtract => sl - sr,
            BinaryOperator.Multiply when sl == 0.0 => left.EvaluateNumeric(ctx) * sr,
            BinaryOperator.Multiply when sr == 0.0 => right.EvaluateNumeric(ctx) * sl,
            BinaryOperator.Divide when sr == 0.0 && right.EvaluateNumeric(ctx) != 0.0 => sl / right.EvaluateNumeric(ctx),
            _ => double.NaN,
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
        // A comparison changes where the difference of its sides passes 0 (or the equality tolerance), or where a side's
        // line ends.
        var until = Crossing.Earlier(left.Until(ctx), right.Until(ctx));
        double difference = left.EvaluateNumeric(ctx) - right.EvaluateNumeric(ctx);
        double slope = left.Slope(ctx) - right.Slope(ctx);
        return op is BinaryOperator.Equal or BinaryOperator.NotEqual
            ? Crossing.First(ctx, this, until, difference, slope, -0.0001, 0.0001)
            : Crossing.First(ctx, this, until, difference, slope, 0.0);
    }
}
