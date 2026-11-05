namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Answers every one of the engine's open outcomes from the combat log during an exact replay. The replay makes no random
/// roll:
///   - each engine hit is matched to the log hit with the same damage ID on the same target; the log decides whether it
///     crit, when it landed (projectile travel) and how much it dealt (<see cref="LogDamage.UnblockedAmount"/>),
///   - each proc roll succeeds if the log shows what that proc produces (its evidence log ID) at the roll,
///   - each periodic tick with a damage log ID fires at the time of the log tick it reserves.
/// <see cref="ReplayVerifier"/> compares each logged value with the engine's range for it.
/// </summary>
public sealed class LogOutcomeSource : IOutcomeSource
{
    private readonly ReplayOptions _options;
    private readonly HitLedger _hits;
    private readonly ReplayVerifier _verifier;
    private readonly List<LogAura> _playerAuraEvents;
    private readonly HashSet<int> _playerAuraIds;
    private readonly HashSet<LogAura> _claimedAuraEvidence = new();
    private readonly HashSet<LogDamage> _claimedDamageEvidence = new();

    public LogOutcomeSource(ReplayScript script, ReplayOptions options, HitLedger hits, ReplayVerifier verifier)
    {
        _options = options;
        _hits = hits;
        _verifier = verifier;
        if (options.FreeDiceSeed.HasValue)
        {
            // Free dice: the engine rolls its own outcomes, so this only collects issues (no log lookups to build).
            _playerAuraEvents = [];
            _playerAuraIds = [];
            return;
        }
        _playerAuraEvents = script.Observations.Auras
            .Where(a => a.Target.Id == script.Player.Id && a.Source.Id == script.Player.Id)
            .OrderBy(a => a.Time)
            .ToList();
        _playerAuraIds = _playerAuraEvents.Select(a => a.AbilityId).ToHashSet();
        _resourceReadings = script.Observations.Resources.OrderBy(r => r.Time).ToList();
        // The player's debuffs on enemies, with how many stacks each event added (for stack-jump evidence).
        foreach (var group in script.Observations.Auras
                     .Where(a => a.Source.Id == script.Player.Id && a.Target.Id != script.Player.Id)
                     .GroupBy(a => (a.AbilityId, a.Target)))
        {
            int stacks = 0;
            var list = new List<(LogAura Event, int Added)>();
            var losses = new List<double>();
            _enemyDebuffApplications[group.Key] = group.Where(a => a.Type is "applydebuff" or "refreshdebuff").OrderBy(a => a.Time).ToList();
            foreach (var a in group.OrderBy(a => a.Time))
            {
                int now = a.Type is "removedebuff" or "removebuff" ? 0 : a.Stack ?? (a.IsGain ? Math.Max(stacks, 1) : stacks);
                if (a.IsGain && now > stacks) list.Add((a, now - stacks));
                if (a.IsLoss && now < stacks) losses.Add(a.Time);
                stacks = now;
            }
            _enemyStackGains[group.Key] = list;
            _enemyStackLosses[group.Key] = losses;
        }
        foreach (var c in script.Commands.Where(c => c.Kind == CommandKind.EnemyDemise && c.Enemy != null))
        {
            _demise[new UnitRef(c.Enemy!.Id, c.Enemy.Instance)] = c.Time;
        }
    }

    private readonly Dictionary<UnitRef, double> _demise = new();
    private readonly Dictionary<(int LogId, UnitRef Target), List<(LogAura Event, int Added)>> _enemyStackGains = new();
    // The player's debuffs on enemies, applied or refreshed (for enemy-debuff evidence).
    private readonly Dictionary<(int LogId, UnitRef Target), List<LogAura>> _enemyDebuffApplications = new();
    private readonly HashSet<LogAura> _claimedDebuffApplications = new();

    /// <summary>Proof of a proc that applies a debuff: the log applies or refreshes it on the proc's enemy in the window.</summary>
    private bool ClaimEnemyDebuffEvidence(ProcEvidence.Evidence evidence, double now, EnemyTarget? target)
    {
        if (target == null || !_enemyDebuffApplications.TryGetValue((evidence.LogId, new UnitRef(target.Id, target.InstanceId)), out var applied)) return false;
        double from = now + evidence.MinDelaySeconds - _options.SameTickSeconds, to = now + evidence.DelaySeconds + _options.SameTickSeconds;
        foreach (var a in applied)
        {
            if (a.Time < from || _claimedDebuffApplications.Contains(a)) continue;
            if (a.Time > to) break;
            _claimedDebuffApplications.Add(a);
            return true;
        }
        return false;
    }
    // When the player's debuffs on enemies lost stacks (for stack-loss evidence).
    private readonly Dictionary<(int LogId, UnitRef Target), List<double>> _enemyStackLosses = new();

    /// <summary>True when the player's debuff <paramref name="logId"/> on <paramref name="target"/> lost a stack in the server frame of <paramref name="time"/>.</summary>
    private bool LostStackAt(int logId, UnitRef target, double time) =>
        _enemyStackLosses.TryGetValue((logId, target), out var losses) && losses.Any(t => Math.Abs(t - time) <= _options.SameTickSeconds);
    private readonly HashSet<LogAura> _claimedStackEvidence = new();
    private const double StackEvidenceWindowSeconds = 0.1;

    /// <summary>Hits that would have landed on an enemy already dead in the log (the projectile hit nothing).</summary>
    public int MissedHits { get; private set; }

