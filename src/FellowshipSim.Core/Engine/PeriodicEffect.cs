namespace FellowshipSim.Core.Engine;

/// <summary>
/// A repeating effect on the event queue: channel ticks, damage-over-time pulses, periodic resource gains.
/// Ticks fire every <see cref="Period"/> from the start until the effect's end. Optionally one tick fires
/// immediately on start, and a final partial tick fires at the end for the leftover fraction of a period.
/// Stopping the effect cancels every tick that has not fired yet.
/// </summary>
public sealed class PeriodicEffect
{
    private readonly List<ScheduledEvent> _events = new();
    private readonly Action<SimulationContext, double> _onTick;
    private readonly Action<SimulationContext, bool>? _onEnd;

    private PeriodicEffect(string name, SimTime start, SimTime end, double periodSeconds,
        Action<SimulationContext, double> onTick, Action<SimulationContext, bool>? onEnd)
    {
        Name = name;
        Start = start;
        End = end;
        Period = periodSeconds;
        _onTick = onTick;
        _onEnd = onEnd;
    }

    public string Name { get; }
    public SimTime Start { get; }
    public SimTime End { get; }
    public double Period { get; }
    public int TicksFired { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The game runs on server frames; a partial final tick shorter than one frame never happens
    /// (combat logs: Bursting Ice @ 29.587 in yMAZVJL9hTpFjqbC fight 7 ends 20ms after its last full pulse, with no partial pulse).
    /// </summary>
    public const double ServerFrameSeconds = 1.0 / 30.0;

    /// <summary>
    /// DefaultGame.ini [/Script/fellowship.CRAbilitySystemGlobals] MinimumPartialTickTime (UCRAbilitySystemGlobals +0x32C),
    /// seconds: when a damage over time's duration runs out (CheckDuration 0x144A6E990), its partial final tick
    /// (0x1452D0C70, Dot and Hot effects alone) is skipped when elapsed = GetTimerRate - GetTimerRemaining of its period
    /// timer, the time since the last tick was due at the hasted rate, is under this (strict: equal runs; 0x1452D0E13),
    /// and its factor is elapsed / rate (game-data-authority answer 10, game-data-binary).
    /// </summary>
    public const double MinHastedPartialTickSeconds = 0.1;

    private HastedTimer? _nextHastedTick;

    /// <summary>The period timer of a damage over time (<see cref="BeginHasted"/> with a damageOverTimeLogId).</summary>
    public HastedTimer? PeriodTimer => _nextHastedTick;

    /// <param name="onTick">Called per tick with the tick's fraction of a full tick (1.0, or less for a partial final tick).</param>
    /// <param name="onEnd">Called once when the effect ends; the flag is true when it was stopped early.</param>
    /// <param name="tickDelaySeconds">
    /// Every tick fires this much after its scheduled time (but no later than the end); tick counts and the partial
    /// final tick's fraction still follow the schedule.
    /// </param>
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
        double minPartialSeconds = ServerFrameSeconds)
    {
        if (periodSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(periodSeconds));

        var start = ctx.CurrentTime;
        var end = start + SimTime.FromSeconds(durationSeconds);
        var effect = new PeriodicEffect(name, start, end, periodSeconds, onTick, onEnd);

        int fullTicks = (int)Math.Floor((durationSeconds + 1e-6) / periodSeconds);
        int firstTick = tickOnStart && tickDelaySeconds > 0 ? 0 : 1;
        // One handler for every full tick.
        Action<SimulationContext> fullTick = c => effect.Tick(c, 1.0);
        for (int k = firstTick; k <= fullTicks; k++)
        {
            var at = start + SimTime.FromSeconds(Math.Min(k * periodSeconds + tickDelaySeconds, durationSeconds));
            effect._events.Add(ctx.Kernel.ScheduleAt(at, fullTick, name));
        }

        double remainder = durationSeconds - fullTicks * periodSeconds;
        if (partialFinalTick && remainder >= Math.Max(minPartialSeconds, 1e-6))
        {
            double fraction = remainder / periodSeconds;
            effect._events.Add(ctx.Kernel.ScheduleAt(end, c => effect.Tick(c, fraction), name));
        }

        effect._events.Add(ctx.Kernel.ScheduleAt(end, c => effect.Finish(c, clipped: false), name));

        if (tickOnStart && tickDelaySeconds <= 0)
        {
            effect.Tick(ctx, 1.0);
        }

        return effect;
    }

