using FellowshipSim.Core.Heroes.Rime;

namespace FellowshipSim.Core.APL.Expressions;

public class ConstantExpression : IAplExpression
{
    public double Value { get; }

    public ConstantExpression(double value) => Value = value;

    public bool Evaluate(Hero hero, SimulationContext ctx) => Math.Abs(Value) > 0.00001;

    public double EvaluateNumeric(Hero hero, SimulationContext ctx) => Value;

    public override string ToString() => Value.ToString();
}

/// <summary>
/// A value read from the hero or the fight, written as a path: a name in <see cref="Named"/>, a resource ("winter_orbs",
/// or "resource.winter_orbs"), or "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" with a scope and property in <see cref="Scopes"/>.
/// </summary>
public class PropertyExpression : IAplExpression
{
    public const string ResourcePrefix = "resource.";
    public const string BuffScope = "buff";
    public const string DebuffScope = "debuff";
    public const string CooldownScope = "cooldown";

    /// <summary>Values named by their whole path.</summary>
    public static IReadOnlyDictionary<string, Func<Hero, SimulationContext, double>> Named { get; } =
        new Dictionary<string, Func<Hero, SimulationContext, double>>
        {
            ["active_enemies"] = (_, ctx) => Math.Max(1, ctx.AllEnemies.Count(e => !e.IsSleeping)),
            ["target.health.pct"] = (_, ctx) => ctx.Player.CurrentTarget?.HealthPercent ?? 100.0,
            ["target.time_to_die"] = (_, _) => 60.0,
            ["fight_remains"] = (_, _) => 100.0,
        };

    /// <summary>Properties of an aura on the hero (buff) or on the current target (debuff); the aura is null when it is not up.</summary>
    private static readonly Dictionary<string, Func<Aura?, SimTime, double>> AuraProperties = new()
    {
        ["up"] = (aura, _) => aura != null ? 1.0 : 0.0,
        ["down"] = (aura, _) => aura == null ? 1.0 : 0.0,
        ["stack"] = (aura, _) => aura?.Stacks ?? 0,
        ["at_max_stacks"] = (aura, _) => aura != null && aura.Stacks >= aura.MaxStacks ? 1.0 : 0.0,
        ["remains"] = (aura, now) => aura == null ? 0.0 : aura.ExpiresAt == SimTime.Infinite ? double.MaxValue : (aura.ExpiresAt - now).TotalSeconds,
    };

    /// <summary>Properties of an ability's cooldown, by the ability's name.</summary>
    private static readonly Dictionary<string, Func<Hero, Ability?, string, SimTime, double>> CooldownProperties = new()
    {
        ["ready"] = (hero, _, name, now) => hero.IsCooldownReady(name, now) ? 1.0 : 0.0,
        ["up"] = (hero, _, name, now) => hero.IsCooldownReady(name, now) ? 1.0 : 0.0,
        ["remains"] = (hero, _, name, now) => hero.GetCooldownRemains(name, now),
        ["charges"] = (hero, ability, name, now) =>
            hero.Cooldowns.TryGetValue(name, out var state) ? state.ChargesAt(now) : ability?.MaxCharges ?? 1,
    };

    /// <summary>The scopes of "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" paths and the properties each one has.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Scopes { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>
        {
            [BuffScope] = AuraProperties.Keys,
            [DebuffScope] = AuraProperties.Keys,
            [CooldownScope] = CooldownProperties.Keys,
        };

    public string PropertyPath { get; }

    /// <summary>The scope of a "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" path, or null.</summary>
    public string? Scope { get; }
    /// <summary>The aura or ability a scoped path names ("glacial_assault").</summary>
    public string? Name { get; }
    /// <summary>The property a scoped path reads ("stack"). A property its scope lacks reads as "up" or "ready".</summary>
    public string? Property { get; }

    public PropertyExpression(string propertyPath)
    {
        PropertyPath = propertyPath.Trim().ToLowerInvariant();
        var parts = PropertyPath.Split('.');
        if (parts.Length >= 3 && Scopes.ContainsKey(parts[0]))
        {
            (Scope, Name, Property) = (parts[0], parts[1], parts[2]);
        }
    }

    /// <summary>The resource name of an unscoped path ("winter_orbs" for "resource.winter_orbs").</summary>
    public string ResourceName => PropertyPath.StartsWith(ResourcePrefix) ? PropertyPath[ResourcePrefix.Length..] : PropertyPath;

    public bool Evaluate(Hero hero, SimulationContext ctx)
    {
        return EvaluateNumeric(hero, ctx) != 0.0;
    }

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        var now = ctx.CurrentTime;

        if (Named.TryGetValue(PropertyPath, out var named)) return named(hero, ctx);

        if (hero.FindResource(ResourceName) is { } resource) return hero.ResourceAmount(resource);

