namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Answers the engine's undecided questions from the combat log during a replay:
///   - each engine hit is matched to the log hit with the same damage ID on the same target; the log decides
///     whether it crit and when it landed (projectile travel),
///   - each proc roll succeeds if the log shows what that proc produces (its evidence log ID) at the roll.
/// Damage spread is not rolled: the engine's average roll is compared against the log instead.
/// Anything the log cannot explain is recorded as a <see cref="ReplayIssue"/>.
/// </summary>
public sealed class LogOutcomeSource : IOutcomeSource
{
    private readonly ReplayOptions _options;
    private readonly HitLedger _hits;
    private readonly List<LogAura> _playerAuraEvents;
    private readonly HashSet<int> _playerAuraIds;
    private readonly HashSet<LogAura> _claimedAuraEvidence = new();
    private readonly HashSet<LogDamage> _claimedDamageEvidence = new();

    public LogOutcomeSource(ReplayScript script, ReplayOptions options, HitLedger hits)
    {
        _options = options;
        _hits = hits;
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
            foreach (var a in group.OrderBy(a => a.Time))
            {
                int now = a.Type is "removedebuff" or "removebuff" ? 0 : a.Stack ?? (a.IsGain ? Math.Max(stacks, 1) : stacks);
                if (a.IsGain && now > stacks) list.Add((a, now - stacks));
                stacks = now;
            }
            _enemyStackGains[group.Key] = list;
        }
        foreach (var c in script.Commands.Where(c => c.Kind == CommandKind.EnemyDemise && c.Enemy != null))
        {
            _demise[new UnitRef(c.Enemy!.Id, c.Enemy.Instance)] = c.Time;
        }
    }

    private readonly Dictionary<UnitRef, double> _demise = new();
    private readonly Dictionary<(int LogId, UnitRef Target), List<(LogAura Event, int Added)>> _enemyStackGains = new();
    private readonly HashSet<LogAura> _claimedStackEvidence = new();
    private const double StackEvidenceWindowSeconds = 0.1;

    /// <summary>Hits that would have landed on an enemy already dead in the log (the projectile hit nothing).</summary>
    public int MissedHits { get; private set; }

    /// <summary>Set by the driver: issues and hits are attributed to this step.</summary>
    public ReplayStep? CurrentStep { get; set; }

    public List<ReplayIssue> PendingIssues { get; } = new();
    public Dictionary<string, ProcRollStats> ProcStats { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<HitPair> AllHits { get; } = new();

    /// <summary>
    /// The enemies the log shows this hit striking: the earliest not-yet-used group of log hits with this damage ID
    /// (hits landing together) within the travel window. Enemies the engine does not know are skipped.
    /// Without such a group the engine's own candidates are used (and reported as extra hits).
    /// </summary>
    public IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx)
    {
        var group = _hits.ClaimGroup(damageLogId, ctx.CurrentTime.TotalSeconds, _options, ctx.CurrentTickFraction);
        if (group == null)
        {
            Report(ctx, IssueCategory.Hits, $"Engine multi-target hit {damageLogId} at {ctx.CurrentTime} has no log hits", $"extra engine multi-target hit: {damageLogId}");
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
        if (preferred != null)
        {
            var unitRef = new UnitRef(preferred.Id, preferred.InstanceId);
            if (_hits.HasUnclaimed(damageLogId, unitRef, ctx.CurrentTime.TotalSeconds, _options)) return preferred;
            var other = _hits.NextUnclaimedTarget(damageLogId, ctx.CurrentTime.TotalSeconds, _options);
            return (other == null ? null : ctx.FindEnemy(other.Id, other.Instance)) ?? preferred;
        }
        // Hits on the current target are left for hits that can only go there (e.g. Cascading Blitz swallows share the ID).
        var current = ctx.Player.CurrentTarget;
        var avoid = current == null ? null : new UnitRef(current.Id, current.InstanceId);
        var unit = _hits.NextUnclaimedTarget(damageLogId, ctx.CurrentTime.TotalSeconds, _options, avoid);
        var enemy = unit == null ? null : ctx.FindEnemy(unit.Id, unit.Instance);
        return enemy ?? (candidates.Count > 0 ? candidates[0] : null);
    }

    /// <summary>On release: match the log hit, which decides the crit and when the hit lands.</summary>
    public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx)
    {
        var target = hit.Target == null ? null : new UnitRef(hit.Target.Id, hit.Target.InstanceId);
        double now = ctx.CurrentTime.TotalSeconds;
        var match = target == null ? null : _hits.Match(hit.AbilityId, target, now, _options);
        if (match != null) _hits.RecordTravel(hit.AbilityId, match.Time - now);

        if (match == null && _options.MissHitsOnDyingTargets && target != null && _demise.TryGetValue(target, out var died)
            && died >= now - _options.SameTickSeconds && died <= now + _hits.LongestTravelSeconds(hit.AbilityId, _options) + PeriodicEffect.ServerFrameSeconds)
        {
            // The target dies in the log before this hit could land (the log has no hit for it, and its flight can be as
            // long as the longest seen; Frost Bolt's varies with distance, 0.03-1.9s): it hits nothing (no damage, no
            // procs, no counters).
            MissedHits++;
            return new HitOutcome(false, SimTime.Zero, 1.0, null, Missed: true);
        }

        if (match == null)
        {
            Report(ctx, IssueCategory.Hits,
                $"Engine hit {hit.AbilityName} ({hit.AbilityId}) on {target?.ToString() ?? "no target"} has no matching log hit",
                $"extra engine hit: {hit.AbilityName} ({hit.AbilityId})");
            return new HitOutcome(false, SimTime.Zero, 1.0, new PendingHit(now, null));
        }

        if (match.Crit && !hit.CanCrit)
        {
            Report(ctx, IssueCategory.Hits,
                $"Log hit {match.AbilityName} at {match.Time:F3} crit, but the engine says {hit.AbilityName} cannot crit",
                $"crit on non-critable: {hit.AbilityName}");
        }

        var delay = SimTime.FromSeconds(Math.Max(0.0, match.Time - now));
        return new HitOutcome(match.Crit, delay, 1.0, new PendingHit(now, match));
    }

    /// <summary>On landing: the engine's damage (average roll) is compared to the log hit.</summary>
    public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx)
    {
        var pending = (PendingHit)outcome.Token!;
        var target = landed.Target == null ? null : new UnitRef(landed.Target.Id, landed.Target.InstanceId);
        var context = new HitContext(
            ctx.ActiveEnemies.Count(),
            landed.Source.Auras.Active(ctx.CurrentTime).Where(a => a.LogId > 0).Select(a => a.LogId).ToArray(),
            landed.Source.Auras.Active(ctx.CurrentTime).Where(a => a.LogId == 0).Select(a => a.Name).ToArray());
        double health = landed.Target?.HealthFraction ?? 1.0;

        if (pending.Log == null)
        {
            double expected = landed.CanCrit ? 1.0 + (landed.CritChance * (landed.CritMultiplier - 1.0)) : 1.0;
            Record(new HitPair(pending.ReleasedAt, landed.AbilityName, landed.AbilityId, target, landed.BaseDamage * expected, null, landed.CanCrit && landed.CritChance >= 1.0, landed.CritChance, health)
            {
                // An engine hit with no log hit still dealt its damage: it counts in the engine's totals.
                Context = context,
                TickFraction = landed.TickFraction,
                LandedTime = ctx.CurrentTime.TotalSeconds,
            });
            return expected;
        }

        var match = pending.Log;
        double multiplier = match.Crit ? landed.CritMultiplier : 1.0;
        var pair = new HitPair(pending.ReleasedAt, landed.AbilityName, landed.AbilityId, target, landed.BaseDamage * multiplier, match, match.Crit, landed.CritChance, health)
        {
            Context = context,
            TickFraction = landed.TickFraction,
            LandedTime = ctx.CurrentTime.TotalSeconds
        };
        Record(pair);

        if (!_options.UnverifiedDamageIds.Contains(landed.AbilityId) && !pair.WithinSpread(_options))
        {
            Report(ctx, IssueCategory.Damage,
                $"{landed.AbilityName} on {target} at {match.Time:F3}: log {match.TotalAmount:F0}{(match.Crit ? " (crit)" : "")} vs engine {pair.EngineAmount:F0} " +
                $"(ratio {pair.Ratio:F3}, allowed {1 - _options.DamageSpread:F2}-{(1 + _options.DamageSpread) * (1 + _options.UnexplainedHighMargin):F3})",
                $"damage {(pair.Ratio > 1 ? "too low" : "too high")}: {landed.AbilityName} ({landed.AbilityId})");
        }
        return multiplier;
    }

    private sealed record PendingHit(double ReleasedAt, LogDamage? Log);

    public void HitFinished(SimulationContext ctx) { }

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
            Report(ctx, IssueCategory.Procs, $"Proc '{procKey}' has no evidence listed in ProcEvidence; treated as not procced", $"proc without evidence: {procKey}");
            return false;
        }

        double now = ctx.CurrentTime.TotalSeconds;
        bool procced = evidence.Resource != null
            ? ClaimResourceEvidence(evidence, now)
            : evidence.StackJump > 0
            ? ClaimStackEvidence(evidence.LogId, evidence.StackJump, now, target)
            : _playerAuraIds.Contains(evidence.LogId)
            ? ClaimAuraEvidence(evidence.LogId, now)
            : ClaimDamageEvidence(evidence, now, target);
        if (procced)
        {
            stats.Procs++;
            if (chance <= 0.0)
            {
                Report(ctx, IssueCategory.Procs, $"Log shows '{procKey}' procced at {now:F3} but the engine's chance was {chance:P1}", $"proc with zero chance: {procKey}");
            }
        }
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
        return gain.Added >= jump;
    }

    private bool ClaimAuraEvidence(int logId, double now)
    {
        foreach (var ev in _playerAuraEvents)
        {
            if (ev.Time < now - _options.ProcEarlySeconds) continue;
            if (ev.Time > now + _options.ProcAuraWindowSeconds) break;
            if (ev.AbilityId != logId || !ev.IsGain || _claimedAuraEvidence.Contains(ev)) continue;
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
                 && (target == null || (h.Target.Id == target.Id && h.Target.Instance == target.InstanceId)));
        if (hit == null) return false;
        // The whole multi-target group is one proc.
        foreach (var h in _hits.GroupOf(hit, _options)) _claimedDamageEvidence.Add(h);
        // So are the hits the proc deals after it, on any enemy (a summoned attacker's later attacks): each the first free
        // hit from 3/4 to 3/2 of the spacing after the one before.
        double previous = hit.Time;
        for (int k = 1; k <= evidence.FollowingHits; k++)
        {
            var following = _hits.Peek(logId, previous + 0.75 * evidence.FollowingSpacingSeconds, previous + 1.5 * evidence.FollowingSpacingSeconds, Free);
            if (following == null) break;
            _claimedDamageEvidence.Add(following);
            previous = following.Time;
        }
        return true;
    }

    private void Record(HitPair pair)
    {
        AllHits.Add(pair);
        CurrentStep?.Hits.Add(pair);
    }

    public void Report(SimulationContext ctx, IssueCategory category, string message, string? subject = null)
    {
        var issue = new ReplayIssue(ctx.CurrentTime.TotalSeconds, CurrentStep?.Index ?? 0, category, message, subject);
        PendingIssues.Add(issue);
        CurrentStep?.Issues.Add(issue);
    }
}