    public Dictionary<string, ProcRollStats> ProcStats { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The enemies the log shows this hit striking. Inside a tick of a periodic effect with a reserved group, the reserved
    /// group's targets; otherwise the earliest not-yet-used group of log hits with this damage ID (hits landing together)
    /// within the travel window. Enemies the engine does not know are skipped.
    /// </summary>
    public IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx)
    {
        IReadOnlyList<UnitRef>? group;
        if (LogTimedTick(damageLogId, ctx) is { } ticks)
        {
            // A tick with no log tick in its window strikes the engine's enemies: each hit is an engine hit with no log hit.
            group = ticks.Group;
            ticks.Group = null;
            if (group == null) return candidates;
        }
        else group = _hits.ClaimGroup(damageLogId, ctx.CurrentTime.TotalSeconds, _options, ctx.CurrentTickFraction);
        if (group == null)
        {
            _verifier.Report(ctx, IssueCategory.Hits, IssueEntity.Damage(damageLogId, damageLogId.ToString()), $"Engine multi-target hit {damageLogId} at {ctx.CurrentTime} has no log hits", $"extra engine multi-target hit: {damageLogId}");
            return Array.Empty<EnemyTarget>();
        }
        var targets = new List<EnemyTarget>();
        foreach (var unit in group)
        {
            var enemy = ctx.FindEnemy(unit.Id, unit.Instance);
            if (enemy != null) targets.Add(enemy);
        }
        return targets;
    }

