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
/// or "resource.winter_orbs") or the room left in one ("spirit.deficit"), "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" with
/// a scope and property in <see cref="Scopes"/> ("dot" is "debuff"), "&lt;scope&gt;.&lt;name&gt;" with a scope in
/// <see cref="BuildScopes"/> (1 when the build has it), "active_dot.&lt;name&gt;" (the enemies in combat with that debuff),
/// or "variable.&lt;name&gt;".
/// </summary>
public class PropertyExpression : IAplExpression
{
    public const string ResourcePrefix = "resource.";
    public const string DeficitSuffix = ".deficit";
    public const string BuffScope = "buff";
    public const string DebuffScope = "debuff";
    /// <summary>SimulationCraft's name for a damage over time effect: the same as <see cref="DebuffScope"/>.</summary>
    public const string DotScope = "dot";
    public const string ActiveDotScope = "active_dot";
    public const string CooldownScope = "cooldown";
    public const string ActionScope = "action";
    public const string TalentScope = "talent";
    public const string BlessingScope = "blessing";
    public const string LegendaryScope = "legendary";
    public const string WeaponScope = "weapon";
    public const string WeaponTraitScope = "weapon_trait";
    public const string VariableScope = "variable";

    /// <summary>SimulationCraft's pandemic window: an aura is refreshable in the last 30% of its duration.</summary>
    public const double RefreshableFraction = 0.3;

    /// <summary>The sim's fights have no raid events: none exist, and the next one never comes.</summary>
    public const double NoRaidEventIn = 10_000.0;

    /// <summary>Values named by their whole path.</summary>
    public static IReadOnlyDictionary<string, Func<Hero, SimulationContext, double>> Named { get; } =
        new Dictionary<string, Func<Hero, SimulationContext, double>>
        {
            ["active_enemies"] = (_, ctx) => Math.Max(1, ctx.ActiveEnemies.Count),
            ["target.health.pct"] = (_, ctx) => ctx.Player.CurrentTarget?.HealthPercent ?? 100.0,
            ["target.health"] = (_, ctx) => ctx.Player.CurrentTarget?.CurrentHealth ?? 0.0,
            ["target.time_to_die"] = (_, _) => 60.0,
            ["fight_remains"] = (_, _) => 100.0,
            ["time"] = (_, ctx) => ctx.CurrentTime.TotalSeconds,
            ["gcd"] = (hero, ctx) => Gcd(hero, ctx.CurrentTime),
            ["gcd.max"] = (hero, ctx) => Gcd(hero, ctx.CurrentTime),
            ["raid_event.adds.exists"] = (_, _) => 0.0,
            ["raid_event.adds.count"] = (_, _) => 0.0,
            ["raid_event.adds.in"] = (_, _) => NoRaidEventIn,
        };

    /// <summary>The hasted global cooldown, as <see cref="SimulationContext.BeginCast"/> sets it.</summary>
    public static double Gcd(Hero hero, SimTime now) => hero.GlobalCooldownSeconds / (1.0 + hero.GetHaste(now));

    /// <summary>Properties of an aura on the hero (buff) or an effect on the current target (debuff); null when it is not up.</summary>
    private static readonly Dictionary<string, Func<TargetEffect?, SimTime, double>> AuraProperties = new()
    {
        ["up"] = (aura, _) => aura != null ? 1.0 : 0.0,
        // SimulationCraft's word for a damage over time effect that is up.
        ["ticking"] = (aura, _) => aura != null ? 1.0 : 0.0,
        ["down"] = (aura, _) => aura == null ? 1.0 : 0.0,
        ["stack"] = (aura, _) => aura?.Stacks ?? 0,
        // SimulationCraft's react is the stacks the player has seen; the sim reacts at once.
        ["react"] = (aura, _) => aura?.Stacks ?? 0,
        ["at_max_stacks"] = (aura, _) => aura is { } a && a.Stacks >= a.MaxStacks ? 1.0 : 0.0,
        ["remains"] = (aura, now) => aura is not { } a ? 0.0 : a.ExpiresAt == SimTime.Infinite ? double.MaxValue : (a.ExpiresAt - now).TotalSeconds,
        ["refreshable"] = (aura, now) => IsRefreshable(aura, now) ? 1.0 : 0.0,
    };

