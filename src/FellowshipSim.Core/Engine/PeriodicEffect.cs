namespace FellowshipSim.Core.Engine;

/// <summary>
/// A repeating effect on the event queue: channel ticks, damage-over-time pulses, periodic resource gains.
/// Ticks fire every <see cref="Period"/> from the start until the effect's end. Optionally one tick fires
/// immediately on start, and a final partial tick fires at the end for the leftover fraction of a period.
/// Stopping the effect cancels every tick that has not fired yet.
/// </summary>
/// <remarks>
/// An effect with a <see cref="DamageLogId"/> asks <see cref="IOutcomeSource.TickAt"/> for each tick's firing time once the
/// tick before it has fired. A simulation answers with the engine's own time, so the ticks keep the schedule they have
/// without a damage log ID.
/// </remarks>
public sealed class PeriodicEffect
{
    // Speed: sized for the events the effect schedules at its start, so the list does not grow through each size.
    private readonly List<ScheduledEvent> _events;
    private readonly Action<SimulationContext, double> _onTick;
    private readonly Action<SimulationContext, bool>? _onEnd;
    private readonly Hero? _hastedBy;

    private PeriodicEffect(string name, SimTime start, SimTime end, double periodSeconds,
        Action<SimulationContext, double> onTick, Action<SimulationContext, bool>? onEnd, Hero source, int damageLogId,
        EnemyTarget? target, Hero? hastedBy, int events)
    {
        _events = new List<ScheduledEvent>(events);
        Name = name;
        Start = start;
        End = end;
        Period = periodSeconds;
        _onTick = onTick;
        _onEnd = onEnd;
        Source = source;
        DamageLogId = damageLogId;
        Target = target;
        _hastedBy = hastedBy;
        LastTickTime = start;
    }

