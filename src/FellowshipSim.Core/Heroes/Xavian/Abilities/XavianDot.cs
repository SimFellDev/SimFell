namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// A damage over time of Xavian's that deals a share of a hit's damage (Solar Burn's Sunburn, Sun's Touch): one instance on
/// each enemy. A tick deals the pool times the period over the duration, every hasted period, with a partial final tick
/// for the hasted progress left; the ticks neither crit nor spread. Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 74.748,
/// Sunburn): 7 ticks of 593 and a partial tick of 342 over 9.02s at about 20% Haste.
/// </summary>
public sealed class XavianDot(Xavian owner, int effectId, string name, double periodSeconds, double durationSeconds)
{
    public int DamageLogId { get; } = LogIds.Effect(effectId);
    public string Name { get; } = name;
    public double PeriodSeconds { get; } = periodSeconds;
    public double DurationSeconds { get; } = durationSeconds;

    /// <summary>The debuff on the enemy while the effect is on it.</summary>
    public AuraDef Debuff { get; } = AuraDef.Effect(name, effectId, durationSeconds);

    private sealed class Instance(EnemyTarget target)
    {
        public EnemyTarget Target { get; } = target;
        public SimTime End;
        public SimTime LastTick;
        public double PerTick;
        public HastedTimer? Timer;
        public ScheduledEvent? EndEvent;
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

    /// <summary>
    /// Puts <paramref name="damage"/> on <paramref name="target"/> over the duration. On an enemy that has the effect, the
    /// damage its ticks have not dealt joins the new damage, the duration restarts and the ticks keep their timing.
    /// UNVERIFIED: how a refresh adds the damage left.
    /// </summary>
    public void Apply(SimulationContext ctx, EnemyTarget target, double damage)
    {
        if (damage <= 0) return;
        SameFight(ctx);
        var now = ctx.CurrentTime;
        double ticksPerDuration = DurationSeconds / PeriodSeconds;
        if (_active.TryGetValue(target, out var dot))
        {
            double left = Math.Max(0.0, (dot.End - now).TotalSeconds) / PeriodSeconds * dot.PerTick;
            dot.PerTick = (damage + left) / ticksPerDuration;
            dot.End = now + SimTime.FromSeconds(DurationSeconds);
            ScheduleEnd(ctx, dot);
            target.Auras.Apply(Debuff, now);
            return;
        }
        dot = new Instance(target) { End = now + SimTime.FromSeconds(DurationSeconds), LastTick = now, PerTick = damage / ticksPerDuration };
        _active[target] = dot;
        target.Auras.Apply(Debuff, now);
        owner.TriggerHarmfulEffectApplied(ctx, target);
        ScheduleTick(ctx, dot);
        ScheduleEnd(ctx, dot);
    }

    private void ScheduleTick(SimulationContext ctx, Instance dot)
    {
        dot.Timer = HastedTimer.Start(ctx, owner, PeriodSeconds, c =>
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
            double fraction = dot.Timer is { IsActive: true } timer ? timer.ElapsedBaseSeconds / PeriodSeconds : 0.0;
            if (fraction > 0 && (c.CurrentTime - dot.LastTick).TotalSeconds >= PeriodicEffect.MinHastedPartialTickSeconds - 1e-9)
            {
                Tick(c, dot, Math.Min(fraction, 1.0));
            }
            Remove(c, dot.Target);
        }, Name);
    }

    private void Tick(SimulationContext ctx, Instance dot, double fraction)
    {
        if (dot.Target.IsSleeping) return;
        ctx.ApplyDamage(owner, dot.Target, dot.PerTick * fraction, DamageLogId, Name, canCrit: false, 0.0, 1.0, forcedCrit: false);
    }

    private void Remove(SimulationContext ctx, EnemyTarget target)
    {
        if (!_active.Remove(target, out var dot)) return;
        dot.Timer?.Cancel();
        dot.EndEvent?.Cancel();
        target.Auras.Remove(Debuff, ctx.CurrentTime);
    }
}
