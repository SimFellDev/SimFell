using FellowshipSim.Core;
using FellowshipSim.Core.Dungeons.Affixes;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Replays a combat log through the engine using only engine commands (cast, cancel, stop channel,
/// enemy arise/demise, external auras). In an exact replay every outcome and every damage amount comes from the log
/// (<see cref="LogOutcomeSource"/>), and <see cref="ReplayVerifier"/> compares each logged value with the engine's range
/// for it: hits, damage, casts, cooldowns, periodic ticks, and the resources and tracked auras before each action.
/// With <see cref="ReplayOptions.StopOnFirstFailure"/> the replay halts at the first step with an issue.
/// </summary>
public sealed class ReplayDriver
{
    private readonly Hero _hero;
    private readonly ReplayScript _script;
    private readonly ReplayOptions _options;
    private readonly SimulationContext _ctx;
    private readonly HitLedger _ledger;
    private readonly LogOutcomeSource _oracle;
    private readonly ReplayVerifier _verifier;
    private readonly Dictionary<int, string> _externalAurasOnPlayer = new();
    private readonly HashSet<ReplayCommand> _handledCommands = new();
    private (Ability Ability, double Time)? _lastCompletedCast;

    public ReplayDriver(Hero hero, ReplayScript script, ReplayOptions? options = null)
    {
        _hero = hero;
        _script = script;
        _options = options ?? new ReplayOptions();
        _ctx = new SimulationContext(hero)
        {
            GlobalCooldownToleranceSeconds = _options.GlobalCooldownToleranceSeconds,
            AbilityQueueWindowSeconds = _options.SameTickSeconds,
        };
        // Free dice matches no log hits.
        _ledger = new HitLedger(_options.FreeDiceSeed.HasValue ? [] : script.Observations.Damage);
        // The log's aura states, for the state checks (free dice makes none).
        _logAuras = FreeDice ? null : new LogAuraTimeline(script.Observations.Auras.Where(a => a.Target.Id == script.Player.Id), ModeledAuras.LogIds(hero),
            logId => hero.Auras.Active(_ctx.CurrentTime).FirstOrDefault(a => a.LogId == logId)?.Stacks ?? 0);
        _verifier = new ReplayVerifier(script, _options, hero, _logAuras);
        _oracle = new LogOutcomeSource(script, _options, _ledger, _verifier);
        _ctx.Outcomes = _options.FreeDiceSeed is { } seed
            ? new FreeDiceOutcomeSource(script, new SimulationRng(seed))
            : _oracle;
        _hero.OnCastCompleted += (ability, _, now) => _lastCompletedCast = (ability, now.TotalSeconds);
        // Logged pulses fire at their log hits (FireDefensiveProcs), so the hero's own chains for them stay off.
        _hero.PulsesFollowLog = true;
    }

    public SimulationContext Context => _ctx;

    private bool FreeDice => _options.FreeDiceSeed.HasValue;

    /// <summary>Issues are recorded unless a free-dice run that never halts would discard them.</summary>
    private bool KeepsIssues => !FreeDice || _options.StopOnFirstFailure;

    /// <summary>Heroes the engine can replay (every hero class in Heroes/).</summary>
    public static IReadOnlyList<string> SupportedHeroes => Catalog.Heroes.Keys.ToList();

    public static bool SupportsHero(string hero) => Catalog.HasHero(hero);

    /// <summary>Builds the logged character from the script.</summary>
    public static Hero CreateHero(ReplayScript script)
    {
        var build = script.Character ?? throw new InvalidOperationException("Replay script has no character.");
        var hero = HeroImporter.Import(build);
        ClassTuningHistory.Apply(hero, script.FightDate);
        foreach (int id in script.DungeonModifierIds) DungeonAffix.ById(id)?.ApplyToHero(hero);
        return hero;
    }

    /// <summary>
    /// Replays the script. <paramref name="cancellationToken"/> stops the replay between player actions with an
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    public ReplayResult Run(CancellationToken cancellationToken = default)
    {
        _hero.ResetForIteration();
        StartFromLoggedResources();
        _hero.BeginFight(_ctx);
        FollowSituationalAuras();
        FireDefensiveProcs();
        CarryOverAuras();

        // An enemy's arrival is only known from its first event in the log, so it was there a little earlier.
        // A channel's stop is logged up to a few milliseconds into its server frame, so it comes before the frame's tick.
        double When(ReplayCommand c) => c.Kind switch
        {
            CommandKind.EnemyArise => c.Time - _options.EnemyAriseLeadSeconds,
            CommandKind.StopChannel => c.Time - _options.ChannelStopLeadSeconds,
            _ => c.Time,
        };
        var order = OrderOf(_script, (_options.EnemyAriseLeadSeconds, _options.ChannelStopLeadSeconds), When);
        var commands = order.Commands;
        var result = new ReplayResult
        {
            ReportCode = _script.ReportCode,
            FightId = _script.FightId,
            FightName = _script.FightName,
            PlayerName = _script.Player.Name,
            Hero = _script.Player.Hero,
            TotalSteps = order.PlayerActions,
            DurationSeconds = _script.DurationSeconds,
            Options = _options,
        };
        // Free dice reads the log's damage from the script.
        if (!FreeDice) result.LogDamage.AddRange(_script.Observations.Damage);
        foreach (var (name, count) in order.LogCasts) result.LogCasts[name] = count;
        _hero.OnCastCompleted += (ability, _, _) => result.EngineCasts[ability.Name] = result.EngineCasts.GetValueOrDefault(ability.Name) + 1;

        var cancelFor = order.CastEnds;
        ReplayStep? step = null;
        if (!FreeDice) DealUnmatchedLogHits(result);

        foreach (var cmd in commands)
        {
            if (_handledCommands.Contains(cmd)) continue;
            var time = SimTime.FromSeconds(When(cmd));
            _ctx.RunUntil(time);

            if (cmd.IsPlayerAction)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Halt(result)) break;
                if (_options.MaxSteps.HasValue && result.Steps.Count >= _options.MaxSteps.Value) break;

                step = BeginStep(cmd, result.Steps.Count + 1);
                result.Steps.Add(step);
                _verifier.CurrentStep = step;
                if (!FreeDice)
                {
                    _hero.RegenerateResources(_ctx.CurrentTime);
                    _verifier.CheckState(_ctx, step, cmd);
                    SyncToLog(step, cmd);
                }
            }

            Execute(cmd, step, cancelFor, result);

            if (KeepsIssues && _verifier.PendingIssues.Any(i => i.Category is IssueCategory.Casts or IssueCategory.GlobalCooldown or IssueCategory.Cooldown)
                && Halt(result)) break;
        }

        if (result.HaltedAt == null)
        {
            _ctx.RunUntil(SimTime.FromSeconds(_script.DurationSeconds + _options.SettleSeconds));
            Halt(result);
        }

        result.EngineTotalDamage = FreeDice ? _ctx.TotalDamage : _verifier.AllHits.Sum(h => h.EngineAmount);
        result.DamageDealt = _ctx.TotalDamage;
        result.MissedHits = _oracle.MissedHits;
        result.LogHits = _ledger.TotalHits;
        result.MatchedLogHits = _ledger.MatchedHits;
        result.ProcStats.AddRange(_oracle.ProcStats.Values.OrderBy(p => p.Key));
        result.AllHits.AddRange(_verifier.AllHits);
        result.Issues.AddRange(_verifier.Issues);
        result.UnmodeledCustomizations.AddRange(_hero.UnmodeledFeatures);
        foreach (var s in result.Steps)
        {
            foreach (var a in s.UntrackedLogAuras) result.UntrackedLogAuras[a] = result.UntrackedLogAuras.GetValueOrDefault(a) + 1;
        }
        return result;
    }

    /// <summary>
    /// The script's commands in replay order, the command that ends each begin_cast, the number of player actions and the
    /// log's casts by ability. Runs only read it.
    /// </summary>
    private sealed record CommandOrder((double Arise, double ChannelStop) Leads, List<ReplayCommand> Commands, Dictionary<ReplayCommand, ReplayCommand> CastEnds,
        int PlayerActions, Dictionary<string, int> LogCasts);

    // Built once per script: free dice replays one script many times, on several threads.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ReplayScript, CommandOrder> _orders = new();

    private static CommandOrder OrderOf(ReplayScript script, (double Arise, double ChannelStop) leads, Func<ReplayCommand, double> when)
    {
        if (_orders.TryGetValue(script, out var cached) && cached.Leads == leads) return cached;
        var commands = script.Commands.OrderBy(when).ToList();
        var logCasts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in commands.Where(c => c.Kind is CommandKind.Cast or CommandKind.CompleteCast))
        {
            logCasts[c.AbilityName] = logCasts.GetValueOrDefault(c.AbilityName) + 1;
        }
        var order = new CommandOrder(leads, commands, MatchCastEnds(commands), commands.Count(c => c.IsPlayerAction), logCasts);
        _orders.AddOrUpdate(script, order);
        return order;
    }

    // ------------------------------------------------------------------ commands

    private void Execute(ReplayCommand cmd, ReplayStep? step, Dictionary<ReplayCommand, ReplayCommand> cancelFor, ReplayResult result)
    {
        switch (cmd.Kind)
        {
            case CommandKind.EnemyArise:
                _ctx.AddEnemy(cmd.Enemy!.Id, cmd.Enemy.Instance, cmd.Name ?? "Enemy", cmd.MaxHp);
                break;

            case CommandKind.EnemyHealth:
                var hurt = _ctx.FindEnemy(cmd.Enemy!.Id, cmd.Enemy.Instance);
                if (hurt != null && cmd.MaxHp > 0)
                {
                    hurt.MaxHealth = cmd.MaxHp;
                    hurt.CurrentHealth = cmd.Hp;
                    // Two of the log's readings show a heal. An engine hit in between lowers the health with no heal after it.
                    if (_loggedEnemyHealth.TryGetValue(hurt, out double loggedBefore) && hurt.HealthFraction > loggedBefore) hurt.HealedTo(hurt.HealthFraction);
                    _loggedEnemyHealth[hurt] = hurt.HealthFraction;
                    _hero.EarnSpiritPoints(hurt, hurt.HealthFraction, _ctx);
                }
                break;

            case CommandKind.EnemyDemise:
                var enemy = _ctx.FindEnemy(cmd.Enemy!.Id, cmd.Enemy.Instance);
                if (enemy != null) _ctx.RemoveEnemy(enemy, defeated: cmd.Reason == "death", died: cmd.Reason == "death");
                break;

            case CommandKind.ExternalAura:
                ApplyExternalAura(cmd, result);
                break;

            case CommandKind.PlayerDeath:
                Die();
                break;

            case CommandKind.PlayerRevive:
                Revive(cmd.Time);
                break;

            case CommandKind.Cast:
            case CommandKind.BeginCast:
                if (QueueFlushingTimer(cmd) is { } timer) timer.RunInLastTick(() => StartCast(cmd, step!, cancelFor, result));
                else StartCast(cmd, step!, cancelFor, result);
                break;

            case CommandKind.CompleteCast:
                CompleteCast(cmd);
                break;

            case CommandKind.CancelCast:
                if (_hero.CurrentCast?.Ability.IsAbility(cmd.AbilityId) == true) _ctx.CancelCast(_hero);
                break;

            case CommandKind.StopChannel:
                if (FindAbility(cmd) == null) break;
                if (_hero.CurrentChannel?.Ability.IsAbility(cmd.AbilityId) == true)
                {
                    _ctx.StopChannel(_hero);
                }
                else
                {
                    if (KeepsIssues) _verifier.Report(_ctx, IssueCategory.Casts, IssueEntity.Ability(cmd.AbilityName), $"Log stopped channeling {cmd.AbilityName}, but the engine was not channeling it (engine channel already ended or never started)", $"stop channel while not channeling: {cmd.AbilityName}");
                }
                break;
        }
    }

    /// <summary>
    /// Each log hit no engine hit has matched by <see cref="ReplayOptions.HitEarlySeconds"/> after it landed (the end of
    /// the window in which a later engine release can match it) is dealt by the replay, with the log's damage ID, target,
    /// amount and crit.
    /// </summary>
    private void DealUnmatchedLogHits(ReplayResult result)
    {
        foreach (var log in _ledger.All)
        {
            _ctx.ScheduleAt(SimTime.FromSeconds(log.Time + _options.HitEarlySeconds), c =>
            {
                if (_ledger.IsMatched(log)) return;
                _ledger.MarkDealt(log);
                var enemy = c.FindEnemy(log.Target.Id, log.Target.Instance);
                if (enemy == null)
                {
                    // An enemy the log has no arrival for: it joins asleep, so it is never one of the enemies in combat.
                    enemy = c.GetOrSpawnEnemy(log.Target.Id, log.Target.Instance);
                    enemy.Demise(c);
                }
                _verifier.LogHitWithoutEngineHit(c, log, enemy);
                result.LoggedHitsDealt++;
                c.DealLoggedHit(_hero, enemy, log.AbilityId, log.AbilityName, log.DealtAmount, log.Crit);
            }, log.AbilityName);
        }
    }

    private void StartCast(ReplayCommand cmd, ReplayStep step, Dictionary<ReplayCommand, ReplayCommand> cancelFor, ReplayResult result)
    {
        var ability = FindAbility(cmd);
        if (ability == null)
        {
            step.Modeled = false;
            step.Note = "No engine model for this ability; skipped";
            result.UnmodeledAbilities[cmd.AbilityName] = result.UnmodeledAbilities.GetValueOrDefault(cmd.AbilityName) + 1;
            return;
        }

        var target = ResolveTarget(cmd.Target);
        if (target != null) _hero.CurrentTarget = target;
        else if (_hero.CurrentTarget is null or { IsSleeping: true }) _hero.CurrentTarget = _ctx.ActiveEnemies.FirstOrDefault()!;
        // A cast the log records with no target (target -1) acts on the current target, as every cast in a sim does.
        if (target == null && cmd.Target is null or { Id: <= 0 }) target = _hero.CurrentTarget;

        cancelFor.TryGetValue(cmd, out var end);

        // A cast still in progress in the engine when the player acts again finished in the game.
        if (_hero.CurrentCast != null && !ability.IgnoresGcd)
        {
            var still = KeepsIssues ? _verifier.StillCasting(_ctx, ability) : null;
            if (still != null) _verifier.Report(still);
            if (!_options.ForceCasts) return;
            Force(step, result, cmd, $"Engine is still casting {_hero.CurrentCast.Ability.Name} (ends {_hero.CurrentCast.EndTime}) when the log casts {cmd.AbilityName}");
            _ctx.CompleteCast(_hero);
        }

        if ((KeepsIssues ? CheckedCast(cmd, ability, target, end, step, result) : UncheckedCast(cmd, ability, target, end, step, result))
            is not { } attempt) return;
        if (!attempt.Succeeded)
        {
            if (KeepsIssues) _verifier.Report(_ctx, IssueCategory.Casts, IssueEntity.Ability(cmd.AbilityName), $"Engine refused {cmd.AbilityName}: {attempt.Reason}", $"refused: {cmd.AbilityName}");
            return;
        }

        double engineCast = attempt.CastTime.TotalSeconds;
        // The logged duration: to the complete_cast, or to the cancel_cast of a cast the log never completed.
        double? logCast = end == null ? null : end.Time - cmd.Time;
        if (cmd.Kind == CommandKind.BeginCast && attempt.Status == CastStatus.Resolved)
        {
            if (KeepsIssues) _verifier.CheckInstantOrCast(_ctx, ability, 0.0, logCast, cmd.Kind);
        }
        else if (cmd.Kind == CommandKind.Cast && attempt.Status == CastStatus.Casting)
        {
            if (KeepsIssues) _verifier.CheckInstantOrCast(_ctx, ability, engineCast, 0.0, cmd.Kind);
            _ctx.CompleteCast(_hero);
        }

        if (attempt.Status != CastStatus.Casting || end == null) return;
        var cast = _hero.CurrentCast!;
        if (end.Kind == CommandKind.CancelCast)
        {
            // The cast is cancelled at the log's cancel_cast time, and never completes on its own.
            _handledCommands.Add(end);
            _ctx.SetCastCompletion(_hero, SimTime.Infinite);
            _ctx.ScheduleAt(SimTime.FromSeconds(end.Time), c => { if (_hero.CurrentCast == cast) c.CancelCast(_hero); }, cmd.AbilityName);
            return;
        }

        // A cast the log completes without the debuff it applies ended at its completion with nothing done.
        if (_options.CastProofDebuffIds.TryGetValue(cmd.AbilityId, out int proof) && !AppliedDebuff(proof, end.Time))
        {
            _handledCommands.Add(end);
            var at = SimTime.FromSeconds(end.Time);
            if (at >= cast.EndTime) at = SimTime.FromMilliseconds(cast.EndTime.Milliseconds - 1);
            _ctx.ScheduleAt(at, c => { if (_hero.CurrentCast == cast) c.CancelCast(_hero); }, cmd.AbilityName);
            step.Note = (step.Note == null ? "" : step.Note + "; ") + $"The log completes {cmd.AbilityName} with no debuff: cancelled at its completion";
            return;
        }

        // The cast completes at the log's complete_cast time (player latency).
        if (KeepsIssues) _verifier.CheckCastTime(_ctx, ability, engineCast, logCast!.Value);
        _ctx.SetCastCompletion(_hero, SimTime.FromSeconds(end.Time));
    }

    /// <summary>
    /// Casts <paramref name="ability"/> after the verifier reports each reason the engine refuses it, forced when there is
    /// one. Null when the cast is not made.
    /// </summary>
    private CastAttempt? CheckedCast(ReplayCommand cmd, Ability ability, EnemyTarget? target, ReplayCommand? end, ReplayStep step, ReplayResult result)
    {
        var refusals = _verifier.CastRefusals(_ctx, ability);
        // A begin_cast the log never completes, refused because the ability is not ready, is a press that did nothing
        // (e.g. pressing Chronoshift again as its channel ends).
        if (refusals.Count > 0 && cmd.Kind == CommandKind.BeginCast && end?.Kind == CommandKind.CancelCast
            && refusals.All(r => r.Category is IssueCategory.GlobalCooldown or IssueCategory.Cooldown))
        {
            _handledCommands.Add(end);
            step.Note = $"Press while not ready ({string.Join("; ", refusals.Select(r => r.Message))}); the log never completed it";
            return null;
        }
        if (refusals.Count == 0) return _ctx.BeginCast(_hero, ability, target);
        foreach (var refusal in refusals) _verifier.Report(refusal);
        if (!_options.ForceCasts) return null;
        Force(step, result, cmd, $"Engine would refuse {cmd.AbilityName}: {string.Join("; ", refusals.Select(r => r.Message))}");
        return _ctx.BeginCast(_hero, ability, target, force: true);
    }

    /// <summary>Casts <paramref name="ability"/> with no checks (free dice), forced when the engine refuses it. Null when the cast is not made.</summary>
    private CastAttempt? UncheckedCast(ReplayCommand cmd, Ability ability, EnemyTarget? target, ReplayCommand? end, ReplayStep step, ReplayResult result)
    {
        var attempt = _ctx.BeginCast(_hero, ability, target);
        if (attempt.Succeeded) return attempt;
        if (cmd.Kind == CommandKind.BeginCast && end?.Kind == CommandKind.CancelCast && attempt.Reason?.Contains("cooldown") == true)
        {
            _handledCommands.Add(end);
            step.Note = $"Press while not ready ({attempt.Reason}); the log never completed it";
            return null;
        }
        if (!_options.ForceCasts) return null;
        Force(step, result, cmd, $"Engine would refuse {cmd.AbilityName}: {attempt.Reason}");
        return _ctx.BeginCast(_hero, ability, target, force: true);
    }

    /// <summary>
    /// The period timer of the damage over time whose tick the log has in this cast's server frame, before the cast, when
    /// that tick ended an ability of the hero (<see cref="Hero.QueueFlushingTick"/>): the server's ability queue, flushed
    /// inside the tick, activated the cast there (game data: the queue is flushed when the ability ends, inside Bursting
    /// Ice's period callback; the cast is logged after the pulse).
    /// </summary>
    private HastedTimer? QueueFlushingTimer(ReplayCommand cmd)
    {
        if (_hero.QueueFlushingTick is not { } tick || !tick.Timer.IsActive) return null;
        double since = cmd.Time - tick.Timer.LastTick.TotalSeconds;
        if (since < 0 || since > PeriodicEffect.ServerFrameSeconds + _options.SameTickSeconds) return null;
        var times = LogHitTimes(tick.DamageLogId);
        int i = Array.BinarySearch(times, cmd.Time);
        if (i < 0) i = ~i - 1;
        return i >= 0 && cmd.Time - times[i] <= _options.SameTickSeconds ? tick.Timer : null;
    }

    private readonly Dictionary<int, double[]> _logHitTimes = new();

    /// <summary>The times of the log's hits of one damage ID, in order.</summary>
    private double[] LogHitTimes(int damageLogId)
    {
        if (_logHitTimes.TryGetValue(damageLogId, out var times)) return times;
        times = _script.Observations.Damage.Where(d => d.AbilityId == damageLogId).Select(d => d.Time).Order().ToArray();
        _logHitTimes[damageLogId] = times;
        return times;
    }

    /// <summary>True when the log has the player applying or refreshing debuff <paramref name="logId"/> within a server frame of <paramref name="time"/>.</summary>
    private bool AppliedDebuff(int logId, double time) => _script.Observations.Auras.Any(a => a.AbilityId == logId
        && a.Source.Id == _script.Player.Id && a.Type is "applydebuff" or "refreshdebuff"
        && a.Time >= time - _options.SameTickSeconds && a.Time <= time + PeriodicEffect.ServerFrameSeconds);

    /// <summary>Records a cast the engine would not have allowed but makes anyway to follow the log.</summary>
    private static void Force(ReplayStep? step, ReplayResult result, ReplayCommand cmd, string why)
    {
        result.ForcedCasts.Add($"{cmd.Time:F3}s {why}");
        if (step != null) step.Note = (step.Note == null ? "" : step.Note + "; ") + "FORCED: " + why;
    }

    private void CompleteCast(ReplayCommand cmd)
    {
        if (FindAbility(cmd) == null) return;

        var current = _hero.CurrentCast;
        if (current != null && current.Ability.IsAbility(cmd.AbilityId))
        {
            _ctx.CompleteCast(_hero);
            return;
        }

        if (_lastCompletedCast is { } done && done.Ability.IsAbility(cmd.AbilityId) && cmd.Time - done.Time < 3.0)
        {
            return; // completed by the engine at the log's completion time
        }

        if (KeepsIssues) _verifier.Report(_ctx, IssueCategory.Casts, IssueEntity.Ability(cmd.AbilityName), $"Log completed {cmd.AbilityName}, but the engine was not casting it", $"completed but not casting: {cmd.AbilityName}");
    }

    /// <summary>
    /// Auras other actors put on the player or on enemies. Their effect comes from the <see cref="PartyBuffs"/> catalogue;
    /// the log decides when they end. Auras the catalogue does not list are reported.
    /// </summary>
    private void ApplyExternalAura(ReplayCommand cmd, ReplayResult result)
    {
        bool onPlayer = cmd.Target?.Id == _script.Player.Id;
        Actor? actor = onPlayer ? _hero : cmd.Target == null ? null : _ctx.FindEnemy(cmd.Target.Id, cmd.Target.Instance);
        if (actor == null) return;

        bool removed = cmd.AuraEventType is "removebuff" or "removedebuff";
        if (onPlayer && AnomalousResidue.ColourByLogId.TryGetValue(cmd.AbilityId, out var colour))
        {
            ChangeResidue(colour, removed ? 0 : cmd.Stack ?? (cmd.AuraEventType is "refreshbuff" ? null : 1));
            return;
        }

        var def = ExternalAuraDef(cmd.AbilityId, cmd.Source?.Id ?? 0);
        if (removed)
        {
            if (def != null) actor.Auras.Remove(def, _ctx.CurrentTime);
            if (onPlayer) _externalAurasOnPlayer.Remove(cmd.AbilityId);
            return;
        }

        if (def != null) actor.Auras.Apply(def, _ctx.CurrentTime, setStacks: cmd.Stack ?? 1);
        if (!onPlayer) return;
        _externalAurasOnPlayer[cmd.AbilityId] = cmd.AbilityName;
        if (def == null || def.Modifiers.Count == 0)
        {
            result.UnmodeledExternalAuras[cmd.AbilityName] = result.UnmodeledExternalAuras.GetValueOrDefault(cmd.AbilityName) + 1;
        }
    }

    /// <summary>
    /// Anomalous Residue on the player: the log's stacks set the colour's gem power
    /// (<see cref="Core.Dungeons.Affixes.AnomalousOrbs.SetResidue"/>). A refresh keeps the stacks.
    /// </summary>
    private void ChangeResidue(Core.Customization.Gems.GemColor colour, int? stacks)
    {
        if (stacks is not { } count) return;
        Core.Dungeons.Affixes.AnomalousOrbs.SetResidue(_hero, colour, count, _ctx.CurrentTime);
    }

    private readonly Dictionary<(int LogId, int Source), AuraDef?> _externalDefs = new();

    /// <summary>
    /// The aura another actor applies, from the <see cref="PartyBuffs"/> catalogue: one instance per source (the same buff
    /// from two players stacks), lasting until the log removes it, and scaled to the source player's rank when it comes
    /// from one of their sigils. Null for buffs the catalogue does not list.
    /// </summary>
    private AuraDef? ExternalAuraDef(int logId, int sourceId)
    {
        if (_externalDefs.TryGetValue((logId, sourceId), out var cached)) return cached;
        AuraDef? def = null;
        if (PartyBuffs.Find(logId) is { } buff)
        {
            def = buff.Aura with { Duration = SimTime.Infinite, MaxStacks = 99, SourceId = sourceId };
            if (buff.ValueByGiverRank is { } byRank && _script.Players.TryGetValue(sourceId.ToString(), out var source))
            {
                int rank = source.GearTraits.GetValueOrDefault(buff.SigilName!)
                           + source.WeaponTraits.GetValueOrDefault((LogIds.AugmentationOffset + buff.SigilId).ToString());
                double value = byRank[Math.Clamp(rank, 1, byRank.Count) - 1];
                def = def with { Modifiers = def.Modifiers.Select(m => m with { Value = value }).ToArray() };
            }
        }
        _externalDefs[(logId, sourceId)] = def;
        return def;
    }

    /// <summary>
    /// Resources that carry over from earlier pulls of the dungeon (Spirit Points) start at the log's first reading.
    /// </summary>
    private void StartFromLoggedResources()
    {
        foreach (var logged in LogResources.All.Where(l => l.StartsFromLog && _hero.ResourceDefs.Contains(l.Resource)))
        {
            SetFromFirstReading(logged, 0.0);
        }
    }

    /// <summary>
    /// Sets the resource to the log's first reading at or after <paramref name="from"/>, at the moment of the reading, so
    /// what the engine gains before it (regeneration) does not count twice.
    /// </summary>
    private void SetFromFirstReading(LogResources.LogResource logged, double from)
    {
        var first = _script.Observations.Resources
            .Where(r => r.Time >= from && r.Resources.ContainsKey(logged.LogResourceType.ToString()))
            .OrderBy(r => r.Time).FirstOrDefault();
        if (first == null) return;
        double value = logged.EngineAmount(first.Resources[logged.LogResourceType.ToString()]);
        var resource = logged.Resource;
        _ctx.ScheduleAt(SimTime.FromSeconds(Math.Max(from, first.Time - 0.001)), c =>
        {
            _hero.RegenerateResources(c.CurrentTime);
            var state = _hero.Resources.Get(resource);
            state.Current = Math.Min(state.Max, value);
        }, $"{resource.Name} from the log");
    }

    /// <summary>
    /// The player dies: the cast or channel ends and the hero loses every aura but its passive and situational ones and
    /// those that keep on death (<see cref="AuraDef.KeepsOnDeath"/>). Damage over time on enemies goes on, but for an effect
    /// that ends on its source's death (<see cref="Hero.OnDied"/>).
    /// </summary>
    private void Die()
    {
        if (_hero.CurrentCast != null) _ctx.CancelCast(_hero);
        if (_hero.CurrentChannel != null) _ctx.StopChannel(_hero);
        _hero.TriggerDied(_ctx);
        var now = _ctx.CurrentTime;
        foreach (var aura in _hero.Auras.Active(now).ToList())
        {
            if (aura.LogId <= 0 || aura.Def.KeepsOnDeath || _hero.PassiveAuras.Contains(aura.Def) || _hero.SituationalAuras.Contains(aura.Def)) continue;
            _hero.Auras.Remove(aura.Def, now);
        }
    }

    /// <summary>
    /// The player is back: every resource the log reports takes the log's first reading after it (the log shows a share of
    /// the Spirit Points at death, and other resources refilled or emptied).
    /// </summary>
    private void Revive(double time)
    {
        foreach (var logged in LogResources.All.Where(l => _hero.ResourceDefs.Contains(l.Resource)))
        {
            SetFromFirstReading(logged, time);
        }
    }

    /// <summary>
    /// Defensive procs (<see cref="Hero.DefensiveProcs"/>) fire in a replay only, at each proof of them in the log
    /// (<see cref="ProcEvidence.Times"/>: every kind of proof a proc roll reads), less the evidence's
    /// <see cref="ProcEvidence.Evidence.MinDelaySeconds"/>. The roll there claims the proof (<see cref="LogOutcomeSource"/>),
    /// so a hit an engine hit already matched fires nothing.
    /// </summary>
    private void FireDefensiveProcs()
    {
        foreach (var proc in _hero.DefensiveProcs)
        {
            if (ProcEvidence.For(proc.ProcKey) is not { } evidence)
            {
                _verifier.Report(_ctx, IssueCategory.Procs, IssueEntity.Proc(proc.ProcKey), $"Defensive proc '{proc.ProcKey}' has no evidence listed in ProcEvidence; it never fires",
                    $"proc without evidence: {proc.ProcKey}");
                continue;
            }
            foreach (var (time, target) in ProcEvidence.Times(_script, evidence, _hero))
            {
                _ctx.Schedule(SimTime.FromSeconds(Math.Max(0.0, time - evidence.MinDelaySeconds)), c =>
                {
                    var enemy = target == null ? _hero.CurrentTarget : c.FindEnemy(target.Id, target.Instance) ?? _hero.CurrentTarget;
                    if (c.Outcomes.RollProc(proc.ProcKey, 1.0, c, enemy)) proc.Fire(c, enemy);
                }, proc.ProcKey);
            }
        }
    }

    /// <summary>
    /// Situational auras (<see cref="Hero.SituationalAuras"/>) depend on what the engine does not simulate, so the replay
    /// gives the hero each one exactly when the log does. One whose first logged event is a loss was up as the fight began.
    /// One on the Main Stat with no logged event was up the whole fight (Might of the Minotaur: it drops only below a health
    /// threshold, and the log's starting Main Stat includes it). A gem's
    /// situational nodes are followed at both ranks.
    /// </summary>
    private void FollowSituationalAuras()
    {
        // A gem's situational nodes at the rank the hero was not built with too: a gem power change in the fight
        // (Anomalous Residue) gives the other rank, which the log shows.
        var otherRanks = Core.Customization.Gems.Ruby.SituationalNodeAuras.Where(a => !_hero.SituationalAuras.Contains(a));
        foreach (var aura in _hero.SituationalAuras.Concat(otherRanks))
        {
            var events = _script.Observations.Auras
                .Where(a => a.AbilityId == aura.LogId && a.Target.Id == _script.Player.Id)
                .OrderBy(a => a.Time).ToList();
            _hero.Auras.Remove(aura, SimTime.Zero);
            bool bound = _hero.SituationalAuras.Contains(aura);
            if (events.FirstOrDefault() is { IsLoss: true }
                || (bound && events.Count == 0 && aura.Changes(AuraDef.MaskOf(StatType.PrimaryAttributeMultiplier))))
            {
                _hero.Auras.Apply(aura, SimTime.Zero);
            }
            foreach (var e in events)
            {
                _ctx.Schedule(SimTime.FromSeconds(Math.Max(0.0, e.Time)), c =>
                {
                    if (e.Type is "removebuff" or "removedebuff") c.Player.Auras.Remove(aura, c.CurrentTime);
                    else if (e.IsGain || e.Stack != null)
                    {
                        // A refresh logs no stacks: it keeps the ones it has (Gunde's Owed in Blood refreshes at its maximum).
                        int held = c.Player.Auras.GetStacks(aura, c.CurrentTime);
                        bool refresh = e.Type is "refreshbuff" or "refreshdebuff";
                        c.Player.Auras.Apply(aura, c.CurrentTime, setStacks: e.Stack ?? (refresh && held > 0 ? held : 1));
                    }
                }, $"{aura.Name} (log)");
            }
        }
    }

    /// <summary>
    /// Auras that carry over from earlier pulls: a modeled aura whose first event on the player in the log is its removal
    /// was on the hero as the fight began. The replay gives it to the hero from the start until the log removes it; its
    /// time left at the start is unknown, so nothing the engine does moves that end (combat logs: The Wayfarer's
    /// cooldown, effect 3183, runs out in N18KtgP3rvpYGm24 fight 27 and Rzg1nQNHpm2fhYtB fights 23 and 24 with no
    /// application before).
    /// </summary>
    private void CarryOverAuras()
    {
        var defs = ModeledAuras.Defs(_hero);
        foreach (var events in _script.Observations.Auras.Where(a => a.Target.Id == _script.Player.Id).GroupBy(a => a.AbilityId))
        {
            var first = events.OrderBy(a => a.Time).First();
            if (first.Type != "removebuff" || !defs.TryGetValue(first.AbilityId, out var def)) continue;
            if (_hero.SituationalAuras.Contains(def) || _hero.Auras.HasAura(def, SimTime.Zero)) continue;
            var carried = def with { Duration = SimTime.Infinite };
            _hero.Auras.Apply(carried, SimTime.Zero);
            _ctx.ScheduleAt(SimTime.FromSeconds(first.Time), c =>
            {
                if (_hero.Auras.Get(carried, c.CurrentTime) is { } aura && ReferenceEquals(aura.Def, carried)) _hero.Auras.Remove(carried, c.CurrentTime);
            }, $"{def.Name} carried over");
        }
    }

    // ------------------------------------------------------------------ checks

    private ReplayStep BeginStep(ReplayCommand cmd, int index) => new()
    {
        Index = index,
        Time = cmd.Time,
        Kind = cmd.Kind,
        AbilityId = cmd.AbilityId,
        AbilityName = cmd.AbilityName,
        Target = cmd.Target is { Id: > 0 } t ? t.ToString() : null,
        ActiveEnemies = _ctx.ActiveEnemies.Count(),
        // For the review page only (free dice has none).
        ExternalAurasOnPlayer = FreeDice ? [] : _externalAurasOnPlayer.Values.Distinct().OrderBy(n => n).ToList(),
    };

    /// <summary>Each enemy's health fraction at the log's last reading.</summary>
    private readonly Dictionary<EnemyTarget, double> _loggedEnemyHealth = new();

    private readonly LogAuraTimeline? _logAuras;
    private Dictionary<int, AuraDef>? _modeledAuras;
    private HashSet<int>? _sharedAuraIds;

    /// <summary>
    /// The engine takes the log's state wherever the pre-cast check found it different: each resource takes the log's
    /// reading, and each tracked aura the log's stacks (gained, changed or removed). The step's note lists each change.
    /// </summary>
    private void SyncToLog(ReplayStep step, ReplayCommand cmd)
    {
        var now = _ctx.CurrentTime;
        var changes = new List<string>();
        foreach (var check in step.Resources.Where(c => !c.Matches))
        {
            var resource = _hero.ResourceDefs.FirstOrDefault(r => r.Name == check.Resource);
            if (resource == null) continue;
            var state = _hero.Resources.Get(resource);
            state.Current = Math.Clamp(check.Log, 0.0, state.Max);
            changes.Add($"{check.Resource} {check.Engine:0.##} -> {state.Current:0.##}");
        }
        _modeledAuras ??= ModeledAuras.Defs(_hero);
        // The log's state of an aura ID it shows from several sources on the player merges them (another player's Inspired
        // Allegiance and the hero's own), so it says nothing certain about the hero's own aura.
        _sharedAuraIds ??= _script.Observations.Auras
            .Where(a => a.Target.Id == _script.Player.Id)
            .GroupBy(a => a.AbilityId)
            .Where(g => g.Select(a => a.Source.Id).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();
        foreach (var check in step.Auras.Where(c => !c.Matches))
        {
            if (_sharedAuraIds.Contains(check.LogId) || !_modeledAuras.TryGetValue(check.LogId, out var def)) continue;
            if (check.LogStacks == 0) _hero.Auras.Remove(def, now);
            else _hero.Auras.Apply(def, now, setStacks: check.LogStacks);
            changes.Add($"{check.Aura} {check.EngineStacks} -> {check.LogStacks} stacks");
        }
        // The pre-cast check matches an aura against the log's state within the tolerance after the step, and there a cast
        // that toggles the aura gains it. An engine still holding the aura then ends it on the cast the log shows gaining it
        // (a mount's end of Stealth: WcTBrQNX46m9axqP fight 7 @ 517.728 before Brooding Shadows @ 533.934, Seething Poison
        // hits behind it; p8VJDcBdxa7ghqG3 fight 124 @ 509.279; pWmcV42K39GHTLQX fight 23 @ 1566.089).
        if (FindAbility(cmd)?.RecastEnds is { } toggled && _hero.Auras.HasAura(toggled, now)
            && _logAuras!.GainedAfterAbsence(toggled.LogId, cmd.Time, _options.AuraToleranceSeconds))
        {
            _hero.Auras.Remove(toggled, now);
            changes.Add($"{toggled.Name} 1 -> 0 stacks");
        }
        if (changes.Count > 0) step.Note = (step.Note == null ? "" : step.Note + "; ") + "Synced to the log: " + string.Join(", ", changes);
    }

    private bool Halt(ReplayResult result)
    {
        if (_verifier.PendingIssues.Count == 0 || !_options.StopOnFirstFailure)
        {
            _verifier.PendingIssues.Clear();
            return false;
        }
        int firstStep = _verifier.PendingIssues.Min(i => i.Step);
        result.HaltedAt = result.Steps.FirstOrDefault(s => s.Index == firstStep) ?? result.Steps.LastOrDefault();
        result.HaltIssues.AddRange(_verifier.PendingIssues);
        return true;
    }


    // ------------------------------------------------------------------ lookups

    private Ability? FindAbility(ReplayCommand cmd) =>
        _hero.Abilities.FirstOrDefault(a => a.IsAbility(cmd.AbilityId))
        ?? _hero.Abilities.FirstOrDefault(a => a.Name.Equals(cmd.AbilityName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The enemy the log's cast targeted, or null for casts without an enemy target.</summary>
    private EnemyTarget? ResolveTarget(UnitRef? target)
    {
        if (target == null || target.Id <= 0) return null;
        var enemy = _ctx.FindEnemy(target.Id, target.Instance);
        return enemy is { IsSleeping: false } ? enemy : null;
    }

    /// <summary>Pairs each begin_cast with the command that ends it (complete_cast or cancel_cast).</summary>
    private static Dictionary<ReplayCommand, ReplayCommand> MatchCastEnds(List<ReplayCommand> commands)
    {
        var result = new Dictionary<ReplayCommand, ReplayCommand>();
        ReplayCommand? open = null;
        foreach (var cmd in commands)
        {
            if (cmd.Kind == CommandKind.BeginCast) open = cmd;
            else if (cmd.Kind is CommandKind.CompleteCast or CommandKind.CancelCast && open?.AbilityId == cmd.AbilityId)
            {
                result[open] = cmd;
                open = null;
            }
        }
        return result;
    }
}

/// <summary>The player's aura state over time according to the log, for the auras the engine models (by log ID).</summary>
public sealed class LogAuraTimeline
{
    private readonly List<LogAura> _events;
    private readonly HashSet<int> _tracked;
    private readonly Dictionary<int, string> _names = new();

    /// <param name="engineStacks">
    /// The engine's stacks of an aura by log ID. The log has no number for the stacks of an inhibited effect's return
    /// (<see cref="InhibitedAuras.StackNotLogged"/>), so until its next number the state reads as the engine's.
    /// </param>
    public LogAuraTimeline(IEnumerable<LogAura> playerAuraEvents, IEnumerable<int> modeledLogIds, Func<int, int> engineStacks)
    {
        _engineStacks = engineStacks;
        _events = InhibitedAuras.Read(playerAuraEvents.OrderBy(e => e.Time).ToList());
        _tracked = modeledLogIds.ToHashSet();
        foreach (var e in _events)
        {
            // An aura whose first event is its removal was up as the fight began (carried over from an earlier pull).
            if (_names.TryAdd(e.AbilityId, e.AbilityName) && e.Type is "removebuff" or "removedebuff") _initial[e.AbilityId] = 1;
        }
    }

    private readonly Dictionary<int, int> _initial = new();
    private readonly Func<int, int> _engineStacks;

    public bool IsTracked(int logId) => logId > 0 && _tracked.Contains(logId);

    public string NameOf(int logId) => _names.TryGetValue(logId, out var name) ? $"{name} ({logId})" : logId.ToString();

    /// <summary>Log ID -> stacks of the tracked auras active at <paramref name="time"/>.</summary>
    public Dictionary<int, int> TrackedStateAt(double time) =>
        StacksAt(time).Where(kv => kv.Value > 0 && IsTracked(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Times of the log's aura changes in [<paramref name="from"/>, <paramref name="to"/>].</summary>
    public IEnumerable<double> ChangeTimesBetween(double from, double to) =>
        _events.Where(e => e.Time >= from && e.Time <= to).Select(e => e.Time).Distinct();

    /// <summary>
    /// The log has the aura absent for the <paramref name="seconds"/> before <paramref name="time"/>, and gains it within
    /// <paramref name="seconds"/> of <paramref name="time"/>.
    /// </summary>
    public bool GainedAfterAbsence(int logId, double time, double seconds)
    {
        bool gained = false;
        foreach (var e in _events)
        {
            if (e.AbilityId != logId || e.Time < time - seconds) continue;
            if (e.Time > time + seconds) break;
            if (e.Time < time - ShownBeforeCastSeconds) return false;
            gained |= e.Type is "applybuff" or "applydebuff";
        }
        return gained && StacksAt(time - seconds).GetValueOrDefault(logId) == 0;
    }

    /// <summary>The offset of the log's state a pre-cast check shows from the cast's time (<see cref="ReplayVerifier"/>).</summary>
    private const double ShownBeforeCastSeconds = 0.001;

    public List<string> UntrackedActiveAt(double time) =>
        StacksAt(time).Where(kv => kv.Value > 0 && !IsTracked(kv.Key)).Select(kv => _names[kv.Key]).OrderBy(n => n).ToList();

    private Dictionary<int, int> StacksAt(double time)
    {
        var stacks = new Dictionary<int, int>(_initial);
        foreach (var e in _events)
        {
            if (e.Time > time) break;
            switch (e.Type)
            {
                case "applybuff" or "applydebuff" or "applybuffstack" or "applydebuffstack" or "removebuffstack" or "removedebuffstack":
                    stacks[e.AbilityId] = e.Stack == InhibitedAuras.StackNotLogged ? _engineStacks(e.AbilityId) : e.Stack ?? 1;
                    break;
                case "refreshbuff" or "refreshdebuff":
                    if (stacks.GetValueOrDefault(e.AbilityId) == 0) stacks[e.AbilityId] = e.Stack ?? 1;
                    break;
                case "removebuff" or "removedebuff":
                    stacks[e.AbilityId] = 0;
                    break;
            }
        }
        return stacks;
    }
}