        switch (Scope)
        {
            case BuffScope:
                return AuraProperty(Property!)(AuraLookup.Find(hero.Auras, Name!, now), now);

            case DebuffScope:
                var target = ctx.Player.CurrentTarget;
                return AuraProperty(Property!)(target == null ? null : AuraLookup.Find(target.Auras, Name!, now), now);

            case CooldownScope:
                // Cooldowns are kept under the ability's own name ("Cold Snap"), not its action-list name.
                var ability = AplNames.FindAbility(hero, Name!);
                var cooldown = CooldownProperties.GetValueOrDefault(Property!) ?? CooldownProperties["ready"];
                return cooldown(hero, ability, ability?.Name ?? Name!, now);
        }

        return 0.0;
    }

    private static Func<Aura?, SimTime, double> AuraProperty(string property) =>
        AuraProperties.GetValueOrDefault(property) ?? AuraProperties["up"];

    public override string ToString() => PropertyPath;
}

public class UnaryOpExpression : IAplExpression
{
    public UnaryOperator Operator { get; }
    public IAplExpression Operand { get; }

    public UnaryOpExpression(UnaryOperator op, IAplExpression operand)
    {
        Operator = op;
        Operand = operand;
    }

    public bool Evaluate(Hero hero, SimulationContext ctx)
    {
        return Operator switch
        {
            UnaryOperator.Not => !Operand.Evaluate(hero, ctx),
            UnaryOperator.Negate => Operand.EvaluateNumeric(hero, ctx) != 0.0,
            _ => throw new InvalidOperationException($"Unsupported APL unary operator: '{Operator}'")
        };
    }

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        return Operator switch
        {
            UnaryOperator.Not => Operand.Evaluate(hero, ctx) ? 0.0 : 1.0,
            UnaryOperator.Negate => -Operand.EvaluateNumeric(hero, ctx),
            _ => throw new InvalidOperationException($"Unsupported APL unary operator: '{Operator}'")
        };
    }

    public override string ToString() => $"{Operator.Symbol()}({Operand})";
}

/// <summary>An expression written in parentheses. <see cref="AplFormatter"/> writes the parentheses back.</summary>
public class GroupExpression(IAplExpression inner) : IAplExpression
{
    public IAplExpression Inner { get; } = inner;

    public bool Evaluate(Hero hero, SimulationContext ctx) => Inner.Evaluate(hero, ctx);

    public double EvaluateNumeric(Hero hero, SimulationContext ctx) => Inner.EvaluateNumeric(hero, ctx);

    public override string ToString() => Inner.ToString()!;
}

public class BinaryOpExpression : IAplExpression
{
    public IAplExpression Left { get; init; }
    public IAplExpression Right { get; init; }
    public BinaryOperator Operator { get; init; }

    public BinaryOpExpression(IAplExpression left, BinaryOperator op, IAplExpression right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }

    public bool Evaluate(Hero hero, SimulationContext ctx)
    {
        return Operator switch
        {
            BinaryOperator.And => Left.Evaluate(hero, ctx) && Right.Evaluate(hero, ctx),
            BinaryOperator.Or => Left.Evaluate(hero, ctx) || Right.Evaluate(hero, ctx),
            BinaryOperator.Less => Left.EvaluateNumeric(hero, ctx) < Right.EvaluateNumeric(hero, ctx),
            BinaryOperator.LessEqual => Left.EvaluateNumeric(hero, ctx) <= Right.EvaluateNumeric(hero, ctx),
            BinaryOperator.Greater => Left.EvaluateNumeric(hero, ctx) > Right.EvaluateNumeric(hero, ctx),
            BinaryOperator.GreaterEqual => Left.EvaluateNumeric(hero, ctx) >= Right.EvaluateNumeric(hero, ctx),
            BinaryOperator.Equal => Math.Abs(Left.EvaluateNumeric(hero, ctx) - Right.EvaluateNumeric(hero, ctx)) < 0.0001,
            BinaryOperator.NotEqual => Math.Abs(Left.EvaluateNumeric(hero, ctx) - Right.EvaluateNumeric(hero, ctx)) >= 0.0001,
            BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo =>
                EvaluateNumeric(hero, ctx) != 0.0,
            _ => throw new InvalidOperationException($"Unsupported APL binary operator: '{Operator}'")
        };
    }

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        switch (Operator)
        {
            case BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo:
                double left = Left.EvaluateNumeric(hero, ctx);
                double right = Right.EvaluateNumeric(hero, ctx);
                return Operator switch
                {
                    BinaryOperator.Add => left + right,
                    BinaryOperator.Subtract => left - right,
                    BinaryOperator.Multiply => left * right,
                    BinaryOperator.Divide => right == 0.0 ? 0.0 : left / right,
                    _ => right == 0.0 ? 0.0 : left % right
                };
            default:
                return Evaluate(hero, ctx) ? 1.0 : 0.0;
        }
    }

    public override string ToString() => $"({Left} {Operator.Symbol()} {Right})";
}

internal static class AuraLookup
{
    /// <summary>An active aura whose name, as an APL slug ("glacial_assault"), matches <paramref name="slug"/>.</summary>
    public static Aura? Find(AuraContainer auras, string slug, SimTime now) =>
        auras.Active(now).FirstOrDefault(a => AplNames.Slug(a.Name) == slug || a.Key == slug);
}
