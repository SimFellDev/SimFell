namespace FellowshipSim.Core.APL.Expressions;

/// <summary>
/// An expression bound to one hero (<see cref="BoundExpression.Bind"/>): every name is resolved to the ability, resource
/// state, cooldown or aura name it reads, so an evaluation does no string or dictionary work. It gives the same values as
/// the <see cref="IAplExpression"/> it was bound from.
/// </summary>
public abstract class BoundExpression
{
    public abstract bool Evaluate(SimulationContext ctx);
    public abstract double EvaluateNumeric(SimulationContext ctx);

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

/// <summary>A value read as a number; it holds when it is not 0.</summary>
internal abstract class BoundNumeric : BoundExpression
{
    public sealed override bool Evaluate(SimulationContext ctx) => EvaluateNumeric(ctx) != 0.0;
}

internal sealed class BoundConstant(double value) : BoundExpression
{
    public override bool Evaluate(SimulationContext ctx) => Math.Abs(value) > 0.00001;
    public override double EvaluateNumeric(SimulationContext ctx) => value;
}

internal sealed class BoundNamed(Hero hero, Func<Hero, SimulationContext, double> read) : BoundNumeric
{
    public override double EvaluateNumeric(SimulationContext ctx) => read(hero, ctx);
}

internal sealed class BoundResource(ResourceState state) : BoundNumeric
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
        Aura? aura;
        if (debuff)
        {
            var target = ctx.Player.CurrentTarget;
            aura = target == null ? null : target.Auras.FindActive(slug, now);
        }
        else
        {
            aura = hero.Auras.FindActive(slug, now);
        }
        return property switch
        {
            AuraProperty.Up => aura != null ? 1.0 : 0.0,
            AuraProperty.Down => aura == null ? 1.0 : 0.0,
            AuraProperty.Stack => aura?.Stacks ?? 0,
            AuraProperty.AtMaxStacks => aura != null && aura.Stacks >= aura.MaxStacks ? 1.0 : 0.0,
            _ => aura == null ? 0.0 : aura.ExpiresAt == SimTime.Infinite ? double.MaxValue : (aura.ExpiresAt - now).TotalSeconds,
        };
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

    public override double EvaluateNumeric(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        if (_version != _hero.CooldownsVersion)
        {
            _hero.Cooldowns.TryGetValue(_name, out _state);
            _version = _hero.CooldownsVersion;
        }
        var state = _state;
        return _property switch
        {
            CooldownProperty.Ready => state == null || state.ChargesAt(now) > 0 ? 1.0 : 0.0,
            CooldownProperty.Remains => state != null && state.ChargesAt(now) == 0 ? Math.Max(0.0, (state.NextChargeAt - now).TotalSeconds) : 0.0,
            _ => state != null ? state.ChargesAt(now) : _ability?.MaxCharges ?? 1,
        };
    }
}

internal sealed class BoundNot(BoundExpression operand) : BoundExpression
{
    public override bool Evaluate(SimulationContext ctx) => !operand.Evaluate(ctx);
    public override double EvaluateNumeric(SimulationContext ctx) => operand.Evaluate(ctx) ? 0.0 : 1.0;
}

internal sealed class BoundNegate(BoundExpression operand) : BoundExpression
{
    public override bool Evaluate(SimulationContext ctx) => operand.EvaluateNumeric(ctx) != 0.0;
    public override double EvaluateNumeric(SimulationContext ctx) => -operand.EvaluateNumeric(ctx);
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
                return op switch
                {
                    BinaryOperator.Add => l + r,
                    BinaryOperator.Subtract => l - r,
                    BinaryOperator.Multiply => l * r,
                    BinaryOperator.Divide => r == 0.0 ? 0.0 : l / r,
                    _ => r == 0.0 ? 0.0 : l % r
                };
            default:
                return Evaluate(ctx) ? 1.0 : 0.0;
        }
    }
}