    /// <summary>True when <paramref name="aura"/> is down or in the last <see cref="RefreshableFraction"/> of its duration.</summary>
    public static bool IsRefreshable(TargetEffect? aura, SimTime now) =>
        aura is not { } a || (a.ExpiresAt != SimTime.Infinite && (a.ExpiresAt - now).TotalSeconds < RefreshableFraction * a.Duration.TotalSeconds);

    /// <summary>Properties of an ability's cooldown, by the ability's name.</summary>
    private static readonly Dictionary<string, Func<Hero, Ability?, string, SimTime, double>> CooldownProperties = new()
    {
        ["ready"] = (hero, _, name, now) => hero.IsCooldownReady(name, now) ? 1.0 : 0.0,
        ["up"] = (hero, _, name, now) => hero.IsCooldownReady(name, now) ? 1.0 : 0.0,
        ["remains"] = (hero, _, name, now) => hero.GetCooldownRemains(name, now),
        ["charges"] = (hero, ability, name, now) =>
            hero.Cooldowns.TryGetValue(name, out var state) ? state.ChargesAt(now) : ability?.MaxCharges ?? 1,
        ["duration"] = (_, ability, _, _) => ability?.Cooldown.TotalSeconds ?? 0.0,
        ["full_recharge_time"] = (hero, ability, name, now) =>
            hero.Cooldowns.TryGetValue(name, out var state) ? FullRechargeTime(state, ability, now) : 0.0,
        ["charges_fractional"] = (hero, ability, name, now) =>
            // The charges held and the part of the next one recharged so far (2.25: two, and a quarter of the third).
            hero.Cooldowns.TryGetValue(name, out var state) ? state.ChargesAt(now) + state.RechargedPart(now, now) : ability?.MaxCharges ?? 1,
    };

    /// <summary>
    /// Seconds until every charge is back: the charge recharging now, then a full cooldown for each other missing charge.
    /// </summary>
    public static double FullRechargeTime(CooldownState state, Ability? ability, SimTime now)
    {
        int missing = state.MaxCharges - state.ChargesAt(now);
        if (missing <= 0) return 0.0;
        return Math.Max(0.0, (state.NextChargeAt - now).TotalSeconds) + (missing - 1) * (ability?.Cooldown.TotalSeconds ?? 0.0);
    }

    /// <summary>Properties of an action's ability, by the ability's name.</summary>
    private static readonly Dictionary<string, Func<Ability?, SimTime, double>> ActionProperties = new()
    {
        // An instant cast takes the global cooldown.
        ["execute_time"] = (ability, now) => ability == null ? 0.0
            : ability.IsInstant ? Gcd(ability.Owner, now) : ability.GetAdjustedCastTime(now).TotalSeconds,
    };

    /// <summary>The scopes of "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" paths and the properties each one has.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Scopes { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>
        {
            [BuffScope] = AuraProperties.Keys,
            [DebuffScope] = AuraProperties.Keys,
            [DotScope] = AuraProperties.Keys,
            [CooldownScope] = CooldownProperties.Keys,
            [ActionScope] = ActionProperties.Keys,
        };

