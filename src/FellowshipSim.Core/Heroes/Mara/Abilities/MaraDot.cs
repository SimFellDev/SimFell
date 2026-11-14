namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// A damage-over-time effect of Mara's (Hemorrhage, Seething Poison, Hemotoxin): one instance on each enemy, ticking every
/// hasted period until it runs out, with a partial final tick for the hasted progress left. It is not an action an action
/// list uses: its ability applies it. Each instance is a <see cref="PeriodicEffect.BeginHasted"/> effect with the damage log
/// ID and the enemy, so a replay takes each tick's time from the log (<see cref="IOutcomeSource.TickAt"/>).
/// </summary>
public abstract class MaraDot : MaraAbility
{
    protected MaraDot(Mara owner, int effectId, string name, double coefficient, double periodSeconds, double durationSeconds)
        : base(owner, LogIds.Effect(effectId), name)
    {
        BaseCoefficient = coefficient;
        PeriodSeconds = periodSeconds;
        DurationSeconds = durationSeconds;
        Debuff = AuraDef.Effect(name, effectId);
    }

    /// <summary>Unhasted seconds between two ticks.</summary>
    public double PeriodSeconds { get; }

    /// <summary>The duration of one application, unless the ability applying it gives another.</summary>
    public double DurationSeconds { get; }

    /// <summary>The debuff on the enemy, while the effect is on it. Action lists read it.</summary>
    public AuraDef Debuff { get; }

    /// <summary>
    /// On a refresh, up to this fraction of the new duration of the time left carries over (0: a refresh restarts the
    /// duration).
    /// </summary>
    protected double RefreshCarryoverFraction { get; init; }

    /// <summary>
    /// True for an effect tagged EffectType.Dot: its ticks run on the game's period timer
    /// (<see cref="PeriodicEffect.BeginHasted"/> with damageOverTime).
    /// </summary>
    protected bool DamageOverTime { get; init; }

    /// <summary>Raised after each tick has dealt its damage, with its enemy.</summary>
    public event Action<SimulationContext, EnemyTarget>? Ticked;

    /// <summary>Raised when the effect runs out on an enemy (not when the enemy dies).</summary>
    public event Action<SimulationContext, EnemyTarget>? Expired;

    /// <summary>Raised when the effect leaves an enemy, for any reason.</summary>
    public event Action<SimulationContext, EnemyTarget>? Removed;

    /// <summary>One application on one enemy.</summary>
    protected sealed class Instance(EnemyTarget target)
    {
        public EnemyTarget Target { get; } = target;
        /// <summary>When the duration runs out; the effect ends on the server frame after it.</summary>
        public SimTime End;
        public PeriodicEffect? Effect;
        /// <summary>The ability's own damage multiplier for this application (e.g. Vexira's Venom's stored damage).</summary>
        public double Multiplier = 1.0;
        public int Stacks = 1;
        /// <summary>
        /// The tick period scaler (`SetByCaller.System.TickRateOverride`): the period is <see cref="PeriodSeconds"/> times it,
        /// shortened by Haste.
        /// </summary>
        public double PeriodScaler = 1.0;
    }

    private readonly Dictionary<EnemyTarget, Instance> _active = new();
    private SimulationContext? _fight;

    private void SameFight(SimulationContext ctx)
    {
        if (_fight == ctx) return;
        _active.Clear();
        ctx.EnemyDemising += (c, enemy) => Remove(c, enemy);
        _fight = ctx;
    }

    public bool IsOn(EnemyTarget? target, SimulationContext ctx)
    {
        if (target == null) return false;
        SameFight(ctx);
        return _active.ContainsKey(target);
    }

    /// <summary>The number of enemies the effect is on.</summary>
    public int ActiveCount(SimulationContext ctx)
    {
        SameFight(ctx);
        return _active.Count;
    }

    public IEnumerable<EnemyTarget> Targets(SimulationContext ctx)
    {
        SameFight(ctx);
        return _active.Keys;
    }

    protected Instance? Get(EnemyTarget target) => _active.GetValueOrDefault(target);

    /// <summary>Seconds left on <paramref name="target"/> (0 when the effect is not on it).</summary>
    public double Remaining(EnemyTarget target, SimulationContext ctx)
    {
        SameFight(ctx);
        return _active.TryGetValue(target, out var dot) ? Math.Max(0.0, (dot.End - ctx.CurrentTime).TotalSeconds) : 0.0;
    }

    /// <summary>Unhasted seconds between two ticks of <paramref name="dot"/>: the period times its tick period scaler.</summary>
    protected double BasePeriod(Instance dot) => PeriodSeconds * dot.PeriodScaler;