    /// <summary>The target of the next log hit with this damage ID that no engine hit has claimed yet (within the travel window).</summary>
    public EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null)
    {
        // A tick that reserved its log hit strikes that hit's target; one with no log tick, the engine's choice.
        if (LogTimedTick(damageLogId, ctx) is { } ticks)
        {
            var group = ticks.Group;
            ticks.Group = null;
            var fallback = preferred ?? (candidates.Count > 0 ? candidates[0] : null);
            return group is [var reserved, ..] ? ctx.FindEnemy(reserved.Id, reserved.Instance) ?? fallback : fallback;
        }
        if (preferred != null)
        {
            // The preferred enemy's log hit landing now; else the log hit nearest the expected landing, the preferred
            // enemy's on a tie. A later hit on the preferred enemy belongs to a later hit (Chain
            // Lightning's bounces land 0.05s apart).
            double now = ctx.CurrentTime.TotalSeconds;
            var unitRef = new UnitRef(preferred.Id, preferred.InstanceId);
            if (_hits.HasUnclaimed(damageLogId, unitRef, now - PeriodicEffect.ServerFrameSeconds, now + PeriodicEffect.ServerFrameSeconds)) return preferred;
            var next = _hits.NextUnclaimedTarget(damageLogId, now, _options, prefer: unitRef);
            return (next == null ? null : ctx.FindEnemy(next.Id, next.Instance)) ?? preferred;
        }
        // Hits on the current target are left for hits that can only go there (e.g. Cascading Blitz swallows share the ID).
        var current = ctx.Player.CurrentTarget;
        var avoid = current == null ? null : new UnitRef(current.Id, current.InstanceId);
        var unit = _hits.NextUnclaimedTarget(damageLogId, ctx.CurrentTime.TotalSeconds, _options, avoid);
        var enemy = unit == null ? null : ctx.FindEnemy(unit.Id, unit.Instance);
        return enemy ?? (candidates.Count > 0 ? candidates[0] : null);
    }

    /// <summary>The log ticks of the periodic effect ticking now, when its ticks follow the log for this damage ID.</summary>
    private EffectTicks? LogTimedTick(int damageLogId, SimulationContext ctx) =>
        ctx.CurrentPeriodicEffect is { } effect && effect.DamageLogId == damageLogId && _ticks.TryGetValue(effect, out var ticks) ? ticks : null;

    /// <summary>On release: match the log hit, which decides the crit and when the hit lands.</summary>
    public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx)
    {
        var target = hit.Target == null ? null : new UnitRef(hit.Target.Id, hit.Target.InstanceId);
        double now = ctx.CurrentTime.TotalSeconds;
        // A tick's hit takes the log tick its effect reserved, and no other: one missing log tick shifts no later pairing.
        bool tickHit = LogTimedTick(hit.AbilityId, ctx) != null;
        var match = target == null ? null : _hits.Match(hit.AbilityId, target, now, _options, ctx.CurrentPeriodicEffect, reservedOnly: tickHit);
        if (match != null) _hits.RecordTravel(hit.AbilityId, match.Time - now);

        if (match == null && target != null && (_options.UnmatchedHitsMissIds.Contains(hit.AbilityId)
            || (_options.MissHitsOnDyingTargets && _demise.TryGetValue(target, out var died)
                && died >= now - _options.SameTickSeconds && died <= now + _hits.LongestTravelSeconds(hit.AbilityId, _options) + PeriodicEffect.ServerFrameSeconds)))
        {
            // The target dies in the log before this hit could land (the log has no hit for it, and its flight can be as
            // long as the longest seen; Frost Bolt's varies with distance, 0.03-1.9s): it hits nothing (no damage, no
            // procs, no counters).
            MissedHits++;
            return new HitOutcome(false, SimTime.Zero, 1.0, null, Missed: true);
        }

        if (match == null)
        {
            _verifier.EngineHitWithoutLogHit(ctx, hit);
            return new HitOutcome(false, SimTime.Zero, 1.0, null, Missed: true);
        }

        var delay = SimTime.FromSeconds(Math.Max(0.0, match.Time - now));
        return new HitOutcome(match.Crit, delay, 1.0, new LogMatch(now, match));
    }

    /// <summary>On landing: the hit deals the log hit's damage, and the verifier compares it with the engine's.</summary>
    public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx)
    {
        var match = (LogMatch)outcome.Token!;
        _verifier.CheckHit(ctx, landed, match.Log, match.ReleasedAt);
        return match.Log.UnblockedAmount;
    }

    private sealed record LogMatch(double ReleasedAt, LogDamage Log);

    public void HitFinished(SimulationContext ctx) { }

    /// <summary>A replay casts at the log's times, so the player's delays are already in them.</summary>
    public SimTime ReactionDelay(ReactionHold hold, ReactionDelays reactions, SimulationContext ctx) => SimTime.Zero;

    // ------------------------------------------------------------------ periodic ticks

    /// <summary>A periodic effect's log ticks: each tick's reserved log time (null without one) and each predicted time, paired in order.</summary>
    private sealed class EffectTicks
    {
        public readonly LinkedList<double?> Reserved = new();
        public readonly Queue<SimTime> Predicted = new();
        public IReadOnlyList<UnitRef>? Group;
        public int ReservedCount;
        /// <summary>The effect's <see cref="PeriodicEffect.TicksFired"/> at the latest ask: equal now when that tick has not fired.</summary>
        public int TicksFiredAtLastAsk = -1;
        public double? LastAt;
        public IReadOnlyList<UnitRef>? LastGroup;
    }

    private readonly Dictionary<PeriodicEffect, EffectTicks> _ticks = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The time of the unclaimed log hit with the tick's damage ID on the effect's target landing nearest the scheduled
    /// time, within half the effect's period on either side; the hit is reserved for the effect. An effect without a single
    /// target reserves a group of hits the same way. Without such a hit the tick fires at the scheduled time.
    /// </summary>
    public SimTime TickAt(in TickRequest tick, SimulationContext ctx)
    {
        if (ctx.CurrentPeriodicEffect is not { } effect) return tick.Scheduled;
        if (!_ticks.TryGetValue(effect, out var ticks))
        {
            _ticks[effect] = ticks = new EffectTicks();
            effect.TickPredicted += OnTickPredicted;
            effect.Ended += OnEffectEnded;
        }
        // The tick asked for last never fired (the effect's duration ran out first, e.g. before its partial tick): this tick
        // takes its place, and the log tick it reserved.
        if (ticks.TicksFiredAtLastAsk == effect.TicksFired)
        {
            if (ticks.LastAt is { } reservedAt)
            {
                ticks.Group = ticks.LastGroup;
                return SimTime.FromSeconds(reservedAt);
            }
            ticks.Reserved.RemoveLast();
        }
        double scheduled = tick.Scheduled.TotalSeconds;
        double half = effect.PeriodSecondsAt(ctx.CurrentTime) / 2.0;
        double? at;
        if (tick.Target is { } enemy)
        {
            at = _hits.ReserveTick(tick.DamageLogId, new UnitRef(enemy.Id, enemy.InstanceId), scheduled, half, effect)?.Time;
            ticks.LastGroup = null;
        }
        else
        {
            var group = _hits.ReserveTickGroup(tick.DamageLogId, scheduled, half, _options, effect, tick.Fraction);
            ticks.Group = ticks.LastGroup = group?.Select(h => h.Target).Distinct().ToList();
            at = group?[0].Time;
        }
        ticks.TicksFiredAtLastAsk = effect.TicksFired;
        ticks.LastAt = at;
        if (at.HasValue) ticks.ReservedCount++;
        ticks.Reserved.AddLast(at);
        PairTicks(ctx, effect, ticks);
        return at is { } time ? SimTime.FromSeconds(time) : tick.Scheduled;
    }

    private void OnTickPredicted(SimulationContext ctx, PeriodicEffect effect, SimTime predicted)
    {
        if (!_ticks.TryGetValue(effect, out var ticks)) return;
        ticks.Predicted.Enqueue(predicted);
        PairTicks(ctx, effect, ticks);
    }

    private void PairTicks(SimulationContext ctx, PeriodicEffect effect, EffectTicks ticks)
    {
        while (ticks.Reserved.Count > 0 && ticks.Predicted.Count > 0)
        {
            var reserved = ticks.Reserved.First!.Value;
            ticks.Reserved.RemoveFirst();
            var predicted = ticks.Predicted.Dequeue();
            if (reserved is { } logTime) _verifier.CheckTickTime(ctx, effect, predicted, logTime);
        }
    }

    /// <summary>The effect's reservation that has not fired is free again, and its tick count is checked.</summary>
    private void OnEffectEnded(SimulationContext ctx, PeriodicEffect effect)
    {
        if (!_ticks.Remove(effect, out var ticks)) return;
        _hits.Release(effect);
        // The last tick asked for did not fire when the effect ended before it.
        int reserved = ticks.ReservedCount - (ticks.TicksFiredAtLastAsk == effect.TicksFired && ticks.LastAt.HasValue ? 1 : 0);
        _verifier.CheckTickCount(ctx, effect, reserved);
    }

    // ------------------------------------------------------------------ procs

    public bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null)
    {
        if (!ProcStats.TryGetValue(procKey, out var stats))
        {
            stats = new ProcRollStats(procKey);
            ProcStats[procKey] = stats;
        }
        stats.Rolls++;
        stats.ExpectedProcs += Math.Clamp(chance, 0.0, 1.0);

        var evidence = ProcEvidence.For(procKey);
        if (evidence == null)
        {
            _verifier.Report(ctx, IssueCategory.Procs, IssueEntity.Proc(procKey), $"Proc '{procKey}' has no evidence listed in ProcEvidence; treated as not procced", $"proc without evidence: {procKey}");
            return false;
        }

        double now = ctx.CurrentTime.TotalSeconds;
        bool procced = evidence.Resource != null
            ? ClaimResourceEvidence(evidence, now)
            : evidence.EnemyDebuffApplied
            ? ClaimEnemyDebuffEvidence(evidence, now, target)
            : evidence.StackJump > 0
            ? ClaimStackEvidence(evidence.LogId, evidence.StackJump, now, target)
            : _playerAuraIds.Contains(evidence.LogId)
            ? ClaimAuraEvidence(evidence.LogId, now, evidence.NotCausedByAbilityId)
            : ClaimDamageEvidence(evidence, now, target);
        if (procced) stats.Procs++;
        _verifier.CheckProc(ctx, procKey, chance, procced, target);
        return procced;
    }

    /// <summary>
    /// Proof of a proc that gives a resource at once: the resource rises by the proc's gain (within 30%) between two
    /// readings, the first at most a frame after the roll and the second within 0.5s of it, beyond what it regenerates
    /// meanwhile. A reading at a cast is before its cost, so a cast's reading never starts a jump.
    /// </summary>
    private bool ClaimResourceEvidence(ProcEvidence.Evidence evidence, double now)
    {
        if (!_resourceJumps.TryGetValue(evidence, out var jumps))
        {
            jumps = ResourceJumps(evidence);
            _resourceJumps[evidence] = jumps;
        }
        for (int i = 0; i < jumps.Count; i++)
        {
            var jump = jumps[i];
            if (jump.To < now - _options.SameTickSeconds) continue;
            if (jump.From > now + PeriodicEffect.ServerFrameSeconds) break;
            if (jump.To > now + ResourceEvidenceWindowSeconds || _claimedResourceJumps.Contains((evidence, i))) continue;
            _claimedResourceJumps.Add((evidence, i));
            return true;
        }
        return false;
    }

    private List<(double From, double To)> ResourceJumps(ProcEvidence.Evidence evidence)
    {
        var jumps = new List<(double From, double To)>();
        var log = LogResources.All.FirstOrDefault(r => r.Resource == evidence.Resource);
        if (log == null) return jumps;
        string type = log.LogResourceType.ToString();
        double max = evidence.Resource!.Max;
        LogResourceSnapshot? previous = null;
        double previousAmount = 0;
        foreach (var reading in _resourceReadings)
        {
            if (!reading.Resources.TryGetValue(type, out var value)) continue;
            double amount = log.EngineAmount(value);
            if (previous != null && previous.Event != "cast" && reading.Time - previous.Time <= ResourceEvidenceWindowSeconds
                && amount < max - 0.5 && previousAmount < max - 0.5)
            {
                double excess = amount - previousAmount - evidence.ResourceRegenPerSecond * (reading.Time - previous.Time);
                if (Math.Abs(excess - evidence.ResourceGain) <= 0.3 * evidence.ResourceGain) jumps.Add((previous.Time, reading.Time));
            }
            previous = reading;
            previousAmount = amount;
        }
        return jumps;
    }

    private const double ResourceEvidenceWindowSeconds = 0.5;
    private readonly List<LogResourceSnapshot> _resourceReadings = [];
    private readonly Dictionary<ProcEvidence.Evidence, List<(double From, double To)>> _resourceJumps = new();
    private readonly HashSet<(ProcEvidence.Evidence, int)> _claimedResourceJumps = new();

    /// <summary>Proof of a proc that adds extra stacks: the debuff on <paramref name="target"/> gains <paramref name="jump"/> stacks at once.</summary>
    private bool ClaimStackEvidence(int logId, int jump, double now, EnemyTarget? target)
    {
        if (target == null || !_enemyStackGains.TryGetValue((logId, new UnitRef(target.Id, target.InstanceId)), out var gains)) return false;
        // The gain closest to the roll: the hit's own (engine hits can land a few server frames from the log's, and the
        // hits that add stacks are at least 0.17s apart).
        var gain = gains
            .Where(g => Math.Abs(g.Event.Time - now) <= StackEvidenceWindowSeconds && !_claimedStackEvidence.Contains(g.Event))
            .OrderBy(g => Math.Abs(g.Event.Time - now))
            .FirstOrDefault();
        if (gain.Event == null) return false;
        _claimedStackEvidence.Add(gain.Event);
        int added = gain.Added;
        if (added < jump)
        {
            // A new debuff given several stacks at once is logged as its application (1 stack) and its stack count in the
            // same millisecond (combat logs: all 220 Coalescing Frost applications with 2 stacks in the 15 Rime fights of
            // docs/accuracy/rime-accuracy-v1.md, each on a Freezing Torrent crit).
            foreach (var g in gains)
            {
                if (g.Event.Time != gain.Event.Time || _claimedStackEvidence.Contains(g.Event)) continue;
                _claimedStackEvidence.Add(g.Event);
                added += g.Added;
            }
        }
        return added >= jump;
    }

    private bool ClaimAuraEvidence(int logId, double now, int notCausedBy = 0)
    {
        foreach (var ev in _playerAuraEvents)
        {
            if (ev.Time < now - _options.ProcEarlySeconds) continue;
            if (ev.Time > now + _options.ProcAuraWindowSeconds) break;
            if (ev.AbilityId != logId || !ev.IsGain || _claimedAuraEvidence.Contains(ev)) continue;
            if (notCausedBy != 0 && ev.ExtraAbilityId == notCausedBy) continue;
            _claimedAuraEvidence.Add(ev);
            return true;
        }
        return false;
    }

    /// <summary>A group of log hits with the proc's damage ID landing at the roll (within one server tick, plus the effect's delay).</summary>
    private bool ClaimDamageEvidence(ProcEvidence.Evidence evidence, double now, EnemyTarget? target)
    {
        // Hits an engine hit already accounts for (matched, or a group claimed for its targets) are not evidence of a proc.
        // A hit type proves a proc of the hit itself: its group is claimed before the roll (a Wild Swing the enemy avoided).
        bool Free(LogDamage h) => !_claimedDamageEvidence.Contains(h) && (evidence.HitTypes != null || !_hits.IsAccountedFor(h));
        int logId = evidence.LogId;
        var hit = _hits.Peek(logId, now + evidence.MinDelaySeconds - _options.SameTickSeconds, now + _options.SameTickSeconds + evidence.DelaySeconds,
            h => Free(h) && (evidence.HitTypes == null || (h.HitType is { } type && evidence.HitTypes.Contains(type)))
                 && (target == null || evidence.AnyEnemy || (h.Target.Id == target.Id && h.Target.Instance == target.InstanceId))
                 && (evidence.StackLossLogId == 0 || LostStackAt(evidence.StackLossLogId, h.Target, h.Time) == evidence.StackLoss)
                 && (evidence.QuietBeforeSeconds == 0.0 || _hits.Peek(logId, h.Time - evidence.QuietBeforeSeconds, h.Time - _options.SameTickSeconds, _ => true) == null));
        if (hit == null) return false;
        if (evidence.ReservesHit)
        {
            // The proc's own hit on its enemy takes the proof, not an earlier log hit still free; the hits on other
            // enemies in the same server frame are other procs.
            _hits.ReserveForMatch(hit);
            _claimedDamageEvidence.Add(hit);
            return true;
        }
        // The whole multi-target group is one proc.
        foreach (var h in _hits.GroupOf(hit, _options)) _claimedDamageEvidence.Add(h);
        // So are the hits the proc deals after it, on any enemy (a summoned attacker's later attacks): each the first free
        // hit from 3/4 to 3/2 of the spacing after the one before.
        double previous = hit.Time;
        for (int k = 1; k <= evidence.FollowingHits; k++)
        {
            var following = _hits.Peek(logId, previous + 0.75 * evidence.FollowingSpacingSeconds - evidence.FollowingSlackSeconds,
                previous + 1.5 * evidence.FollowingSpacingSeconds + evidence.FollowingSlackSeconds, Free);
            if (following == null) break;
            _claimedDamageEvidence.Add(following);
            previous = following.Time;
        }
        return true;
    }
}

