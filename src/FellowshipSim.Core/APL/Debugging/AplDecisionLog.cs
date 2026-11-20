using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Core.APL.Debugging;

/// <summary>
/// The decisions of one traced fight (<see cref="MonteCarloSimulator.TraceFight"/>). A decision of a program bound with a
/// log (<see cref="AplProgram.BindTraced"/>) records, before its cast, the hero and enemies, then each step it reads
/// with the values of its expressions.
/// </summary>
internal sealed class AplDecisionLog(Hero hero, IReadOnlyList<AplTraceLine> lines, BoundVariables variables)
{
    private readonly List<AplDecision> _decisions = [];
    private List<AplStepTrace> _steps = [];
    private AplHeroState? _state;
    private double _time;
    private int _depth;
    private BoundAplAction? _action;
    private string? _cast;

    public void Begin(SimulationContext ctx)
    {
        _time = ctx.CurrentTime.TotalSeconds;
        _state = State(ctx);
        _steps = [];
        _depth = 0;
        _cast = null;
    }

    public void End()
    {
        var cast = _cast == null ? null : _steps[^1];
        _decisions.Add(new AplDecision(_time, _state!, _steps, cast?.Line, _cast, cast?.Target));
    }

    /// <summary>The fight's trace, <paramref name="fightSeconds"/> long, with its casts.</summary>
    public AplFightTrace Trace(double fightSeconds, IReadOnlyList<CastReport> casts) =>
        new(hero.GetType().Name, fightSeconds, lines, _decisions, casts);

    public void Variable(BoundAplVariable step, SimulationContext ctx, bool holds) =>
        _steps.Add(new AplStepTrace(step.Line, _depth, holds ? AplStepOutcome.VariableSet : AplStepOutcome.VariableKept,
            step.ConditionProbe?.Trace(ctx), Value: step.Read(ctx)));

    public void VariableSet(BoundAplVariable step, SimulationContext ctx) => _steps[^1] = _steps[^1] with { Value = step.Read(ctx) };

    public void ListAction(BoundAplListAction step, SimulationContext ctx, AplStepOutcome outcome)
    {
        _steps.Add(new AplStepTrace(step.Line, _depth, outcome, outcome == AplStepOutcome.ListRunning ? null : step.ConditionProbe?.Trace(ctx)));
        if (outcome == AplStepOutcome.ListEntered) _depth++;
    }

    public void ListEnded() => _depth--;

    public void SwitchTarget(BoundAplSwitchTarget step, SimulationContext ctx)
    {
        var pick = step.Pick(ctx);
        var targetIf = TargetIf(step, ctx, step.TargetSelection, pick);
        var condition = pick != null && step.ConditionProbe is { } c ? AtTarget(ctx, pick, c) : null;
        var outcome = pick == null ? AplStepOutcome.NoTarget
            : condition is { Value: 0.0 } ? AplStepOutcome.ConditionFalse
            : pick == ctx.Player.CurrentTarget ? AplStepOutcome.TargetKept
            : AplStepOutcome.TargetSwitched;
        _steps.Add(new AplStepTrace(step.Line, _depth, outcome, condition, targetIf, pick == null ? null : Label(ctx, pick)));
    }

    // The step's target_if value at each enemy in combat, with the enemy picked; null with no enemy in combat.
    private static AplTargetIfTrace? TargetIf(BoundAplStep step, SimulationContext ctx, AplTargetSelection selection, EnemyTarget? picked)
    {
        if (step.TargetIfProbe is not { } probe || ctx.ActiveEnemies.Count == 0) return null;
        var candidates = ctx.ActiveEnemies.Select(enemy => new AplCandidateTrace(Label(ctx, enemy), AtTarget(ctx, enemy, probe))).ToList();
        return new AplTargetIfTrace(selection, candidates, picked == null ? null : Label(ctx, picked));
    }

    public void Action(BoundAplAction step, SimulationContext ctx, bool canExecute)
    {
        _action = step;
        bool found = step.TrySelectTarget(ctx, out var target);
        var targetIf = step.TargetSelection is { } selection ? TargetIf(step, ctx, selection, found ? target : null) : null;
        var condition = found && step.ConditionProbe is { } c ? AtTarget(ctx, target, c) : null;
        string? procRefusal = canExecute ? null : ProcReactionRefusal(step, ctx);
        var outcome = procRefusal != null ? AplStepOutcome.CastRefused
            : !found ? AplStepOutcome.NoTarget
            : condition is { Value: 0.0 } ? AplStepOutcome.ConditionFalse
            : canExecute ? AplStepOutcome.Cast
            : AplStepOutcome.NotReady;
        _steps.Add(new AplStepTrace(step.Line, _depth, outcome, condition, targetIf, target == null ? null : Label(ctx, target), Refusal: procRefusal));
    }

