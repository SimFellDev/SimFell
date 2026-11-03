namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Compares each logged value of an exact replay with the range the engine computes for it, and reports each value
/// outside its range as a <see cref="ReplayIssue"/> with the range, the logged value and the <see cref="EngineState"/>.
/// <see cref="LogOutcomeSource"/> and <see cref="ReplayDriver"/> pass it each engine value and its log counterpart; every
/// comparison and every tolerance is here. The expected range comes from the engine and the observed value from the log.
/// </summary>
public sealed class ReplayVerifier
{
    private readonly ReplayOptions _options;
    private readonly ReplayScript _script;
    private readonly Hero _hero;
    private readonly LogAuraTimeline? _logAuras;
    /// <summary>When the engine last removed each aura (by log ID) and its stacks then.</summary>
    private readonly Dictionary<int, (SimTime Time, int Stacks)> _engineRemoved = new();

    public ReplayVerifier(ReplayScript script, ReplayOptions options, Hero hero, LogAuraTimeline? logAuras = null)
    {
        _script = script;
        _options = options;
        _hero = hero;
        _logAuras = logAuras;
        if (logAuras != null) hero.Auras.OnRemoved += (aura, at) => { if (aura.LogId > 0) _engineRemoved[aura.LogId] = (at, aura.Stacks); };
    }

    /// <summary>Set by the driver: issues and hits are attributed to this step.</summary>
    public ReplayStep? CurrentStep { get; set; }

    /// <summary>Issues since the driver last looked (<see cref="ReplayDriver"/> halts on them).</summary>
    public List<ReplayIssue> PendingIssues { get; } = new();

    /// <summary>Every issue reported, in the order reported.</summary>
    public List<ReplayIssue> Issues { get; } = new();

    /// <summary>Every engine hit and every log hit no engine hit matched, in the order checked.</summary>
    public List<HitPair> AllHits { get; } = new();

    // ------------------------------------------------------------------ hits

    /// <summary>
    /// The damage range of a hit whose average roll is <paramref name="average"/>: the game's spread and the rounding
    /// tolerance, widened for a partial tick (its fraction is known to about a server frame), and down to 0 for a hit with
    /// overkill (the log's amount is cut at the enemy's health).
    /// </summary>
    public ExpectedRange HitDamageRange(double average, double tickFraction = 1.0, bool overkill = false)
    {
        double low = average * (1.0 - _options.DamageSpread) - _options.DamageAbsoluteTolerance;
        double high = average * (1.0 + _options.DamageSpread) * (1.0 + _options.UnexplainedHighMargin) + _options.DamageAbsoluteTolerance;
        if (tickFraction < 1.0 && tickFraction > 0.0)
        {
            double fullTick = average / tickFraction;
            low -= fullTick * _options.PartialTickFractionTolerance;
            high += fullTick * _options.PartialTickFractionTolerance;
        }
        return new ExpectedRange(overkill ? 0.0 : low, high, "damage");
    }

    /// <summary>
    /// An engine hit landed with its log hit: damage, crit on a hit that cannot crit, and a forced crit. The average roll
    /// is <paramref name="landed"/>'s base damage (grievous overflow included), times its crit multiplier on a logged crit.
    /// </summary>
    public HitPair CheckHit(SimulationContext ctx, in HitRequest landed, LogDamage log, double releasedAt)
    {
        double average = landed.BaseDamage * (log.Crit ? landed.CritMultiplier : 1.0);
        var range = HitDamageRange(average, landed.TickFraction, log.Overkill > 0);
        var state = EngineState.Capture(ctx, landed.Target, landed, releasedAt, ctx.CurrentTime.TotalSeconds);
        var target = landed.Target == null ? null : new UnitRef(landed.Target.Id, landed.Target.InstanceId);
        var pair = new HitPair(releasedAt, landed.AbilityName, landed.AbilityId, target, average, log, log.Crit, landed.CritChance,
            landed.Target?.HealthFraction ?? 1.0)
        {
            ExpectedLow = range.Low,
            ExpectedHigh = range.High,
            State = state,
            TickFraction = landed.TickFraction,
            LandedTime = ctx.CurrentTime.TotalSeconds,
        };
        Record(pair);

        var crit = new ExpectedRange(0, 0, "crit");
        if (log.Crit && !landed.CanCrit)
        {
            Report(ctx, IssueCategory.Hits,
                $"Log hit {log.AbilityName} at {log.Time:F3} crit, but the engine says {landed.AbilityName} cannot crit",
                $"crit on non-critable: {landed.AbilityName}", crit, 1, state);
        }
        if (landed.ForcedCrit is { } forced && forced != log.Crit)
        {
            Report(ctx, IssueCategory.Hits,
                $"{landed.AbilityName} at {log.Time:F3}: the engine forces {(forced ? "a crit" : "no crit")}, the log hit {(log.Crit ? "crit" : "did not crit")}",
                $"forced crit: {landed.AbilityName}", new ExpectedRange(forced ? 1 : 0, forced ? 1 : 0, "crit"), log.Crit ? 1 : 0, state);
        }
        if (!range.Contains(log.UnblockedAmount))
        {
            Report(ctx, IssueCategory.Damage,
                $"{landed.AbilityName} on {target} at {log.Time:F3}: log {log.ComparedAmountText()}{(log.Crit ? " (crit)" : "")} vs engine {average:F0}",
                $"damage {(log.UnblockedAmount < range.Low ? "too high" : "too low")}: {landed.AbilityName} ({landed.AbilityId})",
                range, log.UnblockedAmount, state);
        }
        return pair;
    }