/// <summary>
/// The log's damage hits, consumed as the engine produces matching hits. A log hit matches an engine hit with the
/// same damage ID on the same target, landing between slightly before and up to the travel window after the engine hit.
/// A periodic effect's tick reserves its log hit first; a log hit reserved by one effect is not free for another.
/// </summary>
public sealed class HitLedger
{
    private readonly Dictionary<(int AbilityId, int TargetId, int Instance), List<LogDamage>> _byKey = new();
    private readonly Dictionary<int, List<LogDamage>> _byAbility = new();
    private readonly List<LogDamage> _all;
    private readonly HashSet<LogDamage> _matched = new();
    private readonly HashSet<LogDamage> _dealt = new();

    public HitLedger(IEnumerable<LogDamage> hits)
    {
        _all = hits.OrderBy(h => h.Time).ToList();
        foreach (var h in _all)
        {
            var key = (h.AbilityId, h.Target.Id, h.Target.Instance);
            if (!_byKey.TryGetValue(key, out var list)) _byKey[key] = list = new List<LogDamage>();
            list.Add(h);
            if (!_byAbility.TryGetValue(h.AbilityId, out var byAbility)) _byAbility[h.AbilityId] = byAbility = new List<LogDamage>();
            byAbility.Add(h);
        }
    }