    /// <summary>
    /// The reason for an action refused only because a proc's gain is inside the proc reaction time
    /// (<see cref="SimulationContext.ProcReactionTime"/>): the action could go if the action lists saw every gain at once.
    /// Null for any other action.
    /// </summary>
    private string? ProcReactionRefusal(BoundAplAction step, SimulationContext ctx)
    {
        var reaction = ctx.ProcReactionTime;
        if (reaction <= SimTime.Zero) return null;
        var now = ctx.CurrentTime;
        var unseen = hero.Auras.Active(now).Where(aura => aura.SeenGainAt(now, reaction) != SimTime.Infinite).ToList();
        if (unseen.Count == 0) return null;
        bool seenAtOnce;
        ctx.ProcReactionTime = SimTime.Zero;
        try
        {
            seenAtOnce = step.CanExecute(ctx);
        }
        finally
        {
            ctx.ProcReactionTime = reaction;
        }
        if (!seenAtOnce) return null;
        // The procs the action waits on alone; every unseen proc when it waits on none alone.
        var blocking = unseen.Where(aura => CanExecuteSeeing(step, ctx, aura, reaction)).ToList();
        if (blocking.Count == 0) blocking = unseen;
        return string.Join("; ", blocking.Select(aura =>
            $"{aura.Name} was gained {(now - aura.GainedAt).TotalSeconds:F3}s ago, less than the {reaction.TotalSeconds:F3}s proc reaction time"));
    }

    // CanExecute with the gain of `proc` alone seen at once, then the gain time restored.
    private static bool CanExecuteSeeing(BoundAplAction step, SimulationContext ctx, Aura proc, SimTime reaction)
    {
        var gainedAt = proc.GainedAt;
        proc.GainedAt = gainedAt - reaction;
        try
        {
            return step.CanExecute(ctx);
        }
        finally
        {
            proc.GainedAt = gainedAt;
        }
    }

    public void Cast(CastAttempt attempt)
    {
        if (attempt.Succeeded) _cast = _action!.Ability.Name;
        else _steps[^1] = _steps[^1] with { Outcome = AplStepOutcome.CastRefused, Refusal = attempt.Reason };
    }

    private static AplNodeTrace AtTarget(SimulationContext ctx, EnemyTarget? target, AplProbe probe)
    {
        var current = ctx.Player.CurrentTarget;
        if (target == null || target == current) return probe.Trace(ctx);
        ctx.Player.CurrentTarget = target;
        try
        {
            return probe.Trace(ctx);
        }
        finally
        {
            ctx.Player.CurrentTarget = current;
        }
    }

    // An enemy's name, with its instance where another enemy of the fight has the same name.
    private static string Label(SimulationContext ctx, EnemyTarget enemy) =>
        ctx.AllEnemies.Any(other => other != enemy && other.Name == enemy.Name) ? $"{enemy.Name} #{enemy.InstanceId}" : enemy.Name;

    private AplHeroState State(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        var resources = hero.ResourceDefs.Select(def => hero.Resources.Get(def))
            .Zip(hero.ResourceDefs, (state, def) => new AplResourceValue(def.Name, state.Current, state.Max)).ToList();
        var buffs = Auras(hero.Auras.Active(now), now);
        var cooldowns = new List<AplCooldownValue>();
        foreach (var ability in hero.Abilities)
        {
            if (!hero.Cooldowns.TryGetValue(ability.Name, out var cooldown)) continue;
            int charges = cooldown.ChargesAt(now);
            double remains = charges < cooldown.MaxCharges ? Math.Max(0.0, (cooldown.NextChargeAt - now).TotalSeconds) : 0.0;
            cooldowns.Add(new AplCooldownValue(ability.Name, charges, cooldown.MaxCharges, remains));
        }
        var values = variables.Slots.Select(slot => new AplVariableValue(slot.Key, slot.Value.Read(ctx))).ToList();
        var enemies = ctx.ActiveEnemies.Select(enemy => new AplEnemyValue(Label(ctx, enemy), enemy.HealthFollowsTime ? null : enemy.HealthPercent,
            enemy == ctx.Player.CurrentTarget, Debuffs(ctx, enemy, now))).ToList();
        double globalCooldown = Math.Max(0.0, (hero.GlobalCooldownEndTime - now).TotalSeconds);
        return new AplHeroState(resources, buffs, globalCooldown, cooldowns, values, enemies);
    }

    private static List<AplAuraValue> Auras(IEnumerable<Aura> auras, SimTime now) =>
        auras.Select(aura => new AplAuraValue(aura.Name, aura.Stacks, Remains(aura.ExpiresAt, now))).OrderBy(a => a.Name).ToList();

    // The enemy's auras, and the effects the hero keeps on it (ITargetEffects).
    private List<AplAuraValue> Debuffs(SimulationContext ctx, EnemyTarget enemy, SimTime now)
    {
        var debuffs = Auras(enemy.Auras.Active(now), now);
        if (hero is ITargetEffects effects)
        {
            foreach (string name in effects.TargetEffectNames)
            {
                if (debuffs.Any(d => d.Name == name)) continue;
                if (effects.FindOn(enemy, AplNames.Slug(name), now) is { } effect)
                {
                    debuffs.Add(new AplAuraValue(name, effect.Stacks, Remains(effect.ExpiresAt, now)));
                }
            }
        }
        return debuffs.OrderBy(d => d.Name).ToList();
    }

    private static double? Remains(SimTime expiresAt, SimTime now) =>
        expiresAt == SimTime.Infinite ? null : Math.Max(0.0, (expiresAt - now).TotalSeconds);
}