    /// <summary>
    /// The scopes of "&lt;scope&gt;.&lt;name&gt;" paths that read the hero's build: 1 when the hero has a talent, blessing,
    /// legendary or weapon trait of that name, or a weapon (or weapon ability) whose name starts with it. They do not change
    /// during a fight.
    /// </summary>
    public static IReadOnlyDictionary<string, Func<Hero, string, bool>> BuildScopes { get; } =
        new Dictionary<string, Func<Hero, string, bool>>
        {
            [TalentScope] = (hero, name) => hero.Talents.Any(t => AplNames.Slug(t.Name) == name),
            [BlessingScope] = (hero, name) => hero.Glyphs.Any(b => AplNames.Slug(b.Name) == name),
            [LegendaryScope] = (hero, name) => hero.Legendaries.Any(l => AplNames.Slug(l.Name) == name),
            [WeaponTraitScope] = (hero, name) => hero.Augmentations.Any(t => AplNames.Slug(t.Name) == name),
            [WeaponScope] = (hero, name) =>
                (hero.EquippedWeapon != null && AplNames.Slug(hero.EquippedWeapon.Name).StartsWith(name, StringComparison.Ordinal))
                || (hero.WeaponAbility != null && AplNames.Slug(hero.WeaponAbility.Name).StartsWith(name, StringComparison.Ordinal)),
        };

    public string PropertyPath { get; }

    /// <summary>
    /// The scope of a "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" path, of a "&lt;scope&gt;.&lt;name&gt;" path in
    /// <see cref="BuildScopes"/>, or <see cref="VariableScope"/>; else null.
    /// </summary>
    public string? Scope { get; }
    /// <summary>The aura, ability, build part or variable a scoped path names ("glacial_assault").</summary>
    public string? Name { get; }
    /// <summary>The property a scoped path reads ("stack"); null for a build part or a variable. A property its scope lacks reads as "up" or "ready".</summary>
    public string? Property { get; }

    public PropertyExpression(string propertyPath)
    {
        PropertyPath = propertyPath.Trim().ToLowerInvariant();
        var parts = PropertyPath.Split('.');
        if (parts.Length >= 3 && parts[0] == DotScope)
        {
            (Scope, Name, Property) = (DebuffScope, parts[1], parts[2]);
        }
        else if (parts.Length >= 3 && Scopes.ContainsKey(parts[0]))
        {
            (Scope, Name, Property) = (parts[0], parts[1], parts[2]);
        }
        else if (parts.Length == 2 && (BuildScopes.ContainsKey(parts[0]) || parts[0] is VariableScope or ActiveDotScope))
        {
            (Scope, Name) = (parts[0], parts[1]);
        }
    }

    /// <summary>The resource name of an unscoped path ("winter_orbs" for "resource.winter_orbs" and "winter_orbs.deficit").</summary>
    public string ResourceName
    {
        get
        {
            string path = PropertyPath.StartsWith(ResourcePrefix) ? PropertyPath[ResourcePrefix.Length..] : PropertyPath;
            return IsDeficit ? path[..^DeficitSuffix.Length] : path;
        }
    }

    /// <summary>True for a path that reads the room left in a resource ("spirit.deficit").</summary>
    public bool IsDeficit => PropertyPath.EndsWith(DeficitSuffix, StringComparison.Ordinal);

    public bool Evaluate(Hero hero, SimulationContext ctx)
    {
        return EvaluateNumeric(hero, ctx) != 0.0;
    }

    /// <remarks>A variable reads 0 here: only a bound program (<see cref="AplProgram"/>) runs variable actions.</remarks>
    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        var now = ctx.CurrentTime;

        if (Named.TryGetValue(PropertyPath, out var named)) return named(hero, ctx);

        if (hero.FindResource(ResourceName) is { } resource)
        {
            var state = hero.Resources.Get(resource);
            return IsDeficit ? state.Max - state.Current : state.Current;
        }