    /// <summary>Every log hit, in time order.</summary>
    public IReadOnlyList<LogDamage> All => _all;

    /// <param name="owner">The periodic effect ticking now: its reserved log hit on the target is taken first.</param>
    /// <param name="reservedOnly">Take only a log hit <paramref name="owner"/> reserved (the hit of a tick that follows the log).</param>
    public LogDamage? Match(int logId, UnitRef target, double engineTime, ReplayOptions options, PeriodicEffect? owner = null,
        bool reservedOnly = false)
    {
        // A tick takes the log hit its effect reserved: the one nearest now.
        if (owner != null && _owned.TryGetValue(owner, out var owned))
        {
            LogDamage? best = null;
            foreach (var h in owned)
            {
                if (h.AbilityId != logId || h.Target != target || _matched.Contains(h) || _dealt.Contains(h)) continue;
                if (best == null || Math.Abs(h.Time - engineTime) < Math.Abs(best.Time - engineTime)) best = h;
            }
            if (best != null)
            {
                _matched.Add(best);
                return best;
            }
        }
        if (reservedOnly) return null;
        // A hit that chose its target from a log hit takes that hit.
        if (_reserved.TryGetValue((logId, target), out var queue))
        {
            while (queue.Count > 0)
            {
                var reserved = queue.Dequeue();
                // A reservation the engine never used is stale once the hit is behind the window.
                if (reserved.Time < engineTime - options.HitEarlySeconds) continue;
                if (!_dealt.Contains(reserved) && _matched.Add(reserved)) return reserved;
            }
        }
        if (!_byKey.TryGetValue((logId, target.Id, target.Instance), out var list)) return null;
        foreach (var h in list)
        {
            if (h.Time < engineTime - options.HitEarlySeconds) continue;
            if (h.Time > engineTime + options.LateSecondsOf(logId)) break;
            if (_matched.Contains(h) || _dealt.Contains(h) || OwnedByOther(h, owner)) continue;
            _matched.Add(h);
            return h;
        }
        return null;
    }

