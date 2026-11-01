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

    public void Action(BoundAplAction step, SimulationContext ctx, bool canExecute)
    {
        _action = step;
        bool found = step.TrySelectTarget(ctx, out var target);
        AplTargetIfTrace? targetIf = null;
        if (step.TargetIfProbe is { } probe && ctx.ActiveEnemies.Count > 0)
        {
            var candidates = ctx.ActiveEnemies.Select(enemy => new AplCandidateTrace(Label(ctx, enemy), AtTarget(ctx, enemy, probe))).ToList();
            targetIf = new AplTargetIfTrace(step.TargetSelection!.Value, candidates, found && target != null ? Label(ctx, target) : null);
        }
        var condition = found && step.ConditionProbe is { } c ? AtTarget(ctx, target, c) : null;
        var outcome = !found ? AplStepOutcome.NoTarget
            : condition is { Value: 0.0 } ? AplStepOutcome.ConditionFalse
            : canExecute ? AplStepOutcome.Cast
            : AplStepOutcome.NotReady;
        _steps.Add(new AplStepTrace(step.Line, _depth, outcome, condition, targetIf, target == null ? null : Label(ctx, target)));
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
        var enemies = ctx.ActiveEnemies.Select(enemy => new AplEnemyValue(Label(ctx, enemy), enemy.HealthPercent,
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
