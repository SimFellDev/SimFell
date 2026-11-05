namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ardeos's damage over time effects on enemies: applies, extends and ends them, and runs their ticks. Ticks come every
/// period divided by the tick rate, (1 + haste) times every tick rate bonus (Wildfire), and re-time the moment it changes.
/// The last tick of an application deals the part of a tick its time allows.
/// </summary>
public sealed class DotTracker
{
    private static readonly int HasteStats = AuraDef.MaskOf(StatType.Haste) | AuraDef.MaskOf(StatType.HasteRating);

    private readonly Ardeos _hero;
    private readonly List<DotInstance> _active = new();
    // Speed: every hit reads the effects on its target (Devouring Flame), so they are also kept per enemy.
    private readonly Dictionary<EnemyTarget, List<DotInstance>> _byTarget = new();
    private static readonly List<DotInstance> None = new();
    private SimulationContext? _fight;

    public DotTracker(Ardeos hero)
    {
        _hero = hero;
        hero.Auras.OnGained += (aura, _, now) => AuraChanged(aura, now);
        hero.Auras.OnRemoved += (aura, now) => AuraChanged(aura, now);
    }

    /// <summary>Multipliers on the tick rate of every effect (Wildfire).</summary>
    public List<Func<SimTime, double>> RateMultipliers { get; } = new();

    /// <summary>Auras whose gain or loss changes a <see cref="RateMultipliers"/> entry.</summary>
    public HashSet<string> RateAuraKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>An effect was applied or applied again to an enemy.</summary>
    public event Action<SimulationContext, DotInstance>? Applied;

    /// <summary>An enemy dies or leaves combat with these effects on it (before they end).</summary>
    public event Action<SimulationContext, EnemyTarget, IReadOnlyList<DotInstance>>? EnemyDying;

    public IReadOnlyList<DotInstance> Active => _active;

    /// <summary>
    /// Base seconds of <paramref name="effect"/>'s tick period per second: (1 + haste) if its ticks scale with haste, times
    /// every tick rate bonus.
    /// </summary>
    public double Rate(DamageOverTime effect, SimTime now)
    {
        double rate = effect.TicksScaleWithHaste ? 1.0 + _hero.GetHaste(now) : 1.0;
        foreach (var multiplier in RateMultipliers) rate *= multiplier(now);
        return rate;
    }