        switch (Scope)
        {
            case BuffScope:
                return AuraProperty(Property!)(AuraLookup.Find(hero.Auras, Name!, now) is { } aura ? TargetEffect.Of(aura) : null, now);

            case DebuffScope:
                var target = ctx.Player.CurrentTarget;
                return AuraProperty(Property!)(target == null ? null : AuraLookup.On(hero, target, Name!, now), now);

            case ActiveDotScope:
                return ctx.ActiveEnemies.Count(enemy => AuraLookup.On(hero, enemy, Name!, now) != null);

            case CooldownScope:
                // Cooldowns are kept under the ability's own name ("Cold Snap"), not its action-list name.
                var ability = AplNames.FindAbility(hero, Name!);
                var cooldown = CooldownProperties.GetValueOrDefault(Property!) ?? CooldownProperties["ready"];
                return cooldown(hero, ability, ability?.Name ?? Name!, now);

            case ActionScope:
                return ActionProperty(Property!)(AplNames.FindAbility(hero, Name!), now);

            case { } scope when BuildScopes.TryGetValue(scope, out var has):
                return has(hero, Name!) ? 1.0 : 0.0;
        }

        return 0.0;
    }

    /// <summary>The value of an action property ("execute_time"); a property actions lack reads as 0.</summary>
    public static Func<Ability?, SimTime, double> ActionProperty(string property) =>
        ActionProperties.GetValueOrDefault(property) ?? ((_, _) => 0.0);

    /// <summary>The value of a cooldown property; a property cooldowns lack reads as "ready".</summary>
    public static Func<Hero, Ability?, string, SimTime, double> CooldownProperty(string property) =>
        CooldownProperties.GetValueOrDefault(property) ?? CooldownProperties["ready"];

    /// <summary>
    /// Paths an action reads about its own ability, unscoped ("refreshable", "full_recharge_time"), as the scoped path
    /// they stand for on the action's ability <paramref name="ability"/>; null for any other path.
    /// </summary>
    public static string? OfAction(string path, string ability) =>
        ActionValues.TryGetValue(path, out var scoped) ? scoped(ability) : null;

    /// <summary>Paths an action reads about its own ability, with the scoped path each stands for on an ability.</summary>
    public static IReadOnlyDictionary<string, Func<string, string>> ActionValues { get; } = new Dictionary<string, Func<string, string>>
    {
        ["refreshable"] = ability => $"{DebuffScope}.{ability}.refreshable",
        ["ticking"] = ability => $"{DebuffScope}.{ability}.up",
        ["full_recharge_time"] = ability => $"{CooldownScope}.{ability}.full_recharge_time",
        ["charges"] = ability => $"{CooldownScope}.{ability}.charges",
        ["charges_fractional"] = ability => $"{CooldownScope}.{ability}.charges_fractional",
        ["execute_time"] = ability => $"{ActionScope}.{ability}.execute_time",
        ["cast_time"] = ability => $"{ActionScope}.{ability}.execute_time",
    };

    private static Func<TargetEffect?, SimTime, double> AuraProperty(string property) =>
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
            UnaryOperator.Minus => Operand.EvaluateNumeric(hero, ctx) != 0.0,
            _ => throw new InvalidOperationException($"Unsupported APL unary operator: '{Operator}'")
        };
    }

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        return Operator switch
        {
            UnaryOperator.Not => Operand.Evaluate(hero, ctx) ? 0.0 : 1.0,
            UnaryOperator.Minus => -Operand.EvaluateNumeric(hero, ctx),
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
    public static Aura? Find(AuraContainer auras, string slug, SimTime now) => auras.FindActive(slug, now);

    /// <summary>
    /// The effect <paramref name="slug"/> <paramref name="hero"/> keeps on <paramref name="enemy"/>
    /// (<see cref="ITargetEffects"/>: its own expiry, where the enemy's debuff that shows it has none), or else the aura of
    /// that name on it; null when neither is on it.
    /// </summary>
    public static TargetEffect? On(Hero hero, EnemyTarget enemy, string slug, SimTime now) =>
        hero is ITargetEffects effects && effects.FindOn(enemy, slug, now) is { } effect ? effect
        : enemy.Auras.FindActive(slug, now) is { } aura ? TargetEffect.Of(aura) : null;
}