    // ------------------------------------------------------------------ periodic tick reservations

    private readonly Dictionary<LogDamage, PeriodicEffect> _ownerOf = new();
    private readonly Dictionary<PeriodicEffect, List<LogDamage>> _owned = new(ReferenceEqualityComparer.Instance);

    /// <summary>True when an active periodic effect other than <paramref name="owner"/> reserved this log hit.</summary>
    private bool OwnedByOther(LogDamage h, PeriodicEffect? owner) =>
        _ownerOf.TryGetValue(h, out var o) && o != owner && o.IsActive;

    /// <remarks>
    /// A log hit an engine hit chose as its target and has not matched yet is free for a tick: the effect's own cast
    /// chooses its enemies from its first tick's hits (Slaughter).
    /// </remarks>
    private bool FreeForTick(LogDamage h, PeriodicEffect owner) =>
        !_matched.Contains(h) && !_dealt.Contains(h) && !OwnedByOther(h, owner) && !(_ownerOf.TryGetValue(h, out var o) && o == owner);

    private void Own(LogDamage h, PeriodicEffect owner)
    {
        _ownerOf[h] = owner;
        if (!_owned.TryGetValue(owner, out var list)) _owned[owner] = list = new List<LogDamage>();
        list.Add(h);
    }

    /// <summary>
    /// Reserves for <paramref name="owner"/> the free log hit with <paramref name="logId"/> on <paramref name="target"/>
    /// landing nearest <paramref name="scheduled"/>, within <paramref name="halfWindow"/> on either side.
    /// </summary>
    public LogDamage? ReserveTick(int logId, UnitRef target, double scheduled, double halfWindow, PeriodicEffect owner)
    {
        if (!_byKey.TryGetValue((logId, target.Id, target.Instance), out var list)) return null;
        LogDamage? best = null;
        foreach (var h in list)
        {
            if (h.Time < scheduled - halfWindow) continue;
            if (h.Time > scheduled + halfWindow) break;
            if (!FreeForTick(h, owner)) continue;
            if (best == null || Math.Abs(h.Time - scheduled) < Math.Abs(best.Time - scheduled)) best = h;
        }
        if (best != null) Own(best, owner);
        return best;
    }

    /// <summary>
    /// Reserves for <paramref name="owner"/> the free group of log hits with <paramref name="logId"/> landing nearest
    /// <paramref name="scheduled"/>, within <paramref name="halfWindow"/> on either side (a pulse on every enemy in its area).
    /// </summary>
    public IReadOnlyList<LogDamage>? ReserveTickGroup(int logId, double scheduled, double halfWindow, ReplayOptions options, PeriodicEffect owner,
        double tickFraction = 1.0)
    {
        List<LogDamage>? best = null;
        double bestDistance = double.MaxValue;
        foreach (var group in Groups(logId, options))
        {
            var first = group[0];
            if (first.Time < scheduled - halfWindow) continue;
            if (first.Time > scheduled + halfWindow) break;
            if (_claimedGroups.Contains(first) || !group.All(h => FreeForTick(h, owner))) continue;
            double distance = Math.Abs(first.Time - scheduled);
            bool looksPartial = GroupRatio(group, options) < options.PartialTickRatio;
            if (looksPartial != tickFraction < 1.0) distance += options.PartialMismatchPenaltySeconds;
            if (distance < bestDistance)
            {
                best = group;
                bestDistance = distance;
            }
        }
        if (best == null) return null;
        _claimedGroups.Add(best[0]);
        foreach (var h in best) Own(h, owner);
        return best;
    }

    /// <summary>The log hits <paramref name="owner"/> reserved and no engine hit took are free again.</summary>
    public void Release(PeriodicEffect owner)
    {
        if (!_owned.Remove(owner, out var list)) return;
        foreach (var h in list)
        {
            _ownerOf.Remove(h);
            if (!_matched.Contains(h) && _groupOf.TryGetValue(h, out var group) && group[0] == h) _claimedGroups.Remove(h);
        }
    }

    // ------------------------------------------------------------------ target choices

    private readonly HashSet<LogDamage> _claimedGroups = new();
    private readonly HashSet<LogDamage> _claimedGroupHits = new();

    /// <summary>True when an engine hit has matched this log hit or claimed its group for targeting.</summary>
    public bool IsAccountedFor(LogDamage hit) => _matched.Contains(hit) || _claimedGroupHits.Contains(hit) || _ownerOf.ContainsKey(hit);

    /// <summary>True when an engine hit matched this log hit.</summary>
    public bool IsMatched(LogDamage hit) => _matched.Contains(hit);

    /// <summary>The replay dealt this log hit itself (no engine hit matched it in its window).</summary>
    public void MarkDealt(LogDamage hit) => _dealt.Add(hit);

    private readonly HashSet<LogDamage> _targetChoices = new();
    private readonly Dictionary<(int LogId, UnitRef Unit), Queue<LogDamage>> _reserved = new();

