using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Core.APL;

/// <summary>
/// Action lists bound to one hero (<see cref="Bind"/>): each action holds its ability, each list action its list, and each
/// expression is a <see cref="BoundExpression"/>. A decision does no string or dictionary work, and it casts what
/// <see cref="AplActionLists.TryAct(Hero, SimulationContext)"/> on the parsed lists would cast.
/// </summary>
public sealed class AplProgram
{
    private readonly BoundAplList? _default;

    private AplProgram(BoundAplList? defaultList) => _default = defaultList;

    /// <summary>The steps of <see cref="AplActionLine.DefaultList"/>.</summary>
    public IReadOnlyList<BoundAplStep> Actions => _default?.Steps ?? [];

    /// <summary>
    /// Binds <paramref name="lists"/> to <paramref name="hero"/>. An action naming no ability of the hero, or a list action
    /// naming no list, throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public static AplProgram Bind(AplActionLists lists, Hero hero)
    {
        var bound = lists.Lists.ToDictionary(kv => kv.Key, _ => new BoundAplList());
        foreach (var (name, actions) in lists.Lists)
        {
            bound[name].Steps = actions.Select(action => BindStep(action, hero, bound)).ToArray();
        }
        return new AplProgram(bound.GetValueOrDefault(AplActionLine.DefaultList));
    }

    private static BoundAplStep BindStep(AplAction action, Hero hero, Dictionary<string, BoundAplList> lists)
    {
        var condition = action.Condition == null ? null : BoundExpression.Bind(action.Condition, hero);
        if (action.ListAction is { } listAction)
        {
            if (!lists.TryGetValue(action.ListName!, out var list))
            {
                throw new InvalidOperationException($"APL: no action list named `{action.ListName}`.");
            }
            return new BoundAplListAction(list, listAction, condition);
        }
        var ability = action.ResolveAbility(hero)
            ?? throw new InvalidOperationException($"APL: {hero.GetType().Name} has no ability named `{action.AbilityName}`.");
        var targetIf = action.TargetIf == null ? null : new BoundTargetIf(action.TargetIf.Selection, BoundExpression.Bind(action.TargetIf.Expression, hero));
        return new BoundAplAction(hero, ability, condition, targetIf);
    }

    /// <summary>Casts the first ready action, starting at <see cref="AplActionLine.DefaultList"/>. False when nothing was cast.</summary>
    public bool TryAct(SimulationContext ctx) => TryAct(ctx, _default);

    // A list already running in this decision is skipped, so lists that name each other end.
    private static bool TryAct(SimulationContext ctx, BoundAplList? list)
    {
        if (list == null || list.Running) return false;
        list.Running = true;
        try
        {
            foreach (var step in list.Steps)
            {
                if (step is BoundAplListAction listAction)
                {
                    if (listAction.List.Running || !listAction.ConditionHolds(ctx)) continue;
                    bool acted = TryAct(ctx, listAction.List);
                    if (acted || listAction.Kind == AplListAction.Run) return acted;
                    continue;
                }
                var action = (BoundAplAction)step;
                if (action.CanExecute(ctx) && action.Execute(ctx)) return true;
            }
            return false;
        }
        finally
        {
            list.Running = false;
        }
    }

}

/// <summary>A list of a bound program. <see cref="Running"/> is set while a decision runs it.</summary>
internal sealed class BoundAplList
{
    public BoundAplStep[] Steps { get; set; } = [];
    public bool Running { get; set; }
}

/// <summary>One bound action line: an ability (<see cref="BoundAplAction"/>) or a list action (<see cref="BoundAplListAction"/>).</summary>
public abstract class BoundAplStep
{
    private protected BoundAplStep(BoundExpression? condition) => Condition = condition;

    public BoundExpression? Condition { get; }

    /// <summary>True when the step has no condition or its condition is true at the current target.</summary>
    public bool ConditionHolds(SimulationContext ctx) => Condition == null || Condition.Evaluate(ctx);
}

/// <summary><c>run_action_list</c> or <c>call_action_list</c> bound to its list.</summary>
public sealed class BoundAplListAction : BoundAplStep
{
    internal BoundAplListAction(BoundAplList list, AplListAction kind, BoundExpression? condition) : base(condition)
    {
        List = list;
        Kind = kind;
    }

