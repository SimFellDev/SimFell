namespace FellowshipSim.Core.Engine;

/// <summary>
/// A timer that runs on hasted time: it fires after <c>baseSeconds</c> of progress, where progress runs at
/// (1 + haste) per second. When the hero's haste changes part way, the time left shrinks or grows from that moment
/// (combat logs: Amethyst Splinter ticks speed up as soon as Spirit of Heroism starts).
/// Haste is re-read whenever an aura with a haste modifier is gained or removed; a hero's aura runs out in an event at its
/// expiry (<see cref="AuraContainer.RunOutIn"/>).
/// <para>
/// <see cref="Start"/> fires once. <see cref="StartPeriod"/> is the period timer of an effect tagged EffectType.Dot or
/// EffectType.Hot, firing every period until cancelled, re-armed the way the game re-arms it (build 26162532):
/// ACRCharacterBase binds the Haste attribute's change (0x1452EBCA0, NewValue != OldValue) to 0x1452EE870, which re-arms
/// every running Dot or Hot effect the character applied (ASC +0x13B0, registered by 0x1452D4E60) through 0x1452D88D0 at
/// Period / Haste(now), its first delay the new period x remaining / old period. A change inside the tick's own execution
/// reads a remaining of 0 (FTimerManager::GetTimerRemaining 0x144041D00 for an Executing timer), and a first delay of 0
/// fires on the next server frame (FTimerManager::Tick 0x144056E90): the tick comes again a frame later, and the ticks
/// after it come every new period from the first. A change in an ability activation before the frame's timers
/// (<see cref="SimulationContext.AbilityActivation"/>) reads the time left from the frame before: the next tick comes a
/// frame later.
/// </para>
/// </summary>
public sealed class HastedTimer
{
    private readonly SimulationContext _ctx;
    private readonly Hero _hero;
    private readonly Action<SimulationContext> _onFire;
    private readonly string _label;
    private readonly double _baseSeconds;
    private readonly bool _period;
    private double _remainingBase;
    private double _haste;
    private SimTime _lastUpdate;
    private ScheduledEvent? _event;
    private ScheduledEvent? _repeatEvent;
    private bool _updating;
    // A period timer's tick is running: a haste change now re-arms it from the tick.
    private bool _executing;
    private bool _rearmedInTick;
    // The ability activation that last moved the timer a frame later.
    private int _shiftedActivation;
    // Made once per timer: rescheduling happens on every haste change.
    private readonly Action<SimulationContext> _due;
    private readonly Action<SimulationContext>? _repeat;

    private HastedTimer(SimulationContext ctx, Hero hero, double baseSeconds, Action<SimulationContext> onFire, string label, bool period)
    {
        _ctx = ctx;
        _hero = hero;
        _onFire = onFire;
        _label = label;
        _baseSeconds = baseSeconds;
        _period = period;
        _remainingBase = baseSeconds;
        _lastUpdate = ctx.CurrentTime;
        _haste = hero.GetHaste(ctx.CurrentTime);
        if (period)
        {
            _due = c =>
            {
                if (!IsActive) return;
                _event = null;
                Execute(c, regular: true);
            };
            _repeat = c =>
            {
                _repeatEvent = null;
                if (!IsActive) return;
                Execute(c, regular: false);
            };
        }
        else
        {
            _due = c =>
            {
                if (!IsActive) return;
                Finish();
                _onFire(c);
            };
        }
    }

    public bool IsActive { get; private set; } = true;

    /// <summary>When a period timer last ticked, a repeated tick included.</summary>
    public SimTime LastTick { get; private set; } = SimTime.FromSeconds(-1.0);

    /// <summary>
    /// The damage ID of a period timer's own hits. They are part of the tick's execution (the effect's damage execution
    /// runs in the period callback), so a hit an outcome source lands later still lands inside the tick.
    /// </summary>
    public int DamageLogId { get; private init; }

    /// <summary>Hasted progress made so far, in base seconds.</summary>
    public double ElapsedBaseSeconds =>
        _baseSeconds - (_remainingBase - (_ctx.CurrentTime - _lastUpdate).TotalSeconds * (1.0 + _haste));

    public static HastedTimer Start(SimulationContext ctx, Hero hero, double baseSeconds, Action<SimulationContext> onFire, string label)
    {
        var timer = new HastedTimer(ctx, hero, baseSeconds, onFire, label, period: false);
        hero.Auras.AddTimer(timer);
        timer.Reschedule();
        return timer;
    }