    /// <summary>The enemies with an effect on them.</summary>
    public IEnumerable<EnemyTarget> Targets => _byTarget.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key);

    /// <summary>Fills <paramref name="targets"/> with <see cref="Targets"/>, in the same order.</summary>
    public void TargetsInto(List<EnemyTarget> targets)
    {
        targets.Clear();
        foreach (var (target, on) in _byTarget)
        {
            if (on.Count > 0) targets.Add(target);
        }
    }

    public IReadOnlyList<DotInstance> On(EnemyTarget target)
    {
        // Speed: the reads of one hit or tick are mostly of one enemy. Its list changes in place and stays its list.
        if (ReferenceEquals(target, _lastTarget)) return _lastOn!;
        if (!_byTarget.TryGetValue(target, out var on)) return None;
        (_lastTarget, _lastOn) = (target, on);
        return on;
    }

    private EnemyTarget? _lastTarget;
    private List<DotInstance>? _lastOn;

    public IEnumerable<DotInstance> On(EnemyTarget target, DamageOverTime effect) => On(target).Where(d => d.Effect == effect);

    public bool Has(EnemyTarget target, DamageOverTime effect) => Count(target, effect) > 0;

    public int Count(EnemyTarget target, DamageOverTime effect)
    {
        // By index: a foreach over the list interface allocates its enumerator.
        var on = On(target);
        int count = 0;
        for (int i = 0; i < on.Count; i++)
        {
            if (on[i].Effect == effect) count++;
        }
        return count;
    }

    /// <summary>
    /// Applies <paramref name="effect"/> to <paramref name="target"/> for its full duration, with <paramref name="pool"/>
    /// damage to deal over it (an accumulating effect) or <paramref name="stacks"/> more stacks (a stacking one).
    /// Accumulating: the damage of the ticks left joins the new damage, dealt in the full duration's base ticks. Combat
    /// logs (yMAZVJL9hTpFjqbC fight 7, Fire Ball on 19.4 @ 73.66s): 75 a tick with 0.674 of a tick left, plus
    /// 20% of a 2365 hit, gives (50.5 + 473) / 6 = 87 a tick, as logged; the ticks keep their timing.
    /// </summary>
    public DotInstance Apply(SimulationContext ctx, DamageOverTime effect, EnemyTarget target, double pool = 0.0, int stacks = 1,
        double cinderScale = 1.0)
    {
        Begin(ctx);
        var now = ctx.CurrentTime;
        var dot = effect.Stacking == DotStacking.Concurrent ? null : On(target).FirstOrDefault(d => d.Effect == effect);
        if (dot != null)
        {
            Advance(dot, now);
            if (effect.Stacking == DotStacking.Accumulate)
            {
                // The ticks left count at the base tick rate: counted at Wildfire's rate, the frogs refreshing an effect many
                // times a second would multiply its damage (logged Fire Frogs ticks in Wildfire fit the base rate within 12%).
                double ticksLeft = dot.RemainingSeconds(now) / effect.BasePeriod;
                dot.TickAmount = (dot.TickAmount * ticksLeft + pool) / effect.BaseTicks;
            }
            if (effect.Stacking == DotStacking.Stacking) dot.Stacks = Math.Min(effect.MaxStacks, dot.Stacks + stacks);
        }
        else
        {
            dot = new DotInstance(effect, target, now)
            {
                TickAmount = pool / effect.BaseTicks, Stacks = Math.Min(effect.MaxStacks, stacks), Rate = Rate(effect, now),
            };
            _active.Add(dot);
            if (!_byTarget.TryGetValue(target, out var on)) _byTarget[target] = on = new List<DotInstance>();
            on.Add(dot);
        }
        dot.CinderScale = cinderScale;
        dot.ExpiresAt = now + SimTime.FromSeconds(effect.Duration + AuraContainer.ExpiryLagSeconds);
        Schedule(ctx, dot);
        UpdateDebuff(effect, target, now);
        Applied?.Invoke(ctx, dot);
        return dot;
    }

    /// <summary>Adds <paramref name="seconds"/> to <paramref name="dot"/>'s time left, up to <paramref name="maxRemainingSeconds"/> left.</summary>
    public void Extend(SimulationContext ctx, DotInstance dot, double seconds, double maxRemainingSeconds = double.PositiveInfinity)
    {
        if (!dot.IsActive || seconds <= 0) return;
        var now = ctx.CurrentTime;
        double remaining = Math.Min(dot.RemainingSeconds(now) + seconds, maxRemainingSeconds + AuraContainer.ExpiryLagSeconds);
        if (remaining <= dot.RemainingSeconds(now)) return;
        Advance(dot, now);
        dot.ExpiresAt = now + SimTime.FromSeconds(remaining);
        Schedule(ctx, dot);
    }

    /// <summary>Ends every effect and forgets the fight (a new iteration or replay).</summary>
    public void Reset()
    {
        foreach (var dot in _active)
        {
            dot.IsActive = false;
            dot.Event?.Cancel();
        }
        _active.Clear();
        _byTarget.Clear();
        (_lastTarget, _lastOn) = (null, null);
        _fight = null;
    }

    private void Begin(SimulationContext ctx)
    {
        if (_fight == ctx) return;
        Reset();
        _fight = ctx;
        ctx.EnemyDemising += EnemyDemising;
    }

    private void EnemyDemising(SimulationContext ctx, EnemyTarget enemy)
    {
        if (_fight != ctx) return;
        var on = On(enemy).ToList();
        if (on.Count == 0) return;
        EnemyDying?.Invoke(ctx, enemy, on);
        foreach (var dot in on) End(ctx, dot);
    }

    /// <summary>Haste or a tick rate bonus changed: every effect banks its progress at the old rate and re-times.</summary>
    private void AuraChanged(Aura aura, SimTime now)
    {
        if (_fight == null || _active.Count == 0) return;
        if (!aura.Def.Changes(HasteStats) && !RateAuraKeys.Contains(aura.Key)) return;
        foreach (var dot in _active)
        {
            double rate = Rate(dot.Effect, now);
            if (Math.Abs(dot.Rate - rate) < 1e-12) continue;
            Advance(dot, now);
            dot.Rate = rate;
            Schedule(_fight, dot);
        }
    }

    private static void Advance(DotInstance dot, SimTime now)
    {
        if (now <= dot.LastUpdate) return;
        dot.Progress += (now - dot.LastUpdate).TotalSeconds * dot.Rate;
        dot.LastUpdate = now;
    }

    /// <summary>Queues the next tick, or the end when it comes first.</summary>
    private void Schedule(SimulationContext ctx, DotInstance dot)
    {
        double toTick = Math.Max(0.0, dot.Effect.BasePeriod - dot.Progress) / dot.Rate;
        var tickAt = dot.LastUpdate + SimTime.FromSeconds(toTick);
        bool ticks = tickAt < dot.ExpiresAt;
        var time = ticks ? tickAt : dot.ExpiresAt;
        var action = ticks ? dot.TickAction ??= c => TickDue(c, dot) : dot.ExpireAction ??= c => Expire(c, dot);
        // Speed: the queued event moves, so the queue keeps no cancelled copy of it.
        if (dot.Event is { } ev && ctx.Reschedule(ref ev, time, action))
        {
            dot.Event = ev;
            return;
        }
        dot.Event = ctx.ScheduleAt(time, action, dot.Effect.Name);
    }

    private void TickDue(SimulationContext ctx, DotInstance dot)
    {
        if (!dot.IsActive) return;
        var now = ctx.CurrentTime;
        Advance(dot, now);
        dot.Progress = 0.0;
        dot.LastTickAt = now;
        dot.Effect.Tick(ctx, dot, 1.0);
        if (dot.IsActive) Schedule(ctx, dot);
    }

    private void Expire(SimulationContext ctx, DotInstance dot)
    {
        if (!dot.IsActive) return;
        var now = ctx.CurrentTime;
        Advance(dot, now);
        double fraction = Math.Min(1.0, dot.Progress / dot.Effect.BasePeriod);
        // As every hasted effect: no partial tick within a few server frames of the last one (PeriodicEffect).
        if (dot.Effect.PartialFinalTick && fraction > 0 && (now - dot.LastTickAt).TotalSeconds >= PeriodicEffect.MinHastedPartialTickSeconds - 1e-9)
        {
            dot.Effect.Tick(ctx, dot, fraction);
        }
        End(ctx, dot);
    }

    private void End(SimulationContext ctx, DotInstance dot)
    {
        if (!dot.IsActive) return;
        dot.IsActive = false;
        dot.Event?.Cancel();
        _active.Remove(dot);
        if (_byTarget.TryGetValue(dot.Target, out var on)) on.Remove(dot);
        UpdateDebuff(dot.Effect, dot.Target, ctx.CurrentTime);
    }

    /// <summary>The enemy's debuff shows the effect's instances (or stacks) while any is active.</summary>
    private void UpdateDebuff(DamageOverTime effect, EnemyTarget target, SimTime now)
    {
        var on = On(target);
        int stacks = 0;
        for (int i = 0; i < on.Count; i++)
        {
            if (on[i].Effect == effect) stacks += on[i].Stacks;
        }
        if (stacks == 0) target.Auras.Remove(effect.Debuff, now);
        else target.Auras.Apply(effect.Debuff, now, setStacks: stacks);
    }
}
