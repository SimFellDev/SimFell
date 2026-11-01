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
        public double PerTick;
        public PeriodicEffect? Effect;
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
    /// damage left (the old tick times the time left over the period) joins the new damage and the duration restarts.
    /// Combat logs (XyGpcHjNaAzgk7mW fight 20, Sunburn refreshes 39.621-69.472): the old tick's share of the new one is
    /// 0.44-0.55, against 0.43-0.55 from the time left.
    /// </summary>
    public void Apply(SimulationContext ctx, EnemyTarget target, double damage)
    {
        if (damage <= 0) return;
        SameFight(ctx);
        var now = ctx.CurrentTime;
        double ticksPerDuration = DurationSeconds / PeriodSeconds;
        double left = 0.0;
        if (_active.Remove(target, out var old))
        {
            left = Math.Max(0.0, (old.End - now).TotalSeconds) / PeriodSeconds * old.PerTick;
            old.Effect?.Stop(ctx);
        }
        else
        {
            owner.TriggerHarmfulEffectApplied(ctx, target);
        }
        var dot = new Instance(target) { End = now + SimTime.FromSeconds(DurationSeconds), PerTick = (damage + left) / ticksPerDuration };
        _active[target] = dot;
        target.Auras.Apply(Debuff, now);
        // Effect 2660 is tagged EffectType.Dot: its ticks run on the game's period timer.
        dot.Effect = PeriodicEffect.BeginHasted(ctx, owner, Name, DurationSeconds, PeriodSeconds, tickOnStart: false, partialFinalTick: true,
            onTick: (c, fraction) =>
            {
                if (dot.Target.IsSleeping) return;
                c.ApplyDamage(owner, dot.Target, dot.PerTick * fraction, DamageLogId, Name, canCrit: false, 0.0, 1.0, forcedCrit: false);
            },
            onEnd: (c, _) =>
            {
                if (_active.GetValueOrDefault(dot.Target) != dot) return;
                _active.Remove(dot.Target);
                dot.Target.Auras.Remove(Debuff, c.CurrentTime);
            },
            damageLogId: DamageLogId, target: target, damageOverTime: true);
    }

    private void Remove(SimulationContext ctx, EnemyTarget target)
    {
        if (!_active.Remove(target, out var dot)) return;
        dot.Effect?.Stop(ctx);
        target.Auras.Remove(Debuff, ctx.CurrentTime);
    }
}