    /// <summary>
    /// Sets the tick period scaler of <paramref name="dot"/>. The tick in progress keeps its fraction of progress: the time to it
    /// is the new period times the fraction left.
    /// </summary>
    private void SetPeriodScaler(Instance dot, double scaler)
    {
        if (scaler == dot.PeriodScaler) return;
        dot.PeriodScaler = scaler;
        dot.Effect?.ChangePeriod(BasePeriod(dot));
    }

    /// <summary>
    /// Applies the effect to <paramref name="target"/> for <paramref name="durationSeconds"/> (its own duration when
    /// null), with tick period scaler <paramref name="periodScaler"/>. On an enemy that has it, the duration restarts with up to
    /// <see cref="RefreshCarryoverFraction"/> of it carried over, the ticks keep their timing, and the scaler is set again: a
    /// refresh replaces the spec (game data, build 26320967: `ApplyGameplayEffectSpec` exe 0x144A6A660). An application after
    /// the duration ran out, in the server frame the effect still has, is a new one.
    /// </summary>
    protected Instance Apply(SimulationContext ctx, EnemyTarget target, double? durationSeconds = null, double periodScaler = 1.0)
    {
        SameFight(ctx);
        var now = ctx.CurrentTime;
        double duration = durationSeconds ?? DurationSeconds;
        if (_active.TryGetValue(target, out var dot))
        {
            double left = Math.Max(0.0, (dot.End - now).TotalSeconds);
            double refreshed = duration + Math.Min(left, duration * RefreshCarryoverFraction);
            if (dot.Effect is { } running && running.Refresh(ctx, refreshed))
            {
                dot.End = now + SimTime.FromSeconds(refreshed);
                SetPeriodScaler(dot, periodScaler);
                target.Auras.Apply(Debuff, now);
                return dot;
            }
        }

        var fresh = new Instance(target) { End = now + SimTime.FromSeconds(duration), PeriodScaler = periodScaler };
        _active[target] = fresh;
        target.Auras.Apply(Debuff, now);
        fresh.Effect = PeriodicEffect.BeginHasted(ctx, Owner, Name, duration, BasePeriod(fresh), tickOnStart: false, partialFinalTick: true,
            (c, fraction) => Tick(c, fresh, fraction),
            (c, stopped) => Ended(c, fresh, stopped),
            damageLogId: DamageLogId, target: target, damageOverTime: DamageOverTime);
        return fresh;
    }

    /// <summary>Applies one more stack (up to <paramref name="maxStacks"/>), refreshing the duration.</summary>
    protected Instance ApplyStack(SimulationContext ctx, EnemyTarget target, int maxStacks)
    {
        SameFight(ctx);
        var before = Get(target);
        var dot = Apply(ctx, target);
        if (dot == before) dot.Stacks = Math.Min(maxStacks, dot.Stacks + 1);
        return dot;
    }

    /// <summary>Takes one stack off <paramref name="target"/>; the last one takes the effect off. Returns false when it had none.</summary>
    public bool ConsumeStack(SimulationContext ctx, EnemyTarget target)
    {
        if (!IsOn(target, ctx)) return false;
        var dot = _active[target];
        if (--dot.Stacks <= 0) Remove(ctx, target);
        return true;
    }

    private void Tick(SimulationContext ctx, Instance dot, double fraction)
    {
        if (dot.Target.IsSleeping) return;
        DealTick(ctx, dot, fraction);
        Ticked?.Invoke(ctx, dot.Target);
    }

    /// <summary>The effect of <paramref name="dot"/> ended: it ran out, or <paramref name="stopped"/> by a removal.</summary>
    private void Ended(SimulationContext ctx, Instance dot, bool stopped)
    {
        // A new application after this one's duration ran out holds the enemy's place and its debuff.
        if (_active.GetValueOrDefault(dot.Target) != dot) return;
        _active.Remove(dot.Target);
        dot.Target.Auras.Remove(Debuff, ctx.CurrentTime);
        if (!stopped) Expired?.Invoke(ctx, dot.Target);
        Removed?.Invoke(ctx, dot.Target);
    }

    /// <summary>One tick's damage on the instance's enemy; <paramref name="fraction"/> is below 1 for a partial final tick.</summary>
    protected virtual void DealTick(SimulationContext ctx, Instance dot, double fraction) =>
        DealDamage(ctx, dot.Target, damageMultiplier: fraction * dot.Multiplier * dot.Stacks);

    /// <summary>Takes the effect off <paramref name="target"/>, with no partial tick.</summary>
    public void Remove(SimulationContext ctx, EnemyTarget target)
    {
        if (_active.GetValueOrDefault(target)?.Effect is { } effect) effect.Stop(ctx);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
