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

public class PropertyExpression : IAplExpression
{
    public string PropertyPath { get; }

    public PropertyExpression(string propertyPath)
    {
        PropertyPath = propertyPath.Trim().ToLowerInvariant();
    }

    public bool Evaluate(Hero hero, SimulationContext ctx)
    {
        return EvaluateNumeric(hero, ctx) != 0.0;
    }

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        var now = ctx.CurrentTime;

        // Direct property lookups
        switch (PropertyPath)
        {
            case "active_enemies":
                int count = ctx.AllEnemies.Count(e => !e.IsSleeping);
                return count > 0 ? count : 1;

            case "target.health.pct":
                return ctx.Player.CurrentTarget?.HealthPercent ?? 100.0;

            case "target.time_to_die":
                return 60.0;

            case "fight_remains":
                return 100.0;
        }

        // A resource by name (e.g. "winter_orbs", "anima", "spirit"), optionally written "resource.<name>".
        string resourceName = PropertyPath.StartsWith("resource.") ? PropertyPath["resource.".Length..] : PropertyPath;
        if (hero.FindResource(resourceName) is { } resource) return hero.ResourceAmount(resource);

        // buff.<name>.<property>
        if (PropertyPath.StartsWith("buff."))
        {
            var parts = PropertyPath.Split('.');
            if (parts.Length >= 3)
            {
                string buffName = parts[1];
                string prop = parts[2];

                var aura = AuraLookup.Find(hero.Auras, buffName, now);
                int stacks = aura?.Stacks ?? 0;
                return prop switch
                {
                    "up" => aura != null ? 1.0 : 0.0,
                    "down" => aura == null ? 1.0 : 0.0,
                    "stack" => stacks,
                    "at_max_stacks" => aura != null && stacks >= aura.MaxStacks ? 1.0 : 0.0,
                    "remains" => aura == null ? 0.0 : aura.ExpiresAt == SimTime.Infinite ? double.MaxValue : (aura.ExpiresAt - now).TotalSeconds,
                    _ => aura != null ? 1.0 : 0.0
                };
            }
        }

        // debuff.<name>.<property>
        if (PropertyPath.StartsWith("debuff."))
        {
            var parts = PropertyPath.Split('.');
            if (parts.Length >= 3)
            {
                string debuffName = parts[1];
                string prop = parts[2];
                var target = ctx.Player.CurrentTarget;
                var aura = target == null ? null : AuraLookup.Find(target.Auras, debuffName, now);
                return prop switch
                {
                    "up" => aura != null ? 1.0 : 0.0,
                    "down" => aura == null ? 1.0 : 0.0,
                    "stack" => aura?.Stacks ?? 0,
                    _ => aura != null ? 1.0 : 0.0
                };
            }
        }

        // cooldown.<name>.<property>
        if (PropertyPath.StartsWith("cooldown."))
        {
            var parts = PropertyPath.Split('.');
            if (parts.Length >= 3)
            {
                string abilityName = parts[1];
                string prop = parts[2];

                return prop switch
                {
                    "ready" or "up" => hero.IsCooldownReady(abilityName, now) ? 1.0 : 0.0,
                    "remains" => hero.GetCooldownRemains(abilityName, now),
                    "charges" => hero.IsCooldownReady(abilityName, now) ? 1.0 : 0.0,
                    _ => hero.IsCooldownReady(abilityName, now) ? 1.0 : 0.0
                };
            }
        }

        return 0.0;
    }

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
            _ => throw new InvalidOperationException($"Unsupported APL binary operator: '{Operator}'")
        };
    }

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        return Evaluate(hero, ctx) ? 1.0 : 0.0;
    }

    public override string ToString() => $"({Left} {Operator.Symbol()} {Right})";
}

internal static class AuraLookup
{
    /// <summary>An active aura whose name, as an APL slug ("glacial_assault"), matches <paramref name="slug"/>.</summary>
    public static Aura? Find(AuraContainer auras, string slug, SimTime now) =>
        auras.Active(now).FirstOrDefault(a => Slug(a.Name) == slug || a.Key == slug);

    private static string Slug(string name) =>
        new string(name.ToLowerInvariant().Replace(' ', '_').Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
}