    /// <summary>
    /// An engine hit with no log hit: it deals nothing. Its range is from the base damage at release (times the crit
    /// multiplier on a forced crit).
    /// </summary>
    public HitPair EngineHitWithoutLogHit(SimulationContext ctx, in HitRequest released)
    {
        double average = released.BaseDamage * (released.ForcedCrit == true ? released.CritMultiplier : 1.0);
        var range = HitDamageRange(average, released.TickFraction);
        var state = EngineState.Capture(ctx, released.Target, released);
        var target = released.Target == null ? null : new UnitRef(released.Target.Id, released.Target.InstanceId);
        var pair = new HitPair(ctx.CurrentTime.TotalSeconds, released.AbilityName, released.AbilityId, target, average, null,
            released.ForcedCrit ?? (released.CanCrit && released.CritChance >= 1.0), released.CritChance, released.Target?.HealthFraction ?? 1.0)
        {
            ExpectedLow = range.Low,
            ExpectedHigh = range.High,
            State = state,
            TickFraction = released.TickFraction,
        };
        Record(pair);
        Report(ctx, IssueCategory.Hits,
            $"Engine hit {released.AbilityName} ({released.AbilityId}) on {target?.ToString() ?? "no target"} has no matching log hit",
            $"extra engine hit: {released.AbilityName} ({released.AbilityId})", range, 0, state);
        return pair;
    }

    /// <summary>A log hit no engine hit matched, dealt by the replay.</summary>
    public HitPair LogHitWithoutEngineHit(SimulationContext ctx, LogDamage log, EnemyTarget? target)
    {
        var state = EngineState.Capture(ctx, target);
        var pair = new HitPair(log.Time, log.AbilityName, log.AbilityId, log.Target, 0.0, log, log.Crit, 0.0, target?.HealthFraction ?? 1.0)
        {
            ExpectedLow = 0,
            ExpectedHigh = 0,
            State = state,
        };
        Record(pair);
        Report(ctx, IssueCategory.Hits,
            $"Log hit {log.AbilityName} ({log.AbilityId}) on {log.Target} at {log.Time:F3} for {log.ComparedAmountText()}{(log.Crit ? " (crit)" : "")} was never produced by the engine",
            $"missing engine hit: {log.AbilityName} ({log.AbilityId})", new ExpectedRange(0, 0, "damage"), log.UnblockedAmount, state);
        return pair;
    }

    // ------------------------------------------------------------------ casts

    /// <summary>The time a logged cast took (<c>complete_cast</c> minus <c>begin_cast</c>) against the engine's cast time at its start.</summary>
    public void CheckCastTime(SimulationContext ctx, Ability ability, double engineSeconds, double logSeconds)
    {
        var range = CastTimeRange(engineSeconds);
        if (range.Contains(logSeconds)) return;
        Report(ctx, IssueCategory.Casts, $"{ability.Name} cast takes {engineSeconds:F3}s in the engine and {logSeconds:F3}s in the log",
            $"cast time: {ability.Name}", range, logSeconds, EngineState.Capture(ctx, _hero.CurrentTarget, cast: ability));
    }