    /// <summary>
    /// Like <see cref="Begin"/>, but each tick comes after <paramref name="basePeriodSeconds"/> of the hero's hasted time,
    /// so the ticks speed up or slow down the moment haste changes (combat logs: Bursting Ice pulses re-time when
    /// Inspired Allegiance ends mid-debuff). The duration is fixed; the partial final tick is the hasted progress left.
    /// </summary>
    /// <param name="damageOverTimeLogId">
    /// For an effect tagged EffectType.Dot or EffectType.Hot, the damage ID of its ticks' hits: its ticks run on the game's
    /// period timer (<see cref="HastedTimer.StartPeriod"/>), and a haste change inside a tick repeats it a server frame later.
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
        int damageOverTimeLogId = 0)
    {
        if (basePeriodSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(basePeriodSeconds));

        var start = ctx.CurrentTime;
        // Like any timed aura, the effect ends on the server frame after its duration runs out (see AuraContainer.ExpiryLagSeconds).
        var nominalEnd = start + SimTime.FromSeconds(durationSeconds);
        var end = nominalEnd + SimTime.FromSeconds(AuraContainer.ExpiryLagSeconds);
        var effect = new PeriodicEffect(name, start, end, basePeriodSeconds, onTick, onEnd);
        var lastTick = start;
        // One handler for every tick, made once.
        Action<SimulationContext>? fire = null;

        void Fire(SimulationContext cc)
        {
            if (cc.CurrentTime > nominalEnd || !effect.IsActive) return;
            lastTick = cc.CurrentTime;
            effect.Tick(cc, 1.0);
            if (effect.IsActive && damageOverTimeLogId == 0) ScheduleNext(cc);
        }

        void ScheduleNext(SimulationContext c)
        {
            fire ??= Fire;
            if (damageOverTimeLogId != 0)
            {
                effect._nextHastedTick = HastedTimer.StartPeriod(c, hero, basePeriodSeconds, fire, name, damageOverTimeLogId);
                return;
            }
            if (retimeOnHasteChange)
            {
                effect._nextHastedTick = HastedTimer.Start(c, hero, basePeriodSeconds, fire, name);
                return;
            }
            // The period is fixed by the haste when the previous tick fired.
            var period = SimTime.FromSeconds(basePeriodSeconds / (1.0 + hero.GetHaste(c.CurrentTime)));
            effect._events.Add(c.Kernel.ScheduleAt(c.CurrentTime + period, fire, name));
        }

        // The duration runs out at the nominal end (no more ticks; the partial tick is measured there) ...
        double partial = 0.0;
        effect._events.Add(ctx.Kernel.ScheduleAt(nominalEnd, c =>
        {
            if (!effect.IsActive) return;
            var timer = effect._nextHastedTick;
            double fraction = timer is { IsActive: true } ? timer.ElapsedBaseSeconds / basePeriodSeconds : 0.0;
            // A damage over time's partial tick reads how long its period has run (period - remaining, 0x1452D0C70) as the
            // duration runs out: a re-arm since the last tick shortens it.
            double elapsed = damageOverTimeLogId != 0 && timer is { IsActive: true }
                ? timer.ElapsedBaseSeconds / (1.0 + hero.GetHaste(c.CurrentTime))
                : (c.CurrentTime - lastTick).TotalSeconds;
            timer?.Cancel();
            if (partialFinalTick && fraction > 0 && elapsed >= MinHastedPartialTickSeconds - 1e-9)
            {
                partial = Math.Min(fraction, 1.0);
            }
        }, name));
        // ... and the partial tick and the end happen on the next server frame.
        effect._events.Add(ctx.Kernel.ScheduleAt(end, c =>
        {
            if (!effect.IsActive) return;
            if (partial > 0) effect.Tick(c, partial);
            effect.Finish(c, clipped: false);
        }, name));

        if (tickOnStart) effect.Tick(ctx, 1.0);
        if (effect.IsActive) ScheduleNext(ctx);
        return effect;
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
        try { _onTick(ctx, fraction); }
        finally { ctx.CurrentTickFraction = 1.0; }
    }

    private void Finish(SimulationContext ctx, bool clipped)
    {
        if (!IsActive) return;
        IsActive = false;
        _nextHastedTick?.Cancel();
        foreach (var ev in _events)
        {
            if (!ev.HasFired) ev.Cancel();
        }
        _onEnd?.Invoke(ctx, clipped);
    }
}