/// <summary>
/// The log's damage hits, consumed as the engine produces matching hits. A log hit matches an engine hit with the
/// same damage ID on the same target, landing between slightly before and up to the travel window after the engine hit.
/// </summary>
public sealed class HitLedger
{
    private readonly Dictionary<(int AbilityId, int TargetId, int Instance), List<LogDamage>> _byKey = new();
    private readonly Dictionary<int, List<LogDamage>> _byAbility = new();
    private readonly List<LogDamage> _all;
    private readonly HashSet<LogDamage> _matched = new();
    private readonly HashSet<LogDamage> _reported = new();
    private int _settledCursor;

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

    public LogDamage? Match(int logId, UnitRef target, double engineTime, ReplayOptions options)
    {
        // A hit that chose its target from a log hit takes that hit.
        if (_reserved.TryGetValue((logId, target), out var queue))
        {
            while (queue.Count > 0)
            {
                var reserved = queue.Dequeue();
                if (_matched.Add(reserved)) return reserved;
            }
        }
        if (!_byKey.TryGetValue((logId, target.Id, target.Instance), out var list)) return null;
        foreach (var h in list)
        {
            if (h.Time < engineTime - options.HitEarlySeconds) continue;
            if (h.Time > engineTime + options.HitLateSeconds) break;
            if (_matched.Contains(h)) continue;
            _matched.Add(h);
            return h;
        }
        return null;
    }