    /// <summary>The next engine hit with the ID of <paramref name="h"/> on its target matches <paramref name="h"/>.</summary>
    public void ReserveForMatch(LogDamage h) => Reserve(h);

    private void Reserve(LogDamage h)
    {
        _targetChoices.Add(h);
        var key = (h.AbilityId, h.Target);
        if (!_reserved.TryGetValue(key, out var queue)) _reserved[key] = queue = new Queue<LogDamage>();
        queue.Enqueue(h);
    }

    /// <summary>True when the log has a hit with <paramref name="logId"/> on <paramref name="unit"/> from <paramref name="from"/> to <paramref name="to"/> that is still free.</summary>
    public bool HasUnclaimed(int logId, UnitRef unit, double from, double to)
    {
        if (!_byKey.TryGetValue((logId, unit.Id, unit.Instance), out var list)) return false;
        foreach (var h in list)
        {
            if (h.Time < from) continue;
            if (h.Time > to) break;
            if (!_matched.Contains(h) && !_targetChoices.Contains(h) && !_dealt.Contains(h)) return true;
        }
        return false;
    }

    private readonly Dictionary<int, List<double>> _travel = new();

    /// <summary>Records how long after its release a matched hit landed (learns each ID's typical travel time).</summary>
    public void RecordTravel(int logId, double seconds)
    {
        if (!_travel.TryGetValue(logId, out var list)) _travel[logId] = list = new List<double>();
        list.Add(seconds);
    }

    public double TypicalTravelSeconds(int logId, ReplayOptions options) => TypicalTravel(logId, options);

    /// <summary>The longest travel seen for this damage ID (recent hits), or the default before any.</summary>
    public double LongestTravelSeconds(int logId, ReplayOptions options) =>
        _travel.TryGetValue(logId, out var list) && list.Count > 0 ? Math.Max(options.DefaultTravelSeconds, list.Skip(Math.Max(0, list.Count - 50)).Max()) : options.DefaultTravelSeconds;

    private double TypicalTravel(int logId, ReplayOptions options)
    {
        if (!_travel.TryGetValue(logId, out var list) || list.Count == 0) return options.DefaultTravelSeconds;
        var recent = list.Skip(Math.Max(0, list.Count - 50)).OrderBy(x => x).ToList();
        return recent[recent.Count / 2];
    }

    /// <summary>
    /// For a hit that picks its own target: the free log hit with <paramref name="logId"/> landing closest to the release
    /// plus the ID's typical travel time; hits on <paramref name="avoid"/> (the hero's current target) are only taken
    /// when clearly closer, as hits that can only go there share the ID; hits on <paramref name="prefer"/> win a tie
    /// (within <see cref="ReplayOptions.SameTickSeconds"/>).
    /// </summary>
    public UnitRef? NextUnclaimedTarget(int logId, double engineTime, ReplayOptions options, UnitRef? avoid = null, UnitRef? prefer = null)
    {
        if (!_byAbility.TryGetValue(logId, out var list)) return null;
        // An ID that does not travel (its own late window) lands at the engine's time; learning a travel time from it
        // would carry one mispairing into the next.
        double expected = engineTime + (options.HitLateSecondsById.ContainsKey(logId) ? 0.0 : TypicalTravel(logId, options));
        LogDamage? best = null;
        double bestScore = double.MaxValue;
        foreach (var h in list)
        {
            if (h.Time < engineTime - options.HitEarlySeconds) continue;
            if (h.Time > engineTime + options.LateSecondsOf(logId)) break;
            if (_matched.Contains(h) || _targetChoices.Contains(h) || _dealt.Contains(h) || OwnedByOther(h, null)) continue;
            double score = Math.Abs(h.Time - expected) + (avoid != null && h.Target == avoid ? options.AvoidedTargetPenaltySeconds : 0.0)
                           - (prefer != null && h.Target == prefer ? options.SameTickSeconds : 0.0);
            if (score < bestScore)
            {
                best = h;
                bestScore = score;
            }
        }
        if (best == null) return null;
        Reserve(best);
        return best.Target;
    }

    private readonly Dictionary<int, List<List<LogDamage>>> _groups = new();
    private readonly Dictionary<LogDamage, List<LogDamage>> _groupOf = new();

    /// <summary>
    /// The hits with <paramref name="logId"/> split into multi-target groups: hits landing within
    /// <see cref="ReplayOptions.HitGroupSeconds"/> of the group's first hit, each target at most once (a second hit on
    /// a target is another pulse landing in the same server frame).
    /// </summary>
    private List<List<LogDamage>> Groups(int logId, ReplayOptions options)
    {
        if (_groups.TryGetValue(logId, out var groups)) return groups;
        groups = new List<List<LogDamage>>();
        if (_byAbility.TryGetValue(logId, out var list))
        {
            List<LogDamage>? current = null;
            foreach (var h in list)
            {
                if (current == null || h.Time - current[0].Time > options.GroupSecondsOf(logId) || current.Any(x => x.Target == h.Target))
                {
                    // A repeated target opens a new group only if no later open group can take it.
                    current = groups.Count > 0 && h.Time - groups[^1][0].Time <= options.GroupSecondsOf(logId)
                              && current != null && current != groups[^1] && !groups[^1].Any(x => x.Target == h.Target)
                        ? groups[^1]
                        : null;
                    if (current == null)
                    {
                        current = new List<LogDamage>();
                        groups.Add(current);
                    }
                }
                current.Add(h);
                _groupOf[h] = current;
            }
        }
        _groups[logId] = groups;
        return groups;
    }

