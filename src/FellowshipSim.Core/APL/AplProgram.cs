using FellowshipSim.Core.APL.Debugging;
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
    private readonly BoundAplList[] _lists;

    private AplProgram(BoundAplList? defaultList, BoundAplList[] lists) => (_default, _lists) = (defaultList, lists);

    /// <summary>The steps of <see cref="AplActionLine.DefaultList"/>.</summary>
    public IReadOnlyList<BoundAplStep> Actions => _default?.Steps ?? [];

    /// <summary>
    /// Binds <paramref name="lists"/> to <paramref name="hero"/>. An action naming an ability another build of the hero has
    /// (another weapon's) is left out, so one list serves every build. An action naming no ability any build of the hero
    /// has, or a list action naming no list, throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public static AplProgram Bind(AplActionLists lists, Hero hero)
    {
        var bound = lists.Lists.ToDictionary(kv => kv.Key, _ => new BoundAplList());
        var variables = new BoundVariables();
        foreach (var (name, actions) in lists.Lists)
        {
            bound[name].Steps = actions.Select(action => BindStep(action, hero, bound, variables)).OfType<BoundAplStep>().ToArray();
        }
        return new AplProgram(bound.GetValueOrDefault(AplActionLine.DefaultList), bound.Values.ToArray());
    }

    /// <summary>
    /// Binds <paramref name="lists"/> to <paramref name="hero"/> as <see cref="Bind"/> does, with a log of every decision
    /// (<see cref="Log"/>). Each step has its line and each expression its <see cref="AplProbe"/>, bound with the same
    /// variables.
    /// </summary>
    internal static AplProgram BindTraced(AplActionLists lists, Hero hero)
    {
        var sourceLines = lists.Lists.SelectMany(kv => kv.Value.Select(action => (List: kv.Key, Action: action)))
            .OrderBy(line => line.Action.LineNumber).ToList();
        var lineOf = sourceLines.Select((line, index) => (line.Action, index)).ToDictionary(x => x.Action, x => x.index);
        var bound = lists.Lists.ToDictionary(kv => kv.Key, _ => new BoundAplList());
        var variables = new BoundVariables();
        var inBuild = new HashSet<AplAction>();
        foreach (var (name, actions) in lists.Lists)
        {
            var steps = new List<BoundAplStep>();
            foreach (var action in actions)
            {
                if (BindStep(action, hero, bound, variables) is not { } step) continue;
                inBuild.Add(action);
                step.Line = lineOf[action];
                if (action.Condition != null) step.ConditionProbe = AplProbe.Of(action.Condition, hero, variables);
                if (step is BoundAplAction ability && action.TargetIf != null)
                {
                    ability.TargetIfProbe = AplProbe.Of(action.TargetIf.Expression, hero, variables);
                }
                steps.Add(step);
            }
            bound[name].Steps = steps.ToArray();
        }
        var lines = sourceLines.Select(line => new AplTraceLine(line.List, line.Action.LineNumber, line.Action.RawLine, inBuild.Contains(line.Action))).ToList();
        return new AplProgram(bound.GetValueOrDefault(AplActionLine.DefaultList), bound.Values.ToArray())
        {
            Log = new AplDecisionLog(hero, lines, variables),
        };
    }

    /// <summary>The log of every decision, for a program bound with <see cref="BindTraced"/>; null otherwise.</summary>
    internal AplDecisionLog? Log { get; private init; }

    /// <summary>
    /// The time of the next decision after one that cast nothing: the next event, or the earliest time anything the lists
    /// read can change with no event before it (<see cref="NextChange"/>), whichever comes first.
    /// </summary>
    public SimTime NextDecision(SimulationContext ctx) =>
        Crossing.Earlier(ctx.Kernel.PeekNextTime() ?? SimTime.Infinite, NextChange(ctx));

    /// <summary>
    /// The earliest time after now at which a condition, a target selection or a <see cref="Ability.CanCast(SimulationContext)"/>
    /// of any list can change with no event before it, or <see cref="SimTime.Infinite"/>.
    /// </summary>
    public SimTime NextChange(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        var next = SimTime.Infinite;
        foreach (var list in _lists)
        {
            foreach (var step in list.Steps)
            {
                var change = step switch
                {
                    BoundAplAction action => action.NextChange(ctx),
                    // A variable is set again at the next decision, so a change of its value is a change of what reads it.
                    BoundAplVariable variable => Crossing.Earlier(variable.Condition?.NextChange(ctx) ?? SimTime.Infinite,
                        variable.Value?.NextChange(ctx) ?? SimTime.Infinite),
                    _ => step.Condition?.NextChange(ctx) ?? SimTime.Infinite,
                };
                next = Crossing.Earlier(next, change);
            }
        }
        return Crossing.Later(now, next);
    }

    // Null for an action naming no ability of the hero.
    private static BoundAplStep? BindStep(AplAction action, Hero hero, Dictionary<string, BoundAplList> lists, BoundVariables variables)
    {
        var condition = action.Condition == null ? null : BoundExpression.Bind(action.Condition, hero, variables);
        if (action.Variable is { } variable)
        {
            var slot = variables.Slot(variable.Name);
            slot.Default = variable.Default;
            var value = variable.Value == null ? null : BoundExpression.Bind(variable.Value, hero, variables);
            return new BoundAplVariable(slot, variable.Op, value, condition);
        }
        if (action.ListAction is { } listAction)
        {
            if (!lists.TryGetValue(action.ListName!, out var list))
            {
                throw new InvalidOperationException($"APL: no action list named `{action.ListName}`.");
            }
            return new BoundAplListAction(list, listAction, condition);
        }
        if (action.ResolveAbility(hero) is not { } ability)
        {
            if (AnyBuildHas(hero, action.AbilityName)) return null;
            throw new InvalidOperationException($"APL: {hero.GetType().Name} has no ability named `{action.AbilityName}`.");
        }
        var targetIf = action.TargetIf == null ? null : new BoundTargetIf(action.TargetIf.Selection, BoundExpression.Bind(action.TargetIf.Expression, hero, variables));
        return new BoundAplAction(hero, ability, condition, targetIf);
    }

    // The abilities of every build of a hero in the catalog (with every weapon's), by the hero's class.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReadOnlyList<AplName>> BuildAbilities = new();

    private static bool AnyBuildHas(Hero hero, string aplName)
    {
        if (!Catalog.HasHero(hero.GetType().Name)) return false;
        var abilities = BuildAbilities.GetOrAdd(hero.GetType(), type => AplVocabulary.For(type.Name).Abilities);
        string key = AplNames.Slug(aplName).Replace("_", "");
        return abilities.Any(a => a.Slug.Replace("_", "") == key);
    }

    /// <summary>Casts the first ready action, starting at <see cref="AplActionLine.DefaultList"/>. False when nothing was cast.</summary>
    public bool TryAct(SimulationContext ctx)
    {
        if (Log is not { } log) return TryAct(ctx, _default, default(NoDecisionTrace));
        log.Begin(ctx);
        bool acted = TryAct(ctx, _default, new LoggedDecisionTrace(log));
        log.End();
        return acted;
    }

    // A list already running in this decision is skipped, so lists that name each other end.
    private static bool TryAct<TTrace>(SimulationContext ctx, BoundAplList? list, TTrace trace) where TTrace : struct, IAplDecisionTrace
    {
        if (list == null || list.Running) return false;
        list.Running = true;
        try
        {
            foreach (var step in list.Steps)
            {
                if (step is BoundAplVariable variable)
                {
                    bool holds = variable.ConditionHolds(ctx);
                    trace.Variable(variable, ctx, holds);
                    if (holds)
                    {
                        variable.Run(ctx);
                        trace.VariableSet(variable, ctx);
                    }
                    continue;
                }
                if (step is BoundAplListAction listAction)
                {
                    bool running = listAction.List.Running;
                    bool enters = !running && listAction.ConditionHolds(ctx);
                    trace.ListAction(listAction, ctx,
                        running ? AplStepOutcome.ListRunning : enters ? AplStepOutcome.ListEntered : AplStepOutcome.ConditionFalse);
                    if (!enters) continue;
                    bool acted = TryAct(ctx, listAction.List, trace);
                    trace.ListEnded();
                    if (acted || listAction.Kind == AplListAction.Run) return acted;
                    continue;
                }
                var action = (BoundAplAction)step;
                bool canExecute = action.CanExecute(ctx);
                trace.Action(action, ctx, canExecute);
                if (!canExecute) continue;
                var attempt = action.Execute(ctx);
                trace.Cast(attempt);
                if (attempt.Succeeded) return true;
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

    /// <summary>The step's index in its traced fight's lines (<see cref="AplFightTrace.Lines"/>); set by <see cref="AplProgram.BindTraced"/>.</summary>
    internal int Line { get; set; } = -1;

    /// <summary>The condition's probe, for a program bound with <see cref="AplProgram.BindTraced"/>.</summary>
    internal AplProbe? ConditionProbe { get; set; }

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

/// <summary>A <c>variable</c> action bound to its variable. Running it sets the variable; it casts nothing.</summary>
public sealed class BoundAplVariable : BoundAplStep
{
    private readonly BoundVariableSlot _slot;
    private readonly AplVariableOp _op;

    internal BoundAplVariable(BoundVariableSlot slot, AplVariableOp op, BoundExpression? value, BoundExpression? condition) : base(condition)
    {
        _slot = slot;
        _op = op;
        Value = value;
    }

    public BoundExpression? Value { get; }

    /// <summary>The variable's value in the fight of <paramref name="ctx"/>.</summary>
    internal double Read(SimulationContext ctx) => _slot.Read(ctx);

    public void Run(SimulationContext ctx)
    {
        double value = Value?.EvaluateNumeric(ctx) ?? 0.0;
        _slot.Write(ctx, _op switch
        {
            AplVariableOp.Add => _slot.Read(ctx) + value,
            AplVariableOp.Sub => _slot.Read(ctx) - value,
            AplVariableOp.Reset => _slot.Default,
            _ => value,
        });
    }
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

    /// <summary>The <c>target_if</c> selection, or null for an action with none.</summary>
    internal AplTargetSelection? TargetSelection => _targetIf?.Selection;

    /// <summary>The <c>target_if</c> expression's probe, for a program bound with <see cref="AplProgram.BindTraced"/>.</summary>
    internal AplProbe? TargetIfProbe { get; set; }

    public bool CanExecute(SimulationContext ctx)
    {
        if (!TrySelectTarget(ctx, out var target)) return false;
        if (Condition != null && !EvaluateAt(ctx, target, Condition)) return false;
        return Ability.CanCast(ctx);
    }

    /// <summary>
    /// Starts the action's ability, or has the engine's reason for refusing it. The cast sets the hasted global cooldown
    /// (<see cref="SimulationContext.BeginCast"/>).
    /// </summary>
    public CastAttempt Execute(SimulationContext ctx)
    {
        if (!TrySelectTarget(ctx, out var target)) return CastAttempt.Rejected("no target");
        if (target == null)
        {
            target = ctx.AddEnemy(1, 1, "Target Dummy");
            ctx.Player.CurrentTarget = target;
        }

        return ctx.BeginCast(_hero, Ability, target);
    }

    /// <summary>
    /// The earliest time after now at which the action's target selection, its condition at its target or its ability's
    /// <see cref="Ability.CanCast(SimulationContext)"/> can change with no event before it.
    /// </summary>
    internal SimTime NextChange(SimulationContext ctx)
    {
        var next = Ability.NextCastChange(ctx);
        var active = ctx.ActiveEnemies;
        if (_targetIf != null && active.Count > 0) next = Crossing.Earlier(next, SelectionChange(ctx, active));
        if (Condition != null)
        {
            TrySelectTarget(ctx, out var target);
            next = Crossing.Earlier(next, ChangeAt(ctx, target, Condition));
        }
        return next;
    }

    /// <summary>
    /// The earliest time the <c>target_if</c> pick can change: a candidate's truth for <c>first</c>; for <c>min</c> and
    /// <c>max</c>, the end of a candidate's curve or the first millisecond the pick differs where two candidates' values
    /// cross.
    /// </summary>
    private SimTime SelectionChange(SimulationContext ctx, IReadOnlyList<EnemyTarget> candidates)
    {
        var (selection, expression) = _targetIf!;
        var now = ctx.CurrentTime;
        var next = SimTime.Infinite;
        if (selection == AplTargetSelection.First)
        {
            foreach (var enemy in candidates) next = Crossing.Earlier(next, ChangeAt(ctx, enemy, expression));
            return next;
        }
        var current = ctx.Player.CurrentTarget;
        bool level = true;
        try
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                ctx.Player.CurrentTarget = candidates[i];
                level &= expression.IsLevel(ctx);
                next = Crossing.Earlier(next, Crossing.Later(now, expression.Until(ctx)));
            }
        }
        finally
        {
            ctx.Player.CurrentTarget = current;
        }
        // Values that hold until their curves end cannot cross before.
        if (level) return next;
        var curves = new TimeCurve[candidates.Count];
        try
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                ctx.Player.CurrentTarget = candidates[i];
                curves[i] = expression.Curve(ctx);
            }
        }
        finally
        {
            ctx.Player.CurrentTarget = current;
        }
        var roots = new List<double>();
        for (int i = 0; i < curves.Length; i++)
        {
            for (int j = i + 1; j < curves.Length; j++)
            {
                var difference = curves[i] - curves[j];
                Polynomial.Roots(difference.Numerator, -3.0, next.Milliseconds - (double)now.Milliseconds, roots);
                Polynomial.Roots(difference.Denominator, -3.0, next.Milliseconds - (double)now.Milliseconds, roots);
            }
        }
        roots.Sort();
        var picked = PickAt(ctx, candidates, now);
        foreach (double root in roots)
        {
            if (Crossing.FirstAround(now, next, root, time => PickAt(ctx, candidates, time) != picked) is { } changed) return changed;
        }
        return next;
    }

    /// <summary>The <c>min</c> or <c>max</c> pick among <paramref name="candidates"/> at <paramref name="time"/>, as <see cref="Select"/> picks it now.</summary>
    private EnemyTarget? PickAt(SimulationContext ctx, IReadOnlyList<EnemyTarget> candidates, SimTime time)
    {
        var (selection, expression) = _targetIf!;
        EnemyTarget? best = null;
        double bestValue = 0;
        var current = ctx.Player.CurrentTarget;
        try
        {
            foreach (var enemy in candidates)
            {
                ctx.Player.CurrentTarget = enemy;
                double value = expression.NumericAt(ctx, time);
                if (best == null || (selection == AplTargetSelection.Min ? value < bestValue : value > bestValue))
                {
                    best = enemy;
                    bestValue = value;
                }
            }
        }
        finally
        {
            ctx.Player.CurrentTarget = current;
        }
        return best;
    }

    // NextChange of `expression` with `target` as the hero's current target, then restored (as EvaluateAt).
    private static SimTime ChangeAt(SimulationContext ctx, EnemyTarget? target, BoundExpression expression)
    {
        var current = ctx.Player.CurrentTarget;
        if (target == null || target == current) return expression.NextChange(ctx);
        ctx.Player.CurrentTarget = target;
        try
        {
            return expression.NextChange(ctx);
        }
        finally
        {
            ctx.Player.CurrentTarget = current;
        }
    }

    /// <summary>
    /// Target of the cast: the <c>target_if</c> pick among the active enemies, otherwise the current target (null when
    /// there are no enemies). False when <c>target_if</c> finds no enemy.
    /// </summary>
    internal bool TrySelectTarget(SimulationContext ctx, out EnemyTarget? target)
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
        // By index: a foreach over the list interface allocates its enumerator.
        for (int i = 0; i < candidates.Count; i++)
        {
            var enemy = candidates[i];
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
