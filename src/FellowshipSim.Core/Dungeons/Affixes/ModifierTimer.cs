namespace FellowshipSim.Core.Dungeons.Affixes;

/// <summary>
/// The game's dungeon modifier timer (CRAbilityTask_ModifierCooldown::WaitForModifierTrigger, binary 0x145123770, build
/// 26320967), with OneTimeCooldown off, RespectBossWindow on and BeginAfterDungeonStart on, as both modelled affixes call
/// it. Each wait is drawn uniformly from a minimum to a maximum (0x1451227D0) and starts at the dungeon's start, then again
/// at each trigger (0x14511F754). A wait that comes due in a boss fight is queued for a window the boss opens
/// (0x14511F670, CRGameModeBossComponent 0x14533B140), and a boss's end drops it for a new draw (0x145337AC0).
/// <see cref="Paused"/> stops the elapsed time while the party is out of combat, from the first combat end on, keeping
/// the time left (SetTimerPaused 0x145121B50; the Anomalous Orbs Blueprint pauses it on combat end and resumes it on
/// combat begin).
/// </summary>
internal sealed class ModifierTimer
{
    /// <summary>
    /// The seconds a trigger due in a boss fight waits for the boss's window. Boss behaviour, from the logs: the Storm
    /// Shield charges with a boss in combat at both ends come a median 77.9 s apart (36 gaps in the 8 downloaded Storm Shield
    /// fights with a boss gap; Ab1yCxVcNBHPz6t8 fight 7: 90.7 s, 7 gaps; 8vb2pTAhFQRcktaD fight 2: 100.0 s, 4 gaps), 12.4 s
    /// over the draw's mean of 65.5 s. The charges with no boss at either end come a median 62.9 s to 67.9 s apart in each
    /// of the 9 fights.
    /// </summary>
    public const double BossWindowWaitSeconds = 12.4;

    private readonly AffixRun _run;
    private readonly double _min, _max;
    private readonly bool _paused;
    private readonly Action<SimulationContext> _trigger;
    private double _left;
    private SimTime _since;
    private ScheduledEvent? _due, _window;
    private bool _combatEnded;

    /// <summary>
    /// A timer of <paramref name="min"/> to <paramref name="max"/> seconds a wait that runs <paramref name="trigger"/> at each
    /// end; with <paramref name="paused"/>, out of combat after the first combat end the time stops.
    /// </summary>
    public ModifierTimer(AffixRun run, double min, double max, bool paused, Action<SimulationContext> trigger)
    {
        _run = run;
        _min = min;
        _max = max;
        _paused = paused;
        _trigger = trigger;
        run.CombatChanged += Changed;
        run.Ended += _ => Stop();
        Draw(run.Context);
        Resume(run.Context);
    }

    /// <summary>True while out of combat the time stops.</summary>
    public bool Paused => _paused;

    private bool Stopped => _paused && _combatEnded && !_run.InCombat;

    private void Draw(SimulationContext ctx) => _left = _min + ctx.Outcomes.RollDelay(_max - _min, ctx);

    private void Changed(SimulationContext ctx)
    {
        if (!_run.InCombat) _combatEnded = true;
        if (!_paused) return;
        if (Stopped && _due is { } due)
        {
            due.Cancel();
            _due = null;
            _left -= (ctx.CurrentTime - _since).TotalSeconds;
        }
        else if (!Stopped && _due == null && _window == null) Resume(ctx);
    }

    private void Resume(SimulationContext ctx)
    {
        _since = ctx.CurrentTime;
        _due = ctx.Schedule(SimTime.FromSeconds(Math.Max(0, _left)), Due, "Dungeon modifier");
    }

    private void Due(SimulationContext ctx)
    {
        _due = null;
        if (!_run.BossFight)
        {
            Fire(ctx);
            return;
        }
        // Queued for the boss's window; the boss's end drops it for a new draw.
        _window = ctx.Schedule(SimTime.FromSeconds(BossWindowWaitSeconds), c =>
        {
            _window = null;
            if (_run.BossFight) Fire(c);
            else Next(c);
        }, "Boss modifier window");
        _run.BossChanged += BossEnd;
    }

    private void BossEnd(SimulationContext ctx)
    {
        if (_window is not { } window || _run.BossFight) return;
        window.Cancel();
        _window = null;
        _run.BossChanged -= BossEnd;
        Next(ctx);
    }

    private void Fire(SimulationContext ctx)
    {
        _run.BossChanged -= BossEnd;
        _trigger(ctx);
        Next(ctx);
    }

    private void Next(SimulationContext ctx)
    {
        _run.BossChanged -= BossEnd;
        Draw(ctx);
        if (!Stopped) Resume(ctx);
    }

    private void Stop()
    {
        _due?.Cancel();
        _window?.Cancel();
    }
}