    private readonly HashSet<LogDamage> _claimedGroups = new();
    private readonly HashSet<LogDamage> _claimedGroupHits = new();

    /// <summary>True when an engine hit has matched this log hit or claimed its group for targeting.</summary>
    public bool IsAccountedFor(LogDamage hit) => _matched.Contains(hit) || _claimedGroupHits.Contains(hit);
    private readonly HashSet<LogDamage> _targetChoices = new();
    private readonly Dictionary<(int LogId, UnitRef Unit), Queue<LogDamage>> _reserved = new();

    private void Reserve(LogDamage h)
    {
        _targetChoices.Add(h);
        var key = (h.AbilityId, h.Target);
        if (!_reserved.TryGetValue(key, out var queue)) _reserved[key] = queue = new Queue<LogDamage>();
        queue.Enqueue(h);
    }

    /// <summary>True when the log has a hit with <paramref name="logId"/> on <paramref name="unit"/> in the travel window that is still free.</summary>
    public bool HasUnclaimed(int logId, UnitRef unit, double engineTime, ReplayOptions options)
    {
        if (!_byKey.TryGetValue((logId, unit.Id, unit.Instance), out var list)) return false;
        foreach (var h in list)
        {
            if (h.Time < engineTime - options.HitEarlySeconds) continue;
            if (h.Time > engineTime + options.HitLateSeconds) break;
            if (!_matched.Contains(h) && !_targetChoices.Contains(h)) return true;
        }
        return false;
    }