    /// <summary>
    /// A logged <c>cast</c> (no cast time) or <c>begin_cast</c> (<paramref name="logSeconds"/>; null when the log never
    /// ends it, which leaves no numeric range) against the engine's cast time.
    /// </summary>
    public void CheckInstantOrCast(SimulationContext ctx, Ability ability, double engineSeconds, double? logSeconds, string kind)
    {
        ExpectedRange? range = logSeconds.HasValue ? CastTimeRange(engineSeconds) : null;
        if (logSeconds is { } logged && range!.Contains(logged)) return;
        string message = kind == CommandKind.Cast
            ? $"Log cast {ability.Name} instantly, but the engine expects a {engineSeconds:F3}s cast"
            : $"Log shows a cast time for {ability.Name}, but the engine resolves it in {engineSeconds:F3}s";
        Report(ctx, IssueCategory.Casts, message,
            kind == CommandKind.Cast ? $"engine has cast time: {ability.Name}" : $"engine instant: {ability.Name}",
            range, logSeconds, EngineState.Capture(ctx, _hero.CurrentTarget, cast: ability));
    }

    private ExpectedRange CastTimeRange(double engineSeconds) =>
        new(Math.Max(0.0, engineSeconds - _options.CastTimeToleranceSeconds), engineSeconds + _options.CastTimeToleranceSeconds, "s");

    /// <summary>The engine is still casting when the log casts again: the next cast comes no sooner than the engine cast's end.</summary>
    public ReplayIssue? StillCasting(SimulationContext ctx, Ability next)
    {
        if (_hero.CurrentCast is not { } cast || next.IgnoresGcd) return null;
        var range = new ExpectedRange(cast.EndTime.TotalSeconds, double.PositiveInfinity, "s");
        double now = ctx.CurrentTime.TotalSeconds;
        if (range.Contains(now)) return null;
        return Issue(ctx, IssueCategory.Casts,
            $"Engine is still casting {cast.Ability.Name} (ends {cast.EndTime}) when the log casts {next.Name}",
            $"still casting: {cast.Ability.Name}", range, now, EngineState.Capture(ctx, cast.Target, cast: cast.Ability));
    }

    /// <summary>
    /// Every reason the engine refuses <paramref name="ability"/> now: the global cooldown, the cooldown, and any other
    /// <see cref="Ability.CanCast(SimulationContext, out string?)"/> refusal (resources, requirements). The issues are not
    /// reported yet (<see cref="Report(ReplayIssue)"/>).
    /// </summary>
    public List<ReplayIssue> CastRefusals(SimulationContext ctx, Ability ability)
    {
        var refusals = new List<ReplayIssue>();
        var now = ctx.CurrentTime;
        double time = now.TotalSeconds;
        bool canCast = ability.CanCast(ctx, out var reason);
        var state = EngineState.Capture(ctx, _hero.CurrentTarget, cast: ability, refusal: reason);

        if (!ability.IgnoresGcd && !ability.UsableDuringGcd)
        {
            var gcd =new ExpectedRange(_hero.GlobalCooldownEndTime.TotalSeconds - _options.GlobalCooldownToleranceSeconds, double.PositiveInfinity, "s");
            if (!gcd.Contains(time))
            {
                refusals.Add(Issue(ctx, IssueCategory.GlobalCooldown,
                    $"Log casts {ability.Name} on the engine's global cooldown ({(_hero.GlobalCooldownEndTime - now).TotalSeconds:F2}s remaining)",
                    $"on global cooldown: {ability.Name}", gcd, time, state));
            }
        }

        bool onCooldown = false;
        if (ability.Cooldown > SimTime.Zero && _hero.Cooldowns.TryGetValue(ability.Name, out var cooldown) && cooldown.ChargesAt(now) == 0)
        {
            onCooldown = true;
            var range = new ExpectedRange(time + _hero.GetCooldownRemains(ability.Name, now), double.PositiveInfinity, "s");
            refusals.Add(Issue(ctx, IssueCategory.Cooldown,
                $"Log casts {ability.Name} on the engine's cooldown ({range.Low - time:F2}s remaining)",
                $"on cooldown: {ability.Name}", range, time, state));
        }

        if (!canCast && !onCooldown)
        {
            refusals.Add(Issue(ctx, IssueCategory.Casts, $"Engine refuses {ability.Name}: {reason}", $"refused: {ability.Name}", null, null, state));
        }
        return refusals;
    }

