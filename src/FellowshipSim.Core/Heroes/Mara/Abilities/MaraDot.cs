namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// A damage-over-time effect of Mara's (Hemorrhage, Seething Poison, Hemotoxin, Vexira's Venom): one instance on each
/// enemy, ticking every hasted period until it runs out, with a partial final tick for the hasted progress left. It is
/// not an action an action list uses: its ability applies it.
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

    /// <summary>Tick rate bonuses ("ticks faster"), added together.</summary>
    public List<Func<SimTime, double>> TickRateBonuses { get; } = new();

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
        public SimTime End;
        public SimTime LastTick;
        public HastedTimer? Timer;
        public ScheduledEvent? EndEvent;
        /// <summary>The ability's own damage multiplier for this application (e.g. Vexira's Venom's stored damage).</summary>
        public double Multiplier = 1.0;
        public int Stacks = 1;
    }

    private readonly Dictionary<EnemyTarget, Instance> _active = new();
    private SimulationContext? _fight;

    private void SameFight(SimulationContext ctx)
    {
        if (_fight == ctx) return;
        _active.Clear();
        ctx.EnemyDemising += (c, enemy) => Remove(c, enemy, expired: false);
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

    /// <summary>Unhasted seconds between two ticks, with the tick rate bonuses.</summary>
    protected double HastedPeriod(SimTime now)
    {
        double bonus = 0.0;
        foreach (var b in TickRateBonuses) bonus += b(now);
        return PeriodSeconds / (1.0 + bonus);
    }

    /// <summary>
    /// Applies the effect to <paramref name="target"/> for <paramref name="durationSeconds"/> (its own duration when
    /// null). On an enemy that has it, the duration restarts with up to <see cref="RefreshCarryoverFraction"/> of it
    /// carried over, and the ticks keep their timing.
    /// </summary>
    protected Instance Apply(SimulationContext ctx, EnemyTarget target, double? durationSeconds = null)
    {
        SameFight(ctx);
        var now = ctx.CurrentTime;
        double duration = durationSeconds ?? DurationSeconds;
        if (_active.TryGetValue(target, out var dot))
        {
            double left = Math.Max(0.0, (dot.End - now).TotalSeconds);
            dot.End = now + SimTime.FromSeconds(duration + Math.Min(left, duration * RefreshCarryoverFraction));
            ScheduleEnd(ctx, dot);
            target.Auras.Apply(Debuff, now);
            return dot;
        }

        dot = new Instance(target) { End = now + SimTime.FromSeconds(duration), LastTick = now };
        _active[target] = dot;
        target.Auras.Apply(Debuff, now);
        ScheduleTick(ctx, dot);
        ScheduleEnd(ctx, dot);
        return dot;
    }

    /// <summary>Applies one more stack (up to <paramref name="maxStacks"/>), refreshing the duration.</summary>
    protected Instance ApplyStack(SimulationContext ctx, EnemyTarget target, int maxStacks)
    {
        bool had = IsOn(target, ctx);
        var dot = Apply(ctx, target);
        if (had) dot.Stacks = Math.Min(maxStacks, dot.Stacks + 1);
        return dot;
    }

    /// <summary>Takes one stack off <paramref name="target"/>; the last one takes the effect off. Returns false when it had none.</summary>
    public bool ConsumeStack(SimulationContext ctx, EnemyTarget target)
    {
        if (!IsOn(target, ctx)) return false;
        var dot = _active[target];
        if (--dot.Stacks <= 0) Remove(ctx, target, expired: false);
        return true;
    }

    private void ScheduleTick(SimulationContext ctx, Instance dot)
    {
        dot.Timer = HastedTimer.Start(ctx, Owner, HastedPeriod(ctx.CurrentTime), c =>
        {
            if (_active.GetValueOrDefault(dot.Target) != dot || c.CurrentTime > dot.End) return;
            dot.LastTick = c.CurrentTime;
            Tick(c, dot, 1.0);
            if (_active.GetValueOrDefault(dot.Target) == dot) ScheduleTick(c, dot);
        }, Name);
    }

    private void ScheduleEnd(SimulationContext ctx, Instance dot)
    {
        dot.EndEvent?.Cancel();
        dot.EndEvent = ctx.ScheduleAt(dot.End, c =>
        {
            if (_active.GetValueOrDefault(dot.Target) != dot) return;
            // The hasted progress toward the next tick is a partial tick, when enough time passed since the last one.
            double fraction = dot.Timer is { IsActive: true } timer ? timer.ElapsedBaseSeconds / HastedPeriod(c.CurrentTime) : 0.0;
            if (fraction > 0 && (c.CurrentTime - dot.LastTick).TotalSeconds >= PeriodicEffect.MinHastedPartialTickSeconds - 1e-9)
            {
                Tick(c, dot, Math.Min(fraction, 1.0));
            }
            Remove(c, dot.Target, expired: true);
        }, Name);
    }

    private void Tick(SimulationContext ctx, Instance dot, double fraction)
    {
        if (dot.Target.IsSleeping) return;
        DealTick(ctx, dot, fraction);
        Ticked?.Invoke(ctx, dot.Target);
    }

    /// <summary>One tick's damage on the instance's enemy; <paramref name="fraction"/> is below 1 for a partial final tick.</summary>
    protected virtual void DealTick(SimulationContext ctx, Instance dot, double fraction) =>
        DealDamage(ctx, dot.Target, damageMultiplier: fraction * dot.Multiplier * dot.Stacks);

    /// <summary>Takes the effect off <paramref name="target"/>.</summary>
    public void Remove(SimulationContext ctx, EnemyTarget target, bool expired)
    {
        if (!_active.Remove(target, out var dot)) return;
        dot.Timer?.Cancel();
        dot.EndEvent?.Cancel();
        target.Auras.Remove(Debuff, ctx.CurrentTime);
        if (expired) Expired?.Invoke(ctx, target);
        Removed?.Invoke(ctx, target);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