    /// <summary>
    /// The period timer of a running EffectType.Dot or EffectType.Hot effect: <paramref name="onTick"/> every
    /// <paramref name="basePeriodSeconds"/> of hasted time from now until <see cref="Cancel"/>, the first tick one period
    /// after the start. <paramref name="damageLogId"/> is the damage ID of the tick's own hits.
    /// </summary>
    public static HastedTimer StartPeriod(SimulationContext ctx, Hero hero, double basePeriodSeconds, Action<SimulationContext> onTick, string label,
        int damageLogId)
    {
        var timer = new HastedTimer(ctx, hero, basePeriodSeconds, onTick, label, period: true) { DamageLogId = damageLogId };
        hero.Auras.AddTimer(timer);
        timer.Reschedule();
        return timer;
    }

    public void Cancel()
    {
        if (!IsActive) return;
        Finish();
    }

    /// <summary>
    /// Runs <paramref name="action"/> as part of the last tick's execution, for what the game runs inside it after the
    /// tick has passed in the engine: a haste change in it re-arms the period timer from that tick.
    /// </summary>
    public void RunInLastTick(Action action)
    {
        if (!_period || !IsActive || _executing)
        {
            action();
            return;
        }
        var outer = _ctx.ExecutingTick;
        _ctx.ExecutingTick = this;
        _executing = true;
        _rearmedInTick = false;
        try { action(); }
        finally
        {
            _executing = false;
            _ctx.ExecutingTick = outer;
        }
        if (IsActive && _rearmedInTick) Rearm(LastTick);
    }

    /// <summary>
    /// Called by the hero's auras whenever one is gained or removed: banks the progress made at the old haste, then
    /// re-reads haste and reschedules.
    /// </summary>
    internal void Update()
    {
        if (!IsActive || _updating) return;
        _updating = true;
        try
        {
            var now = _ctx.CurrentTime;
            double haste = _hero.GetHaste(now);
            if (_executing)
            {
                // The tick's own execution: the re-arm reads a remaining of 0, and the timer goes on from the tick.
                if (Math.Abs(haste - _haste) >= 1e-12) _rearmedInTick = true;
                _haste = haste;
                return;
            }
            _remainingBase -= (now - _lastUpdate).TotalSeconds * (1.0 + _haste);
            _lastUpdate = now;
            if (Math.Abs(haste - _haste) < 1e-12 && _event is { HasFired: false, IsCancelled: false }) return;
            if (_period && _ctx.AbilityActivation != 0 && _ctx.AbilityActivation != _shiftedActivation)
            {
                // Before the frame's timers, GetTimerRemaining reads the timer against the last frame's time, and the
                // re-armed timer is pending until this frame's FTimerManager::Tick adds this frame's time
                // (InternalSetTimer 0x144041D40, 0x144041F17; FTimerManager::Tick 0x144057664): the next tick comes a
                // server frame later. A second change in the same activation reads the pending timer's own time left.
                _shiftedActivation = _ctx.AbilityActivation;
                _remainingBase += PeriodicEffect.ServerFrameSeconds * (1.0 + _haste);
            }
            _haste = haste;
            Reschedule();
        }
        finally { _updating = false; }
    }

    private void Execute(SimulationContext c, bool regular)
    {
        var now = c.CurrentTime;
        LastTick = now;
        var outer = c.ExecutingTick;
        c.ExecutingTick = this;
        _executing = true;
        _rearmedInTick = false;
        try { _onFire(c); }
        finally
        {
            _executing = false;
            c.ExecutingTick = outer;
        }
        if (!IsActive) return;
        if (_rearmedInTick) Rearm(now);
        else if (regular)
        {
            // The next period starts at this tick.
            _remainingBase = _baseSeconds;
            _lastUpdate = now;
            Reschedule();
        }
    }

    /// <summary>A first delay of 0 at <paramref name="tick"/>: the tick again on the next server frame, then every new period from it.</summary>
    private void Rearm(SimTime tick)
    {
        _haste = _hero.GetHaste(_ctx.CurrentTime);
        _remainingBase = _baseSeconds;
        _lastUpdate = tick;
        Reschedule();
        _repeatEvent?.Cancel();
        _repeatEvent = _ctx.ScheduleAt(tick + SimTime.FromSeconds(PeriodicEffect.ServerFrameSeconds), _repeat!, _label);
    }

    private void Reschedule()
    {
        _event?.Cancel();
        var due = _lastUpdate + SimTime.FromSeconds(Math.Max(0.0, _remainingBase) / (1.0 + _haste));
        _event = _ctx.ScheduleAt(due, _due, _label);
    }

    private void Finish()
    {
        IsActive = false;
        _event?.Cancel();
        _repeatEvent?.Cancel();
        _hero.Auras.RemoveTimer(this);
    }
}