    // ------------------------------------------------------------------ periodic ticks

    /// <summary>A log tick's time against the engine's predicted tick time.</summary>
    public void CheckTickTime(SimulationContext ctx, PeriodicEffect effect, SimTime predicted, double logTime)
    {
        double tolerance = _options.TickTimeToleranceSeconds;
        var range = new ExpectedRange(predicted.TotalSeconds - tolerance, predicted.TotalSeconds + tolerance, "s");
        if (range.Contains(logTime)) return;
        Report(ctx, IssueCategory.Ticks, $"{effect.Name} ticks at {logTime:F3} in the log; the engine predicts {predicted}",
            $"tick time: {effect.Name}", range, logTime, EngineState.Capture(ctx, effect.Target));
    }

    /// <summary>The ticks an ended effect fired (full and partial) against the log ticks it reserved.</summary>
    public void CheckTickCount(SimulationContext ctx, PeriodicEffect effect, int reserved)
    {
        var range = new ExpectedRange(effect.TicksFired, effect.TicksFired, "ticks");
        if (range.Contains(reserved)) return;
        Report(ctx, IssueCategory.Ticks, $"{effect.Name} (from {effect.Start}) fired {effect.TicksFired} ticks; the log has {reserved} of them",
            $"tick count: {effect.Name}", range, reserved, EngineState.Capture(ctx, effect.Target));
    }

    // ------------------------------------------------------------------ procs

    /// <summary>A proc the log shows at a roll whose chance is zero in the engine.</summary>
    public void CheckProc(SimulationContext ctx, string procKey, double chance, bool procced, EnemyTarget? target)
    {
        if (!procced || chance > 0.0) return;
        Report(ctx, IssueCategory.Procs, $"Log shows '{procKey}' procced at {ctx.CurrentTime.TotalSeconds:F3} but the engine's chance was {chance:P1}",
            $"proc with zero chance: {procKey}", new ExpectedRange(0, 0, "procs"), 1, EngineState.Capture(ctx, target));
    }

    // ------------------------------------------------------------------ state before a cast

    /// <summary>Whether the log marks ability activations on its cast events.</summary>
    private bool? _activationMarked;

    /// <summary>Resources and tracked auras before the action match the log.</summary>
    public void CheckState(SimulationContext ctx, ReplayStep step, ReplayCommand cmd)
    {
        // The cast's own activation event: a cast-time ability's completion at the same moment carries the resources
        // before its own gains and spends (combat logs, the 15 Rime fights of patch 0.5.0.0: 333 begincasts right after the
        // same ability's completion, whose activation event has no resources, took that completion's reading, and 239 of
        // them differed in Anima). A log without activation marks keeps the nearest reading of the ability.
        _activationMarked ??= _script.Observations.Resources.Any(r => r.Activation);
        var snapshot = _script.Observations.Resources.FirstOrDefault(r =>
            Math.Abs(r.Time - cmd.Time) <= _options.ResourceSnapshotWindowSeconds && r.AbilityId == cmd.AbilityId
            && (r.Activation || _activationMarked == false));
        if (snapshot != null)
        {
            // Each of the hero's resources that combat logs report (LogResources).
            foreach (var logged in LogResources.All.Where(l => _hero.ResourceDefs.Contains(l.Resource)))
            {
                if (!snapshot.Resources.TryGetValue(logged.LogResourceType.ToString(), out var value)) continue;
                CheckResource(ctx, step, logged.Resource.Name, _hero.ResourceAmount(logged.Resource), logged.EngineAmount(value), logged.Tolerance);
            }
        }

        if (_logAuras == null) return;
        // The log shows one state per aura ID; the engine can have two auras with one ID (the hero's own Inspired Allegiance
        // and another player's).
        var engineState = _hero.Auras.Active(ctx.CurrentTime)
            .Where(a => _logAuras.IsTracked(a.LogId))
            .GroupBy(a => a.LogId)
            .ToDictionary(g => g.Key, g => g.Max(a => a.Stacks));

        // Each aura matches if the log shows the engine's stacks at some moment within the tolerance (server-frame timing).
        double tol = _options.AuraToleranceSeconds;
        var shown = _logAuras.TrackedStateAt(cmd.Time - 0.001);
        var window = _logAuras.ChangeTimesBetween(cmd.Time - tol, cmd.Time + tol)
            .Prepend(cmd.Time - tol)
            .Select(t => _logAuras.TrackedStateAt(t)).ToList();
        foreach (var id in engineState.Keys.Union(shown.Keys).OrderBy(id => _logAuras.NameOf(id)))
        {
            int engine = engineState.GetValueOrDefault(id);
            // An aura the engine removed within the tolerance still counts (the game ends auras on its next server frame).
            if (engine == 0 && _engineRemoved.TryGetValue(id, out var removed)
                && (ctx.CurrentTime - removed.Time).TotalSeconds <= tol && shown.GetValueOrDefault(id) == removed.Stacks)
            {
                engine = removed.Stacks;
            }
            int log = window.Any(w => w.GetValueOrDefault(id) == engine) ? engine : shown.GetValueOrDefault(id);
            CheckAura(ctx, step, _logAuras.NameOf(id), engine, log, id);
        }

        step.UntrackedLogAuras = _logAuras.UntrackedActiveAt(cmd.Time - 0.001);
        step.EngineHaste = _hero.GetHaste(ctx.CurrentTime);
        step.HasteSources = _hero.Auras.Active(ctx.CurrentTime)
            .SelectMany(a => a.Modifiers.Where(m => m.Type is StatType.Haste or StatType.HasteRating)
                .Select(m => $"{a.Name} x{a.Stacks}: {m.Type} {m.Op} {m.Value:0.####}"))
            .ToList();
    }