    internal BoundAplList List { get; }
    public AplListAction Kind { get; }
}

/// <summary>A <c>target_if</c> bound to its hero.</summary>
internal sealed record BoundTargetIf(AplTargetSelection Selection, BoundExpression Expression);

/// <summary>An ability action bound to its ability, with the same target selection, condition and cast as <see cref="AplAction"/>.</summary>
public sealed class BoundAplAction : BoundAplStep
{
    private readonly Hero _hero;
    private readonly BoundTargetIf? _targetIf;

    internal BoundAplAction(Hero hero, Ability ability, BoundExpression? condition, BoundTargetIf? targetIf) : base(condition)
    {
        _hero = hero;
        Ability = ability;
        _targetIf = targetIf;
    }

    public Ability Ability { get; }

    public bool CanExecute(SimulationContext ctx)
    {
        if (!TrySelectTarget(ctx, out var target)) return false;
        if (Condition != null && !EvaluateAt(ctx, target, Condition)) return false;
        return Ability.CanCast(ctx);
    }

    /// <summary>Starts the action's ability and sets the global cooldown. Returns false if nothing was cast.</summary>
    public bool Execute(SimulationContext ctx)
    {
        if (!TrySelectTarget(ctx, out var target)) return false;
        if (target == null)
        {
            target = ctx.AddEnemy(1, 1, "Target Dummy");
            ctx.Player.CurrentTarget = target;
        }

        var attempt = ctx.BeginCast(_hero, Ability, target);
        if (!attempt.Succeeded) return false;

        if (!Ability.IgnoresGcd)
        {
            var gcd = attempt.CastTime == SimTime.Zero ? SimTime.FromSeconds(1.0) : attempt.CastTime;
            _hero.GlobalCooldownEndTime = ctx.CurrentTime + gcd;
        }
        return true;
    }

    /// <summary>
    /// Target of the cast: the <c>target_if</c> pick among the active enemies, otherwise the current target (null when
    /// there are no enemies). False when <c>target_if</c> finds no enemy.
    /// </summary>
    private bool TrySelectTarget(SimulationContext ctx, out EnemyTarget? target)
    {
        var active = ctx.ActiveEnemies;
        target = ctx.Player.CurrentTarget ?? (active.Count > 0 ? active[0] : null);
        if (_targetIf == null || ctx.ActiveEnemies.Count == 0) return true;
        target = Select(ctx, ctx.ActiveEnemies);
        return target != null;
    }

    /// <summary>The <c>target_if</c> pick among <paramref name="candidates"/>, as <see cref="AplTargetIf.Select"/> picks it.</summary>
    private EnemyTarget? Select(SimulationContext ctx, IReadOnlyList<EnemyTarget> candidates)
    {
        var (selection, expression) = _targetIf!;
        EnemyTarget? best = null;
        double bestValue = 0;
        foreach (var enemy in candidates)
        {
            if (selection == AplTargetSelection.First)
            {
                if (EvaluateAt(ctx, enemy, expression)) return enemy;
                continue;
            }
            double value = EvaluateNumericAt(ctx, enemy, expression);
            if (best == null || (selection == AplTargetSelection.Min ? value < bestValue : value > bestValue))
            {
                best = enemy;
                bestValue = value;
            }
        }
        return best;
    }

    // With target as the hero's current target, then restored (as AplAction.AtTarget, without a closure).
    private static bool EvaluateAt(SimulationContext ctx, EnemyTarget? target, BoundExpression expression)
    {
        var current = ctx.Player.CurrentTarget;
        if (target == null || target == current) return expression.Evaluate(ctx);
        ctx.Player.CurrentTarget = target;
        try
        {
            return expression.Evaluate(ctx);
        }
        finally
        {
            ctx.Player.CurrentTarget = current;
        }
    }

    private static double EvaluateNumericAt(SimulationContext ctx, EnemyTarget? target, BoundExpression expression)
    {
        var current = ctx.Player.CurrentTarget;
        if (target == null || target == current) return expression.EvaluateNumeric(ctx);
        ctx.Player.CurrentTarget = target;
        try
        {
            return expression.EvaluateNumeric(ctx);
        }
        finally
        {
            ctx.Player.CurrentTarget = current;
        }
    }

    public override string ToString() => Ability.Name;
}