    public string Name { get; }
    public SimTime Start { get; }
    public SimTime End { get; private set; }
    public double Period { get; private set; }
    public int TicksFired { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>The hero whose effect this is.</summary>
    public Hero Source { get; }
    /// <summary>The damage ID of the effect's tick hits (0 for an effect that deals no damage of its own, e.g. resource gains).</summary>
    public int DamageLogId { get; }
    /// <summary>The enemy every tick hits; null for a pulse on every enemy in its area.</summary>
    public EnemyTarget? Target { get; }
    /// <summary>True for an effect whose ticks keep hitting <see cref="Target"/> after it dies, until the effect runs out.</summary>
    public bool TicksAfterTargetDies { get; set; }
    /// <summary>When the latest tick fired (the start before the first).</summary>
    public SimTime LastTickTime { get; private set; }

    /// <summary>Seconds between full ticks at <paramref name="now"/> (the hasted period for <see cref="BeginHasted"/>).</summary>
    public double PeriodSecondsAt(SimTime now) => _hastedBy == null ? Period : Period / (1.0 + _hastedBy.GetHaste(now));

    /// <summary>
    /// The engine's predicted time of each tick with a damage log ID. For <see cref="Begin"/> it is the previous tick's
    /// time plus the period. For <see cref="BeginHasted"/> it is the fire time of the tick's <see cref="HastedTimer"/>,
    /// started at the previous tick.
    /// </summary>
    public event Action<SimulationContext, PeriodicEffect, SimTime>? TickPredicted;

    /// <summary>Raised once when the effect ends, naturally or stopped early.</summary>
    public event Action<SimulationContext, PeriodicEffect>? Ended;

    /// <summary>
    /// The game runs on server frames; a partial final tick shorter than one frame never happens
    /// (combat logs: Bursting Ice @ 29.587 in yMAZVJL9hTpFjqbC fight 7 ends shortly after its last full pulse, with no partial pulse).
    /// </summary>
    public const double ServerFrameSeconds = 1.0 / 30.0;

    /// <summary>
    /// The game setting for the shortest partial tick, seconds: when a damage over
    /// time's duration runs out, its partial final tick (Dot and Hot effects alone) is skipped when elapsed =
    /// the time its period timer has run, the time since the last tick was due at the hasted rate, is
    /// under this (strict: equal runs), and its factor is elapsed / rate.
    /// </summary>
    public const double MinHastedPartialTickSeconds = 0.1;

    private HastedTimer? _nextHastedTick;

    /// <summary>The period timer of a damage over time (<see cref="BeginHasted"/> with damageOverTime).</summary>
    public HastedTimer? PeriodTimer => _nextHastedTick;

    // Ticks of a Begin effect with a damage log ID, in firing order. A tick's scheduled event is _events[tick - _slotOffset]
    // (the tick on start has none); the last tick has _partialFraction when it is a partial tick.
    private int _slotCount;
    private int _slotOffset;
    private double _partialFraction;
    private int _nextSlot;
    private SimTime _lastScheduled;
    private Action<SimulationContext>? _fireSlot;

    /// <summary>A tick moved to the time <see cref="IOutcomeSource.TickAt"/> gave; the effect ends after it.</summary>
    private ScheduledEvent? _movedTick;

    private void AddMovedTick(ScheduledEvent tick)
    {
        _events.Add(tick);
        _movedTick = tick;
    }

    /// <param name="onTick">Called per tick with the tick's fraction of a full tick (1.0, or less for a partial final tick).</param>
    /// <param name="onEnd">Called once when the effect ends; the flag is true when it was stopped early.</param>
    /// <param name="tickDelaySeconds">
    /// Every tick fires this much after its scheduled time (but no later than the end); tick counts and the partial
    /// final tick's fraction still follow the schedule.
    /// </param>
    /// <param name="damageLogId">The damage ID of the tick hits; 0 for an effect that deals no damage of its own.</param>
    /// <param name="target">The enemy every tick hits; null for a pulse on every enemy in its area.</param>
    /// <param name="minPartialSeconds">The shortest leftover that gives a partial final tick.</param>
    public static PeriodicEffect Begin(
        SimulationContext ctx,
        string name,
        double durationSeconds,
        double periodSeconds,
        bool tickOnStart,
        bool partialFinalTick,
        Action<SimulationContext, double> onTick,
        Action<SimulationContext, bool>? onEnd = null,
        double tickDelaySeconds = 0.0,
        int damageLogId = 0,
        EnemyTarget? target = null,
        double minPartialSeconds = ServerFrameSeconds)
    {
        if (periodSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(periodSeconds));

        var start = ctx.CurrentTime;
        var end = start + SimTime.FromSeconds(durationSeconds);
        int fullTicks = (int)Math.Floor((durationSeconds + 1e-6) / periodSeconds);
        int firstTick = tickOnStart && tickDelaySeconds > 0 ? 0 : 1;
        double remainder = durationSeconds - fullTicks * periodSeconds;
        bool partial = partialFinalTick && remainder >= Math.Max(minPartialSeconds, 1e-6);
        bool tickNow = tickOnStart && tickDelaySeconds <= 0;
        // The ticks, the partial tick and the end, and a tick moved by the outcome source.
        int events = Math.Max(0, fullTicks - firstTick + 1) + (partial ? 1 : 0) + 1 + (damageLogId != 0 ? 1 : 0);
        var effect = new PeriodicEffect(name, start, end, periodSeconds, onTick, onEnd, ctx.Player, damageLogId, target, hastedBy: null,
            events);
        if (damageLogId != 0)
        {
            effect.BeginSlots(ctx, fullTicks, firstTick, remainder, partial, tickNow, durationSeconds, tickDelaySeconds);
            return effect;
        }

        // One handler for every full tick.
        Action<SimulationContext> fullTick = c => effect.Tick(c, 1.0);
        for (int k = firstTick; k <= fullTicks; k++)
        {
            var at = start + SimTime.FromSeconds(Math.Min(k * periodSeconds + tickDelaySeconds, durationSeconds));
            effect._events.Add(ctx.Kernel.ScheduleAt(at, fullTick, name));
        }

        if (partial)
        {
            double fraction = remainder / periodSeconds;
            effect._events.Add(ctx.Kernel.ScheduleAt(end, c => effect.Tick(c, fraction), name));
        }

        effect._events.Add(ctx.Kernel.ScheduleAt(end, c => effect.Finish(c, clipped: false), name));

        if (tickNow)
        {
            effect.Tick(ctx, 1.0);
        }

        return effect;
    }

    /// <summary>
    /// The ticks of a <see cref="Begin"/> effect with a damage log ID: the same events in the same order as without one,
    /// each moved to the time <see cref="IOutcomeSource.TickAt"/> gives once the tick before it has fired.
    /// </summary>
    private void BeginSlots(SimulationContext ctx, int fullTicks, int firstTick, double remainder, bool partial, bool tickNow,
        double durationSeconds, double tickDelaySeconds)
    {
        _slotOffset = tickNow ? 1 : 0;
        _slotCount = _slotOffset + Math.Max(0, fullTicks - firstTick + 1) + (partial ? 1 : 0);
        _partialFraction = partial ? remainder / Period : 0.0;
        _lastScheduled = Start;
        _fireSlot = FireSlot;
        for (int k = firstTick; k <= fullTicks; k++)
        {
            var at = Start + SimTime.FromSeconds(Math.Min(k * Period + tickDelaySeconds, durationSeconds));
            _events.Add(ctx.Kernel.ScheduleAt(at, _fireSlot, Name));
        }
        if (partial) _events.Add(ctx.Kernel.ScheduleAt(End, _fireSlot, Name));
        _events.Add(ctx.Kernel.ScheduleAt(End, FinishAtEnd, Name));
        if (_slotCount > 0) AskSlot(ctx, 0);
    }

    /// <summary>The event tick <paramref name="slot"/> was scheduled with; null for the tick on start.</summary>
    private ScheduledEvent? SlotEvent(int slot) => slot < _slotOffset ? null : _events[slot - _slotOffset];

    private SimTime SlotTime(int slot) => SlotEvent(slot)?.Time ?? Start;

    /// <summary>Asks the firing time of tick <paramref name="slot"/> and moves its event there.</summary>
    private void AskSlot(SimulationContext ctx, int slot)
    {
        var scheduled = SlotTime(slot);
        double fraction = slot == _slotCount - 1 && _partialFraction > 0 ? _partialFraction : 1.0;
        var at = AskTick(ctx, scheduled, fraction);
        // Predicted: the previous tick plus the period, i.e. the schedule shifted by the previous tick's move.
        TickPredicted?.Invoke(ctx, this, SimTime.FromMilliseconds(scheduled.Milliseconds + LastTickTime.Milliseconds - _lastScheduled.Milliseconds));
        // Ticks keep their order: a tick moves no later than the next one's scheduled time.
        if (slot + 1 < _slotCount && at >= SlotTime(slot + 1)) at = scheduled;
        var original = SlotEvent(slot);
        if (at == scheduled)
        {
            // A tick on start fires now.
            if (original == null) FireSlot(ctx);
            return;
        }
        original?.Cancel();
        AddMovedTick(ctx.ScheduleAt(at, _fireSlot!, Name));
    }

    private void FireSlot(SimulationContext ctx)
    {
        if (!IsActive) return;
        int slot = _nextSlot++;
        LastTickTime = ctx.CurrentTime;
        _lastScheduled = SlotTime(slot);
        Tick(ctx, slot == _slotCount - 1 && _partialFraction > 0 ? _partialFraction : 1.0);
        if (IsActive && _nextSlot < _slotCount) AskSlot(ctx, _nextSlot);
    }

    private void FinishAtEnd(SimulationContext ctx)
    {
        // A tick moved past the end fires before the effect ends.
        if (_movedTick is { IsPending: true } moved && moved.Time >= ctx.CurrentTime)
        {
            _events.Add(ctx.ScheduleAt(moved.Time, FinishAtEnd, Name));
            return;
        }
        Finish(ctx, clipped: false);
    }

    /// <summary>The firing time <see cref="IOutcomeSource.TickAt"/> gives for a tick scheduled at <paramref name="scheduled"/>.</summary>
    private SimTime AskTick(SimulationContext ctx, SimTime scheduled, double fraction)
    {
        var outer = ctx.CurrentPeriodicEffect;
        ctx.CurrentPeriodicEffect = this;
        try { return ctx.Outcomes.TickAt(new TickRequest(Source, Target, DamageLogId, Name, scheduled, fraction), ctx); }
        finally { ctx.CurrentPeriodicEffect = outer; }
    }

    /// <summary>
    /// Like <see cref="Begin"/>, but each tick comes after <paramref name="basePeriodSeconds"/> of the hero's hasted time,
    /// so the ticks speed up or slow down the moment haste changes (combat logs: Bursting Ice pulses re-time when
    /// Inspired Allegiance ends mid-debuff). The duration is fixed; the partial final tick is the hasted progress left.
    /// </summary>
    /// <remarks>
    /// With a damage log ID, a tick that <see cref="IOutcomeSource.TickAt"/> moves fires at the time given. Its
    /// <see cref="HastedTimer"/> still runs from the previous tick to its own fire time, which raises
    /// <see cref="TickPredicted"/> and fires no tick. A damage over time's period timer moves its tick to the time given,
    /// and its next period runs from there.
    /// </remarks>
    /// <param name="damageOverTime">
    /// For an effect tagged EffectType.Dot or EffectType.Hot with a damage log ID: its ticks run on the game's period timer
    /// (<see cref="HastedTimer.StartPeriod"/>), and a haste change inside a tick repeats it a server frame later.
    /// </param>
    public static PeriodicEffect BeginHasted(
        SimulationContext ctx,
        Hero hero,
        string name,
        double durationSeconds,
        double basePeriodSeconds,
        bool tickOnStart,
        bool partialFinalTick,
        Action<SimulationContext, double> onTick,
        Action<SimulationContext, bool>? onEnd = null,
        bool retimeOnHasteChange = true,
        int damageLogId = 0,
        EnemyTarget? target = null,
        bool damageOverTime = false)
    {
        if (damageOverTime && damageLogId == 0) throw new ArgumentException("A damage over time needs its damage log ID.", nameof(damageLogId));
        if (basePeriodSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(basePeriodSeconds));

        var start = ctx.CurrentTime;
        // Like any timed aura, the effect ends on the server frame after its duration runs out (see AuraContainer.ExpiryLagSeconds).
        var nominalEnd = start + SimTime.FromSeconds(durationSeconds);
        var end = nominalEnd + SimTime.FromSeconds(AuraContainer.ExpiryLagSeconds);
        // A refresh moves both ends.
        // A moved tick; a fixed period also has its ticks, counted at no haste.
        int events = 2 + (retimeOnHasteChange || damageOverTime ? 0 : (int)Math.Ceiling(durationSeconds / basePeriodSeconds));
        var effect = new PeriodicEffect(name, start, end, basePeriodSeconds, onTick, onEnd, hero, damageLogId, target, hastedBy: hero,
            events);
        var lastTick = start;
        // One handler for every tick, made once.
        Action<SimulationContext>? fire = null;
        Action<SimulationContext>? fireMoved = null;
        Action<SimulationContext>? predict = null;
        Action<SimulationContext>? periodTick = null;
        Action<SimulationContext>? askPeriodTick = null;

        void Fire(SimulationContext cc)
        {
            if (cc.CurrentTime > nominalEnd || !effect.IsActive) return;
            effect.TickPredicted?.Invoke(cc, effect, cc.CurrentTime);
            FireTick(cc);
        }

        void FireTick(SimulationContext cc)
        {
            if (cc.CurrentTime > nominalEnd || !effect.IsActive) return;
            lastTick = cc.CurrentTime;
            effect.LastTickTime = lastTick;
            effect.Tick(cc, 1.0);
            if (effect.IsActive) ScheduleNext(cc);
        }

        void Predict(SimulationContext cc)
        {
            if (effect.IsActive) effect.TickPredicted?.Invoke(cc, effect, cc.CurrentTime);
        }

        // A damage over time ticks on its period timer, a repeated tick included.
        bool tickMoved = false;
        HastedTimer? predictor = null;
        void PeriodTick(SimulationContext cc)
        {
            if (cc.CurrentTime > nominalEnd || !effect.IsActive) return;
            if (!tickMoved) effect.TickPredicted?.Invoke(cc, effect, cc.CurrentTime);
            tickMoved = false;
            lastTick = cc.CurrentTime;
            effect.LastTickTime = lastTick;
            effect.Tick(cc, 1.0);
        }

        // Once the period timer has its next tick, asks its firing time and moves the tick there.
        void AskPeriodTick(SimulationContext c)
        {
            if (!effect.IsActive || effect._nextHastedTick is not { IsActive: true } timer) return;
            var due = timer.DueTime;
            var at = effect.AskTick(c, due, 1.0);
            if (at == due) return;
            // The tick fires at the time given; a timer from the last tick predicts it (a repeated tick is predicted a
            // server frame after the last).
            tickMoved = true;
            if (timer.RepeatPending) effect.TickPredicted?.Invoke(c, effect, due);
            else predictor = HastedTimer.Start(c, hero, basePeriodSeconds, predict ??= Predict, name);
            timer.MoveNextTick(at);
        }

        void ScheduleNext(SimulationContext c)
        {
            fire ??= Fire;
            if (damageOverTime)
            {
                if (effect._nextHastedTick is { IsActive: true }) return;
                effect._nextHastedTick = HastedTimer.StartPeriod(c, hero, basePeriodSeconds, periodTick ??= PeriodTick, name, damageLogId,
                    askPeriodTick ??= AskPeriodTick);
                AskPeriodTick(c);
                return;
            }
            if (retimeOnHasteChange)
            {
                var timer = HastedTimer.Start(c, hero, basePeriodSeconds, fire, name);
                effect._nextHastedTick = timer;
                if (damageLogId == 0) return;
                var due = timer.DueTime;
                var at = effect.AskTick(c, due, 1.0);
                if (at == due) return;
                // The tick fires at the time given; the timer only predicts.
                timer.Cancel();
                effect._nextHastedTick = HastedTimer.Start(c, hero, basePeriodSeconds, predict ??= Predict, name);
                effect.AddMovedTick(c.ScheduleAt(at, fireMoved ??= FireTick, name));
                return;
            }
            // The period is fixed by the haste when the previous tick fired.
            var next = c.CurrentTime + SimTime.FromSeconds(basePeriodSeconds / (1.0 + hero.GetHaste(c.CurrentTime)));
            if (damageLogId != 0)
            {
                var at = effect.AskTick(c, next, 1.0);
                if (at != next)
                {
                    effect._events.Add(c.Kernel.ScheduleAt(next, predict ??= Predict, name));
                    effect.AddMovedTick(c.ScheduleAt(at, fireMoved ??= FireTick, name));
                    return;
                }
            }
            effect._events.Add(c.Kernel.ScheduleAt(next, fire, name));
        }

        // The duration runs out at the nominal end (no more ticks; the partial tick is measured there) ...
        double partial = 0.0;
        void DurationRunsOut(SimulationContext c)
        {
            if (!effect.IsActive) return;
            var timer = effect._nextHastedTick;
            double fraction = timer is { IsActive: true } ? timer.ElapsedBaseSeconds / basePeriodSeconds : 0.0;
            // A damage over time's partial tick reads how long its period has run (period - remaining) as the
            // duration runs out: a re-arm since the last tick shortens it.
            double elapsed = damageOverTime && timer is { IsActive: true }
                ? timer.ElapsedBaseSeconds / (1.0 + hero.GetHaste(c.CurrentTime))
                : (c.CurrentTime - lastTick).TotalSeconds;
            timer?.Cancel();
            if (partialFinalTick && fraction > 0 && elapsed >= MinHastedPartialTickSeconds - 1e-9)
            {
                partial = Math.Min(fraction, 1.0);
            }
        }
        // ... and the partial tick and the end happen on the next server frame.
        Action<SimulationContext>? finishAfterPartial = null;
        void EndFrame(SimulationContext c)
        {
            if (!effect.IsActive) return;
            if (partial > 0)
            {
                double fraction = partial;
                partial = 0.0;
                if (damageLogId != 0)
                {
                    var at = effect.AskTick(c, c.CurrentTime, fraction);
                    effect.TickPredicted?.Invoke(c, effect, c.CurrentTime);
                    if (at != c.CurrentTime)
                    {
                        // The partial tick fires at the time given, and the effect ends after it.
                        finishAfterPartial = cc =>
                        {
                            if (!effect.IsActive) return;
                            effect.LastTickTime = cc.CurrentTime;
                            effect.Tick(cc, fraction);
                            effect.Finish(cc, clipped: false);
                        };
                        effect._events.Add(c.ScheduleAt(at, finishAfterPartial, name));
                        return;
                    }
                }
                effect.LastTickTime = c.CurrentTime;
                effect.Tick(c, fraction);
            }
            // A full tick moved past the end fires before the effect ends.
            if (effect._movedTick is { IsPending: true } moved && moved.Time >= c.CurrentTime)
            {
                effect._events.Add(c.ScheduleAt(moved.Time, cc => effect.Finish(cc, clipped: false), name));
                return;
            }
            effect.Finish(c, clipped: false);
        }
        ScheduledEvent? durationEvent = null, endEvent = null;
        void ScheduleEnd(SimulationContext c)
        {
            durationEvent?.Cancel();
            endEvent?.Cancel();
            durationEvent = c.Kernel.ScheduleAt(nominalEnd, DurationRunsOut, name);
            effect._events.Add(durationEvent.Value);
            endEvent = c.Kernel.ScheduleAt(end, EndFrame, name);
            effect._events.Add(endEvent.Value);
        }
        ScheduleEnd(ctx);
        // A refresh restarts the duration and keeps the period timer and the tick it has next.
        effect._refresh = (c, seconds) =>
        {
            nominalEnd = c.CurrentTime + SimTime.FromSeconds(seconds);
            end = nominalEnd + SimTime.FromSeconds(AuraContainer.ExpiryLagSeconds);
            effect.End = end;
            ScheduleEnd(c);
        };
        effect._changePeriod = seconds =>
        {
            basePeriodSeconds = seconds;
            effect.Period = seconds;
            effect._nextHastedTick?.ChangeBaseSeconds(seconds);
            predictor?.ChangeBaseSeconds(seconds);
        };

        if (tickOnStart)
        {
            var at = damageLogId == 0 ? start : effect.AskTick(ctx, start, 1.0);
            if (at == start)
            {
                effect.TickPredicted?.Invoke(ctx, effect, start);
                effect.Tick(ctx, 1.0);
            }
            else
            {
                // The first tick fires at the time given; the next is asked for once it has.
                effect.TickPredicted?.Invoke(ctx, effect, start);
                effect.AddMovedTick(ctx.ScheduleAt(at, fireMoved ??= FireTick, name));
                return effect;
            }
        }
        if (effect.IsActive) ScheduleNext(ctx);
        return effect;
    }

    private Action<SimulationContext, double>? _refresh;
    private Action<double>? _changePeriod;

    /// <summary>
    /// A new unhasted period for a running <see cref="BeginHasted"/> effect: the time left to the next tick keeps its fraction
    /// of the period.
    /// </summary>
    public void ChangePeriod(double basePeriodSeconds) => _changePeriod?.Invoke(basePeriodSeconds);

    /// <summary>
    /// Restarts the duration of a <see cref="BeginHasted"/> effect: it now runs out <paramref name="durationSeconds"/> from now.
    /// The period timer runs on, so the next tick comes when it was due. Returns false, changing nothing, for an effect
    /// that ended or whose duration already ran out (its last tick is under way), or one from <see cref="Begin"/>.
    /// </summary>
    public bool Refresh(SimulationContext ctx, double durationSeconds)
    {
        if (!IsActive || _refresh == null || ctx.CurrentTime >= End - SimTime.FromSeconds(AuraContainer.ExpiryLagSeconds)) return false;
        _refresh(ctx, durationSeconds);
        return true;
    }

    /// <summary>Ends the effect now, cancelling every tick that has not fired yet.</summary>
    public void Stop(SimulationContext ctx)
    {
        if (!IsActive) return;
        Finish(ctx, clipped: true);
    }

    private void Tick(SimulationContext ctx, double fraction)
    {
        if (!IsActive) return;
        TicksFired++;
        // Hits dealt by this tick are marked as a partial tick when fraction < 1.
        ctx.CurrentTickFraction = fraction;
        var outer = ctx.CurrentPeriodicEffect;
        ctx.CurrentPeriodicEffect = this;
        try { _onTick(ctx, fraction); }
        finally
        {
            ctx.CurrentTickFraction = 1.0;
            ctx.CurrentPeriodicEffect = outer;
        }
    }

    private void Finish(SimulationContext ctx, bool clipped)
    {
        if (!IsActive) return;
        IsActive = false;
        _nextHastedTick?.Cancel();
        foreach (var ev in _events) ev.Cancel();
        _onEnd?.Invoke(ctx, clipped);
        Ended?.Invoke(ctx, this);
    }
}