    /// <summary>A pre-cast resource: the log reading against the engine amount within the resource's tolerance.</summary>
    public void CheckResource(SimulationContext ctx, ReplayStep step, string resource, double engine, double log, double tolerance)
    {
        var check = new ResourceCheck(resource, engine, log, tolerance);
        step.Resources.Add(check);
        var range = new ExpectedRange(engine - tolerance - 1e-9, engine + tolerance + 1e-9, resource);
        if (range.Contains(log)) return;
        Report(ctx, IssueCategory.Resources, $"Pre-cast {resource}: engine {engine:0.##} vs log {log:0.##}",
            $"{resource} {(engine > log ? "too high" : "too low")}", range, log, EngineState.Capture(ctx, _hero.CurrentTarget));
    }

    /// <summary>A pre-cast aura: the log's stacks within the aura tolerance against the engine's.</summary>
    public void CheckAura(SimulationContext ctx, ReplayStep step, string aura, int engine, int log, int logId = 0)
    {
        var check = new AuraCheck(aura, engine, log, logId);
        step.Auras.Add(check);
        if (check.Matches) return;
        string how = engine == 0 ? "missing in engine" : log == 0 ? "extra in engine" : "wrong stacks";
        Report(ctx, IssueCategory.Auras, $"Pre-cast {aura}: engine {engine} stacks vs log {log}", $"{aura} {how}",
            new ExpectedRange(engine, engine, "stacks"), log, EngineState.Capture(ctx, _hero.CurrentTarget));
    }

    // ------------------------------------------------------------------ issues

    /// <summary>Reports a disagreement with no numeric range (or one built by the caller).</summary>
    public ReplayIssue Report(SimulationContext ctx, IssueCategory category, string message, string? subject = null,
        ExpectedRange? expected = null, double? observed = null, EngineState? state = null) =>
        Report(Issue(ctx, category, message, subject, expected, observed, state ?? EngineState.Capture(ctx, _hero.CurrentTarget)));

    public ReplayIssue Report(ReplayIssue issue)
    {
        PendingIssues.Add(issue);
        Issues.Add(issue);
        CurrentStep?.Issues.Add(issue);
        return issue;
    }

    private ReplayIssue Issue(SimulationContext ctx, IssueCategory category, string message, string? subject, ExpectedRange? expected,
        double? observed, EngineState? state) =>
        new(ctx.CurrentTime.TotalSeconds, CurrentStep?.Index ?? 0, category, message, subject)
        {
            Expected = expected,
            Observed = observed,
            State = state,
        };

    private void Record(HitPair pair)
    {
        AllHits.Add(pair);
        CurrentStep?.Hits.Add(pair);
    }
}