    /// <summary>The target of the earliest unmatched log hit with <paramref name="logId"/> in the travel window not already chosen.</summary>
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
    /// when clearly closer, as hits that can only go there share the ID.
    /// </summary>
    public UnitRef? NextUnclaimedTarget(int logId, double engineTime, ReplayOptions options, UnitRef? avoid = null)
    {
        if (!_byAbility.TryGetValue(logId, out var list)) return null;
        double expected = engineTime + TypicalTravel(logId, options);
        LogDamage? best = null;
        double bestScore = double.MaxValue;
        foreach (var h in list)
        {
            if (h.Time < engineTime - options.HitEarlySeconds) continue;
            if (h.Time > engineTime + options.HitLateSeconds) break;
            if (_matched.Contains(h) || _targetChoices.Contains(h)) continue;
            double score = Math.Abs(h.Time - expected) + (avoid != null && h.Target == avoid ? options.AvoidedTargetPenaltySeconds : 0.0);
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
                if (current == null || h.Time - current[0].Time > options.HitGroupSeconds || current.Any(x => x.Target == h.Target))
                {
                    // A repeated target opens a new group only if no later open group can take it.
                    current = groups.Count > 0 && h.Time - groups[^1][0].Time <= options.HitGroupSeconds
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
            if (first.Time > engineTime + options.HitLateSeconds) break;
            if (_claimedGroups.Contains(first)) continue;
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
            var prev = list.LastOrDefault(p => p.Time < group[0].Time - options.HitGroupSeconds && p.Time > h.Time - 3.0
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

    /// <summary>Log hits that landed before <paramref name="settledBefore"/> without any engine hit matching them (each returned once).</summary>
    public IEnumerable<LogDamage> UnmatchedBefore(double settledBefore)
    {
        while (_settledCursor < _all.Count && _all[_settledCursor].Time < settledBefore)
        {
            var h = _all[_settledCursor++];
            if (!_matched.Contains(h) && _reported.Add(h)) yield return h;
        }
    }

    public int TotalHits => _all.Count;
    public int MatchedHits => _matched.Count;
}

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
    /// <summary>Diagnostics: the situation the engine hit was dealt in.</summary>
    public HitContext? Context { get; init; }

    /// <summary>Below 1 for a partial final tick of a periodic effect.</summary>
    public double TickFraction { get; init; } = 1.0;

    /// <summary>When the engine's hit landed (dealt its damage); null if it never landed.</summary>
    public double? LandedTime { get; init; }

    /// <summary>Log over engine, with the log hit before its block (the engine has no blocks).</summary>
    public double? Ratio => Log == null || EngineAmount <= 0 ? null : Log.UnblockedAmount / EngineAmount;

    public bool WithinSpread(ReplayOptions options)
    {
        if (Log == null) return false;
        double low = EngineAmount * (1.0 - options.DamageSpread) - options.DamageAbsoluteTolerance;
        double high = EngineAmount * (1.0 + options.DamageSpread) * (1.0 + options.UnexplainedHighMargin) + options.DamageAbsoluteTolerance;
        if (TickFraction < 1.0 && TickFraction > 0.0)
        {
            // The fraction of a partial tick is only known to about one server frame.
            double fullTick = EngineAmount / TickFraction;
            low -= fullTick * options.PartialTickFractionTolerance;
            high += fullTick * options.PartialTickFractionTolerance;
        }
        // Overkill truncates the logged amount, so only an engine hit that is too small is detectable then.
        if (Log.Overkill > 0 && Log.TotalAmount < EngineAmount) return true;
        return Log.UnblockedAmount >= low && Log.UnblockedAmount <= high;
    }
}

/// <summary>The situation of an engine hit: active enemies and the hero's active auras (log IDs, and names of internal ones).</summary>
public sealed record HitContext(int ActiveEnemies, int[] AuraLogIds, string[] InternalAuras);

public sealed class ProcRollStats
{
    public ProcRollStats(string key) => Key = key;
    public string Key { get; }
    public int Rolls { get; set; }
    public double ExpectedProcs { get; set; }
    public int Procs { get; set; }
}
