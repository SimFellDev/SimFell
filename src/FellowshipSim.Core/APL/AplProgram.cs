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
    private readonly Hero _hero;
    private readonly BoundAplList? _default;
    private readonly BoundAplList[] _lists;

    private AplProgram(Hero hero, BoundAplList? defaultList, BoundAplList[] lists)
    {
        (_hero, _default, _lists) = (hero, defaultList, lists);
        foreach (var action in lists.SelectMany(list => list.Steps).OfType<BoundAplAction>())
        {
            HasOffGcdActions |= action.UseOffGcd;
            HasWhileCastingActions |= action.UseWhileCasting && action.Ability.IgnoresGcd;
        }
    }

    /// <summary>True when an action of the lists is a candidate during <see cref="AplDecisionHold.GlobalCooldown"/>.</summary>
    public bool HasOffGcdActions { get; }

    /// <summary>True when an action of the lists is a candidate during <see cref="AplDecisionHold.Cast"/>.</summary>
    public bool HasWhileCastingActions { get; }

    /// <summary>
    /// The hold on the hero now: <see cref="AplDecisionHold.Cast"/> while a cast or a channel runs,
    /// <see cref="AplDecisionHold.GlobalCooldown"/> while the global cooldown (past <see cref="SimulationContext.GlobalCooldownToleranceSeconds"/>)
    /// or the reaction after a hold runs.
    /// </summary>
    public AplDecisionHold HoldOf(SimulationContext ctx)
    {
        if (_hero.CurrentCast != null || _hero.CurrentChannel != null) return AplDecisionHold.Cast;
        double left = (_hero.GlobalCooldownEndTime - ctx.CurrentTime).TotalSeconds;
        return left > ctx.GlobalCooldownToleranceSeconds || ctx.CurrentTime < ctx.ReactionEndTime ? AplDecisionHold.GlobalCooldown : AplDecisionHold.None;
    }

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
        return new AplProgram(hero, bound.GetValueOrDefault(AplActionLine.DefaultList), bound.Values.ToArray());
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
        return new AplProgram(hero, bound.GetValueOrDefault(AplActionLine.DefaultList), bound.Values.ToArray())
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
        var interruptIf = action.InterruptIf == null ? null : BoundExpression.Bind(action.InterruptIf, hero, variables);
        return new BoundAplAction(hero, ability, condition, targetIf, action.UseOffGcd, action.UseWhileCasting)
        {
            InterruptIf = interruptIf,
            InterruptImmediate = action.InterruptImmediate,
        };
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

    // The action that last cast a channeled ability: its channel is the hero's while the hero channels that ability.
    private BoundAplAction? _channelAction;

    // The channel whose ticks the interrupt check has seen, and how many.
    private ActiveChannel? _seenChannel;
    private int _ticksSeen;

    /// <summary>The action with an <c>interrupt_if</c> whose channel runs now; null when no such channel runs.</summary>
    private BoundAplAction? InterruptibleAction =>
        _hero.CurrentChannel is { } channel && _channelAction is { InterruptIf: not null } action && channel.Ability == action.Ability ? action : null;

    /// <summary>
    /// Stops the hero's channel when the action that started it has an <c>interrupt_if</c> that is true at the channel's
    /// target, and an ability action above that action's line is ready to cast (<see cref="SimulationContext.InterruptChannel"/>).
    /// Without <c>interrupt_immediate=1</c>, the check runs only right after a tick of the channel once the global cooldown
    /// has ended. True when the channel stopped.
    /// </summary>
    public bool TryInterrupt(SimulationContext ctx)
    {
        if (InterruptibleAction is not { } action) return false;
        var channel = _hero.CurrentChannel!;
        if (!action.InterruptImmediate)
        {
            if (channel != _seenChannel) (_seenChannel, _ticksSeen) = (channel, 0);
            bool ticked = channel.Ticks.TicksFired > _ticksSeen;
            _ticksSeen = channel.Ticks.TicksFired;
            if (!ticked) return false;
            double left = (_hero.GlobalCooldownEndTime - ctx.CurrentTime).TotalSeconds;
            if (left > ctx.GlobalCooldownToleranceSeconds) return false;
        }
        if (!action.InterruptHolds(ctx, channel.Target)) return false;
        if (ReadyAbove(ctx, _default, action) != Scan.Ready) return false;
        return ctx.InterruptChannel(_hero);
    }

    /// <summary>The end of a scan for a ready action above a channel's line (<see cref="ReadyAbove"/>).</summary>
    private enum Scan
    {
        /// <summary>The list ended with no ready action and without the channel's line.</summary>
        None,
        /// <summary>A ready ability action comes before the channel's line.</summary>
        Ready,
        /// <summary>The scan reached the channel's line first.</summary>
        Channel,
    }

    // SimulationCraft's select_action with the channel's action as its context: the first ready ability action of the
    // decision, as at the top of a decision with no hold, when it comes before the channel's own line. A variable action is
    // left to the decision after the interrupt, so the scan changes no variable; a list action enters its list as a
    // decision does.
    private static Scan ReadyAbove(SimulationContext ctx, BoundAplList? list, BoundAplAction channel)
    {
        if (list == null || list.Running) return Scan.None;
        list.Running = true;
        try
        {
            foreach (var step in list.Steps)
            {
                switch (step)
                {
                    case BoundAplAction action when action == channel:
                        return Scan.Channel;
                    case BoundAplAction action:
                        if (action.CanExecute(ctx)) return Scan.Ready;
                        break;
                    case BoundAplListAction listAction when !listAction.List.Running && listAction.ConditionHolds(ctx):
                        var found = ReadyAbove(ctx, listAction.List, channel);
                        if (found != Scan.None || listAction.Kind == AplListAction.Run) return found;
                        break;
                }
            }
            return Scan.None;
        }
        finally
        {
            list.Running = false;
        }
    }

    /// <summary>
    /// The next time <see cref="TryInterrupt"/> can stop the running channel: the next event (a tick is one), or with
    /// <c>interrupt_immediate=1</c> also the time the condition can turn true with no event before it.
    /// <see cref="SimTime.Infinite"/> when no channel with an <c>interrupt_if</c> runs.
    /// </summary>
    public SimTime NextInterruptCheck(SimulationContext ctx)
    {
        if (InterruptibleAction is not { } action) return SimTime.Infinite;
        var next = ctx.Kernel.PeekNextTime() ?? SimTime.Infinite;
        if (!action.InterruptImmediate) return next;
        return Crossing.Earlier(next, Crossing.Later(ctx.CurrentTime, action.InterruptChange(ctx, _hero.CurrentChannel!.Target)));
    }

    /// <summary>
    /// Casts the first ready candidate action for the hold on the hero (<see cref="HoldOf"/>), starting at
    /// <see cref="AplActionLine.DefaultList"/>. False when nothing was cast.
    /// </summary>
    public bool TryAct(SimulationContext ctx)
    {
        var hold = HoldOf(ctx);
        if (Log is not { } log) return TryAct(ctx, _default, hold, default(NoDecisionTrace));
        log.Begin(ctx);
        bool acted = TryAct(ctx, _default, hold, new LoggedDecisionTrace(log));
        log.End();
        return acted;
    }

    // A list already running in this decision is skipped, so lists that name each other end. An ability action that is no
    // candidate for the hold is skipped and left out of the trace.
    private bool TryAct<TTrace>(SimulationContext ctx, BoundAplList? list, AplDecisionHold hold, TTrace trace) where TTrace : struct, IAplDecisionTrace
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
                    bool acted = TryAct(ctx, listAction.List, hold, trace);
                    trace.ListEnded();
                    if (acted || listAction.Kind == AplListAction.Run) return acted;
                    continue;
                }
                var action = (BoundAplAction)step;
                if (!action.IsCandidate(hold)) continue;
                bool canExecute = action.CanExecute(ctx);
                trace.Action(action, ctx, canExecute);
                if (!canExecute) continue;
                var attempt = action.Execute(ctx);
                trace.Cast(attempt);
                if (attempt.Succeeded)
                {
                    if (action.Ability.IsChanneled) _channelAction = action;
                    return true;
                }
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

    internal BoundAplAction(Hero hero, Ability ability, BoundExpression? condition, BoundTargetIf? targetIf, bool useOffGcd = false,
        bool useWhileCasting = false) : base(condition)
    {
        _hero = hero;
        Ability = ability;
        _targetIf = targetIf;
        UseOffGcd = useOffGcd;
        UseWhileCasting = useWhileCasting;
    }

    public Ability Ability { get; }

    /// <summary><c>use_off_gcd=1</c>.</summary>
    public bool UseOffGcd { get; }

    /// <summary><c>use_while_casting=1</c>.</summary>
    public bool UseWhileCasting { get; }

    /// <summary><c>interrupt_if=&lt;expr&gt;</c>: the condition that stops the channel the action started; null for none.</summary>
    public BoundExpression? InterruptIf { get; init; }

    /// <summary><c>interrupt_immediate=1</c>.</summary>
    public bool InterruptImmediate { get; init; }

    /// <summary>True when <see cref="InterruptIf"/> is true at <paramref name="target"/>, the channel's target.</summary>
    internal bool InterruptHolds(SimulationContext ctx, EnemyTarget? target) => InterruptIf != null && EvaluateAt(ctx, target, InterruptIf);

    /// <summary>The next time <see cref="InterruptIf"/> at <paramref name="target"/> can change with no event before it.</summary>
    internal SimTime InterruptChange(SimulationContext ctx, EnemyTarget? target) =>
        InterruptIf == null ? SimTime.Infinite : ChangeAt(ctx, target, InterruptIf);

    /// <summary>
    /// True when the action is a candidate in a decision under <paramref name="hold"/>. During a cast or a channel only an
    /// ability that ignores the global cooldown is one: any other cast ends the channel or is refused.
    /// </summary>
    public bool IsCandidate(AplDecisionHold hold) => hold switch
    {
        AplDecisionHold.GlobalCooldown => UseOffGcd,
        AplDecisionHold.Cast => UseWhileCasting && Ability.IgnoresGcd,
        _ => true,
    };

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