    /// <remarks>
    /// Between groups landing within a few milliseconds of each other, a partial tick (<paramref name="tickFraction"/>
    /// below 1) prefers the group that deals clearly less than the previous one on the same targets, and a full tick the
    /// group that does not (a proc pulse and a partial final pulse can land in the same server frame).
    /// </remarks>
    public IReadOnlyList<UnitRef>? ClaimGroup(int logId, double engineTime, ReplayOptions options, double tickFraction = 1.0)
    {
        // The nearest unclaimed group in time: engine and log tick times differ by a few milliseconds
        // (server frames), and travelling hits land after the engine releases them.
        List<LogDamage>? best = null;
        double bestDistance = double.MaxValue;
        foreach (var group in Groups(logId, options))
        {
            var first = group[0];
            if (first.Time < engineTime - options.HitEarlySeconds) continue;
            if (first.Time > engineTime + options.LateSecondsOf(logId)) break;
            if (_claimedGroups.Contains(first) || group.Any(h => OwnedByOther(h, null) || _dealt.Contains(h))) continue;
            double distance = Math.Abs(first.Time - engineTime);
            bool looksPartial = GroupRatio(group, options) < options.PartialTickRatio;
            if (looksPartial != tickFraction < 1.0) distance += options.PartialMismatchPenaltySeconds;
            if (distance < bestDistance)
            {
                best = group;
                bestDistance = distance;
            }
        }
        if (best == null) return null;
        _claimedGroups.Add(best[0]);
        foreach (var h in best)
        {
            _claimedGroupHits.Add(h);
            Reserve(h); // the hits on these targets match this group, not another group's
        }
        return best.Select(h => h.Target).Distinct().ToList();
    }

    /// <summary>Median of each hit's damage over the previous hit with the same ID, target and crit status (1 if unknown).</summary>
    private double GroupRatio(List<LogDamage> group, ReplayOptions options)
    {
        var list = _byAbility[group[0].AbilityId];
        var ratios = new List<double>();
        foreach (var h in group)
        {
            var prev = list.LastOrDefault(p => p.Time < group[0].Time - options.GroupSecondsOf(group[0].AbilityId) && p.Time > h.Time - 3.0
                                               && p.Target == h.Target && p.Crit == h.Crit && p.TotalAmount > 0);
            if (prev != null) ratios.Add(h.TotalAmount / prev.TotalAmount);
        }
        if (ratios.Count == 0) return 1.0;
        ratios.Sort();
        return ratios[ratios.Count / 2];
    }

    /// <summary>All hits of the multi-target group <paramref name="hit"/> belongs to.</summary>
    public IEnumerable<LogDamage> GroupOf(LogDamage hit, ReplayOptions options)
    {
        Groups(hit.AbilityId, options);
        return _groupOf.TryGetValue(hit, out var group) ? group : new List<LogDamage> { hit };
    }

    public LogDamage? Peek(int logId, double from, double to, Func<LogDamage, bool> filter)
    {
        if (!_byAbility.TryGetValue(logId, out var list)) return null;
        foreach (var h in list)
        {
            if (h.Time < from) continue;
            if (h.Time > to) break;
            if (filter(h)) return h;
        }
        return null;
    }

    public int TotalHits => _all.Count;
    public int MatchedHits => _matched.Count;
}

/// <summary>
/// One engine hit and its log hit, or a log hit no engine hit matched (<see cref="EngineAmount"/> 0, no
/// <see cref="LandedTime"/>).
/// </summary>
/// <param name="EngineAmount">The engine's average roll: base damage, times the crit multiplier on a logged crit.</param>
public sealed record HitPair(
    double EngineTime,
    string AbilityName,
    int AbilityId,
    UnitRef? Target,
    double EngineAmount,
    LogDamage? Log,
    bool Crit,
    double CritChance,
    double TargetHealthFraction)
{
    /// <summary>The low end of the engine's damage range for the hit.</summary>
    public double ExpectedLow { get; init; }
    /// <summary>The high end of the engine's damage range for the hit.</summary>
    public double ExpectedHigh { get; init; }

    /// <summary>The engine's state at the check (landing, or release for a hit that never landed).</summary>
    public EngineState? State { get; init; }

    /// <summary>Below 1 for a partial final tick of a periodic effect.</summary>
    public double TickFraction { get; init; } = 1.0;

    /// <summary>When the engine's hit landed (dealt its damage); null if it never landed.</summary>
    public double? LandedTime { get; init; }

    /// <summary>Log over engine, with the log hit before its block (the engine has no blocks).</summary>
    public double? Ratio => Log == null || EngineAmount <= 0 ? null : Log.UnblockedAmount / EngineAmount;

    /// <summary>The log amount (0 with no log hit) minus the range bound it is outside; 0 inside the range.</summary>
    public double Deviation
    {
        get
        {
            double observed = Log?.UnblockedAmount ?? 0.0;
            return observed < ExpectedLow ? observed - ExpectedLow : observed > ExpectedHigh ? observed - ExpectedHigh : 0.0;
        }
    }

    /// <summary>True when the log amount is inside the engine's damage range.</summary>
    public bool WithinRange => Log != null && Deviation == 0.0;
}

public sealed class ProcRollStats
{
    public ProcRollStats(string key) => Key = key;
    public string Key { get; }
    public int Rolls { get; set; }
    public double ExpectedProcs { get; set; }
    public int Procs { get; set; }
}
