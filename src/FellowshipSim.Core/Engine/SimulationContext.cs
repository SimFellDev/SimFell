using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FellowshipSim.Core.Engine;

public class SimulationContext
{
    private readonly Dictionary<(int Id, int InstanceId), EnemyTarget> _enemyLookup = new();
    private readonly List<EnemyTarget> _enemies = new();

    // Damage by name, in the order the names first dealt damage; names equal but for case share one total. Speed: a hit
    // finds its total by its name's string instance (hits use a few, mostly constants) without hashing the characters.
    private readonly List<(string Name, double Damage)> _damageByName = new();
    private readonly Dictionary<string, int> _damageSlotByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _damageSlotByInstance = new(ReferenceEqualityComparer.Instance);
    // Speed: the slots of recent name instances, one per bits of the instance's hash code, ahead of the dictionary.
    private const int RecentDamageNames = 64;
    private readonly string?[] _recentDamageNames = new string?[RecentDamageNames];
    private readonly int[] _recentDamageSlots = new int[RecentDamageNames];

    public Hero Player { get; }
    public SimTime CurrentTime { get; private set; } = SimTime.Zero;
    public IReadOnlyList<EnemyTarget> AllEnemies => _enemies;

    /// <summary>The enemies in combat now (a snapshot: rebuilt, not changed, when one arrives, dies or leaves).</summary>
    public IReadOnlyList<EnemyTarget> ActiveEnemies
    {
        get
        {
            if (_activeEnemies == null)
            {
                var active = new List<EnemyTarget>();
                foreach (var e in _enemies)
                {
                    if (!e.IsSleeping) active.Add(e);
                }
                _activeEnemies = active;
            }
            return _activeEnemies;
        }
    }
    private List<EnemyTarget>? _activeEnemies;

    /// <summary>The enemies in combat now other than <paramref name="target"/>, in the same order (a snapshot, like <see cref="ActiveEnemies"/>).</summary>
    public IReadOnlyList<EnemyTarget> ActiveEnemiesExcept(EnemyTarget? target)
    {
        // Speed: the list is kept until the enemies in combat or the target change; it is never changed once made.
        var active = ActiveEnemies;
        if (_others == null || !ReferenceEquals(active, _othersOf) || target != _othersExcept)
        {
            var others = new List<EnemyTarget>(active.Count);
            foreach (var e in active)
            {
                if (e != target) others.Add(e);
            }
            (_others, _othersOf, _othersExcept) = (others, active, target);
        }
        return _others;
    }
    private List<EnemyTarget>? _others;
    private IReadOnlyList<EnemyTarget>? _othersOf;
    private EnemyTarget? _othersExcept;

    public SimulationKernel Kernel { get; } = new();

    /// <summary>The fraction of a full tick being dealt right now (below 1 during a partial final tick of a periodic effect).</summary>
    public double CurrentTickFraction { get; internal set; } = 1.0;

    /// <summary>The period timer (<see cref="HastedTimer.StartPeriod"/>) whose tick is executing right now.</summary>
    public HastedTimer? ExecutingTick { get; internal set; }

    /// <summary>The periodic effect whose tick is being asked for (<see cref="IOutcomeSource.TickAt"/>) or dealt right now.</summary>
    public PeriodicEffect? CurrentPeriodicEffect { get; internal set; }

    /// <summary>The hit being released (<see cref="IOutcomeSource.ResolveHit"/>) or landed (<see cref="IOutcomeSource.Land"/>) right now.</summary>
    public PendingHit? CurrentHit { get; private set; }

    /// <summary>How early an ability may come off the global cooldown (a replay allows server-frame and latency slack).</summary>
    public double GlobalCooldownToleranceSeconds { get; set; }

    /// <summary>The time the fight ends; <see cref="SimTime.Infinite"/> when it has no set end.</summary>
    public SimTime FightEnd { get; set; } = SimTime.Infinite;

    /// <summary>True while a dungeon boss is in combat, or in a training dummy fight against a boss.</summary>
    public bool Boss { get; set; }

    /// <summary>True while the final boss of a dungeon is in combat.</summary>
    public bool FinalBoss { get; set; }

    /// <summary>True while a dungeon enemy that is not a boss is in combat.</summary>
    public bool Trash { get; set; }

    /// <summary>The estimated start of the next dungeon pull with enemies that are not bosses; <see cref="SimTime.Infinite"/> when none follows or no estimate is possible.</summary>
    public SimTime NextTrashAt { get; set; } = SimTime.Infinite;

    /// <summary>The estimated start of the next dungeon pull with a boss; <see cref="SimTime.Infinite"/> when none follows or no estimate is possible.</summary>
    public SimTime NextBossAt { get; set; } = SimTime.Infinite;

    /// <summary>Raised as an enemy first enters the fight.</summary>
    public event Action<EnemyTarget>? EnemySpawned;
    public SimulationRng Rng { get; } = new();
    public IOutcomeSource Outcomes { get; set; }

    /// <summary>
    /// The hero's delays to cast once a hold ends (<see cref="HoldEnds"/>); null for none, as in a replay, which casts at
    /// the log's times.
    /// </summary>
    public ReactionDelays? Reactions { get; set; }

    /// <summary>
    /// How long a proc's gain takes to reach the action lists (<see cref="Aura.Seen"/>); zero for none, as in a replay,
    /// which casts at the log's times.
    /// </summary>
    public SimTime ProcReactionTime { get; set; }

    /// <summary>Until this time the hero is still reacting to the last hold's end, and casts nothing on the global cooldown.</summary>
    public SimTime ReactionEndTime { get; private set; }
    private SimTime _lastHoldEnd;

    /// <summary>
    /// The hero is held by <paramref name="hold"/> until <paramref name="end"/>: the first time a hold ending there is seen,
    /// the hero's reaction to it is rolled (<see cref="IOutcomeSource.ReactionDelay"/>). A hold ending no later than one
    /// already seen rolls nothing.
    /// </summary>
    public void HoldEnds(SimTime end, ReactionHold hold)
    {
        if (Reactions == null || end <= _lastHoldEnd) return;
        _lastHoldEnd = end;
        ReactionEndTime = end + Outcomes.ReactionDelay(hold, Reactions, this);
    }

    public double TotalDamage { get; private set; }

    /// <summary>Damage dealt by name (a new dictionary of the totals so far, in the order the names first dealt damage).</summary>
    public IReadOnlyDictionary<string, double> AbilityDamage
    {
        get
        {
            var totals = new Dictionary<string, double>(_damageByName.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, damage) in _damageByName) totals[name] = damage;
            return totals;
        }
    }

    /// <summary>The slot of <paramref name="name"/>'s total, added when the name first deals damage.</summary>
    private int DamageSlot(string name)
    {
        if (!_damageSlotByName.TryGetValue(name, out int slot))
        {
            slot = _damageByName.Count;
            _damageByName.Add((name, 0.0));
            _damageSlotByName[name] = slot;
        }
        _damageSlotByInstance[name] = slot;
        return slot;
    }

    public SimulationContext(Hero player)
    {
        Player = player;
        player.SimContext = this;
        player.Auras.RunOutIn(this);
        Outcomes = new RandomOutcomeSource(Rng);
    }

    // ------------------------------------------------------------------ time

    public ScheduledEvent Schedule(SimTime delay, Action<SimulationContext> action, string label = "") =>
        Kernel.ScheduleAt(CurrentTime + delay, action, label);

    public ScheduledEvent ScheduleAt(SimTime time, Action<SimulationContext> action, string label = "") =>
        Kernel.ScheduleAt(time < CurrentTime ? CurrentTime : time, action, label);

    /// <summary>
    /// Moves the queued <paramref name="ev"/> to <paramref name="time"/> with <paramref name="action"/>, as cancelling it and
    /// scheduling a new event would. False when it is no longer queued.
    /// </summary>
    internal bool Reschedule(ref ScheduledEvent ev, SimTime time, Action<SimulationContext> action) =>
        Kernel.Reschedule(ref ev, time < CurrentTime ? CurrentTime : time, action);

    /// <summary>
    /// Advances the clock to <paramref name="time"/>, firing every queued event due on the way in time order (the hero's
    /// aura expiries among them). This is the only way simulation time moves forward.
    /// </summary>
    public void RunUntil(SimTime time) => RunUntil(time, null);

    /// <summary>
    /// Advances the clock as <see cref="RunUntil(SimTime)"/> does, and stops right after an event once <paramref name="stop"/>
    /// holds, the clock at that event. Returns true when it stopped there.
    /// </summary>
    public bool RunUntil(SimTime time, Func<bool>? stop)
    {
        while (Kernel.TryDequeueNode(time) is { } ev)
        {
            if (ev.Time > CurrentTime) CurrentTime = ev.Time;
            if (ev.Hit is { } hit) Land(hit);
            else ev.Action!(this);
            if (stop != null && stop()) return true;
        }
        if (time > CurrentTime) CurrentTime = time;
        return false;
    }

    // ------------------------------------------------------------------ enemies

    public EnemyTarget GetOrSpawnEnemy(int id, int instanceId, string name = "Target")
    {
        var key = (id, instanceId);
        if (!_enemyLookup.TryGetValue(key, out var enemy))
        {
            // A log names an enemy but has no game ID for it. Where several creatures have the name, the highest Spirit
            // Points count.
            enemy = new EnemyTarget(id, instanceId, name)
            {
                SpiritPointValue = GameCreatures.Named(name).Select(c => c.SpiritPoints).DefaultIfEmpty().Max(),
            };
            _enemyLookup[key] = enemy;
            _enemies.Add(enemy);
            enemy.SleepChanged = () => _activeEnemies = null;
            _activeEnemies = null;
            EnemySpawned?.Invoke(enemy);
        }
        return enemy;
    }

    public EnemyTarget? FindEnemy(int id, int instanceId) =>
        _enemyLookup.TryGetValue((id, instanceId), out var enemy) ? enemy : null;

    /// <summary>An enemy enters combat.</summary>
    public EnemyTarget AddEnemy(int id, int instanceId, string name, double maxHealth = 0)
    {
        var enemy = GetOrSpawnEnemy(id, instanceId, name);
        if (maxHealth > 0)
        {
            enemy.MaxHealth = maxHealth;
            enemy.CurrentHealth = maxHealth;
        }
        enemy.Arise(this);
        return enemy;
    }

    /// <summary>Raised as an enemy dies or leaves combat, before it is removed (effects on it can still resolve).</summary>
    public event Action<SimulationContext, EnemyTarget>? EnemyDemising;

    /// <summary>
    /// The hero interrupts <paramref name="enemy"/>'s cast: it ends with no effect, and <see cref="Hero.OnInterrupt"/> is
    /// raised. Returns false when the enemy was not casting.
    /// </summary>
    public bool Interrupt(EnemyTarget enemy)
    {
        string? ability = enemy.CastAbility;
        if (ability == null || !enemy.StopCast()) return false;
        Player.TriggerInterrupt(this, enemy, ability);
        return true;
    }

    /// <summary>An enemy dies (<paramref name="defeated"/>) or leaves combat.</summary>
    public void RemoveEnemy(EnemyTarget enemy, bool defeated = false)
    {
        if (enemy.IsSleeping) return;
        EnemyDemising?.Invoke(this, enemy);
        if (defeated) Player.TriggerEnemyDefeated(this, enemy);
        enemy.Demise(this);
        if (Player.CurrentTarget == enemy)
        {
            Player.CurrentTarget = ActiveEnemies.FirstOrDefault()!;
        }
    }

    // ------------------------------------------------------------------ casting

    /// <summary>
    /// Starts casting <paramref name="ability"/>. Instant abilities resolve immediately; abilities with a
    /// cast time resolve when the cast completes (scheduled, or forced by <see cref="CompleteCast"/>).
    /// </summary>
    /// <param name="force">Cast even if the ability is not ready (cooldown, resources, global cooldown); a replay uses this
    /// to follow the log 1:1. A missing cooldown charge is granted so the cast still starts its cooldown.</param>
    public CastAttempt BeginCast(Hero hero, Ability ability, EnemyTarget? target, bool force = false)
    {
        // The server activates a player's ability from the client's request or its ability queue before the frame's timers
        // (client RPCs and actor ticks, then the timers), unless an ability's end in the frame's timers flushes the
        // queue: a tick's execution, or a cast's completion or a channel's end right before.
        int outer = AbilityActivation;
        if (ExecutingTick == null && (CurrentTime - _lastAbilityEnd).TotalSeconds > AbilityQueueWindowSeconds) AbilityActivation = ++_activations;
        try { return Activate(hero, ability, target, force); }
        finally { AbilityActivation = outer; }
    }

    /// <summary>The ability of the last cast started in this fight (<c>prev.&lt;ability&gt;</c>); null before the first.</summary>
    public Ability? PreviousCast { get; private set; }

    /// <summary>
    /// The ability of the last cast started on the global cooldown in this fight (<c>prev_gcd.&lt;ability&gt;</c>); null before
    /// the first. A cast with <see cref="Ability.IgnoresGcd"/> leaves it as it is.
    /// </summary>
    public Ability? PreviousGcdCast { get; private set; }

    /// <summary>
    /// The ability activation running before the frame's timers (numbered, 0 when none): a haste change in it re-arms a
    /// damage over time's period with the time left measured from the frame before (<see cref="HastedTimer"/>).
    /// </summary>
    public int AbilityActivation { get; private set; }
    private int _activations;

    /// <summary>
    /// How long after a cast's completion or a channel's end a cast still comes from the ability queue that end flushed,
    /// in the same server frame (a replay allows the spread of a frame's events in the log).
    /// </summary>
    public double AbilityQueueWindowSeconds { get; set; }
    private SimTime _lastAbilityEnd = SimTime.FromSeconds(-1.0);

    private CastAttempt Activate(Hero hero, Ability ability, EnemyTarget? target, bool force)
    {
        if (!ability.CanCast(this, out var reason))
        {
            if (!force) return CastAttempt.Rejected(reason ?? "cannot cast");
            if (ability.Cooldown > SimTime.Zero) hero.CooldownOf(ability).GrantCharge(CurrentTime);
        }

        if (hero.CurrentCast != null && !ability.IgnoresGcd)
        {
            return CastAttempt.Rejected($"already casting {hero.CurrentCast.Ability.Name}");
        }

        // An ability off the global cooldown goes during a cast or a channel when it is instant: one with a cast time or a
        // channel of its own waits for the running one to end.
        var running = hero.CurrentCast?.Ability ?? hero.CurrentChannel?.Ability;
        if (running != null && ability.IgnoresGcd && !force
            && (ability.IsChanneled || ability.GetAdjustedCastTime(CurrentTime) > SimTime.Zero))
        {
            return CastAttempt.Rejected($"already casting {running.Name}");
        }

        if (!ability.IgnoresGcd)
        {
            double left = (hero.GlobalCooldownEndTime - CurrentTime).TotalSeconds;
            if (left > GlobalCooldownToleranceSeconds && !force && !ability.UsableDuringGcd)
            {
                return CastAttempt.Rejected($"on global cooldown ({left:F2}s remaining)");
            }
            if (CurrentTime < ReactionEndTime && !force && !ability.UsableDuringGcd)
            {
                return CastAttempt.Rejected($"reacting ({(ReactionEndTime - CurrentTime).TotalSeconds:F3}s remaining)");
            }
            StopChannel(hero);
            double gcd = ability.GlobalCooldownSeconds ?? hero.GlobalCooldownSeconds;
            if (ability.GlobalCooldownScalesWithHaste ?? hero.GlobalCooldownScalesWithHaste) gcd /= 1.0 + hero.GetHaste(CurrentTime);
            hero.GlobalCooldownEndTime = CurrentTime + SimTime.FromSeconds(gcd);
            PreviousGcdCast = ability;
        }

        var castTime = ability.GetAdjustedCastTime(CurrentTime);
        PreviousCast = ability;
        hero.TriggerCastStarted(ability, target, CurrentTime);

        if (castTime == SimTime.Zero)
        {
            FinishCast(hero, ability, target);
            return CastAttempt.Resolved(castTime);
        }

        BeginCast(hero, ability, target, castTime);
        return CastAttempt.Casting(castTime);
    }

    // A method of its own, so Activate allocates the closure only for a cast with a cast time: a closure's captures are
    // allocated where their scope begins.
    private void BeginCast(Hero hero, Ability ability, EnemyTarget? target, SimTime castTime)
    {
        var cast = new ActiveCast(ability, target, CurrentTime, CurrentTime + castTime);
        cast.CompletionEvent = ScheduleAt(cast.EndTime, c => c.CompleteCast(hero), ability.Name);
        hero.CurrentCast = cast;
    }

    /// <summary>
    /// Moves the hero's current cast to complete at <paramref name="time"/> instead of when its cast time runs out
    /// (e.g. the player's latency). Returns false if nothing was being cast.
    /// </summary>
    public bool SetCastCompletion(Hero hero, SimTime time)
    {
        var cast = hero.CurrentCast;
        if (cast == null) return false;
        cast.CompletionEvent?.Cancel();
        cast.CompletionEvent = ScheduleAt(time, c => { if (hero.CurrentCast == cast) c.CompleteCast(hero); }, cast.Ability.Name);
        return true;
    }

    /// <summary>Completes the hero's current cast now. Returns false if nothing was being cast.</summary>
    public bool CompleteCast(Hero hero)
    {
        var cast = hero.CurrentCast;
        if (cast == null) return false;
        cast.CompletionEvent?.Cancel();
        FinishCast(hero, cast.Ability, cast.Target);
        _lastAbilityEnd = CurrentTime;
        return true;
    }

    /// <summary>Cancels the hero's current cast without any of its effects. Returns false if nothing was being cast.</summary>
    public bool CancelCast(Hero hero)
    {
        var cast = hero.CurrentCast;
        if (cast == null) return false;
        cast.CompletionEvent?.Cancel();
        hero.CurrentCast = null;
        // Cancelling a cast frees the global cooldown it started (combat logs, 10 Rime logs: instants cast
        // after a cancelled Frost Bolt, inside its global cooldown).
        if (hero.GlobalCooldownEndTime > CurrentTime) hero.GlobalCooldownEndTime = CurrentTime;
        hero.TriggerCastCancelled(cast.Ability, cast.Target, CurrentTime);
        return true;
    }

    /// <summary>Stops the hero's current channel early. Returns false if nothing was being channeled.</summary>
    public bool StopChannel(Hero hero)
    {
        var channel = hero.CurrentChannel;
        if (channel == null) return false;
        channel.Ticks.Stop(this);
        return true;
    }

    private void FinishCast(Hero hero, Ability ability, EnemyTarget? target)
    {
        // An instant off-GCD ability used mid-cast leaves that cast running.
        if (hero.CurrentCast?.Ability == ability) hero.CurrentCast = null;
        // Untargeted casts act on the hero's current target where they need one; cast events keep the cast's own target.
        ability.ResolveCast(this, target ?? hero.CurrentTarget);
        hero.TriggerCastCompleted(ability, target, CurrentTime);

        if (ability.IsChanneled)
        {
            BeginChannel(hero, ability, target);
        }
    }

    private void BeginChannel(Hero hero, Ability ability, EnemyTarget? target)
    {
        double period = ability.ChannelTickPeriodFor(this) / (1.0 + hero.GetHaste(CurrentTime));
        ActiveChannel? channel = null;
        var ticks = PeriodicEffect.Begin(
            this,
            ability.Name,
            ability.ChannelDuration.TotalSeconds,
            period,
            ability.ChannelTicksOnStart,
            ability.ChannelPartialFinalTick,
            onTick: (c, fraction) =>
            {
                ability.OnChannelTick(c, target ?? hero.CurrentTarget, fraction);
                hero.TriggerChannelTick(ability, target ?? hero.CurrentTarget, fraction, c);
            },
            onEnd: (c, clipped) =>
            {
                if (hero.CurrentChannel == channel) hero.CurrentChannel = null;
                ability.OnChannelEnd(c, target ?? hero.CurrentTarget, clipped);
                hero.TriggerChannelEnded(ability, target, c.CurrentTime, clipped);
                if (!clipped) _lastAbilityEnd = c.CurrentTime;
            },
            // Combat logs: channel ticks come one server frame after the channel starts (Freezing Torrent @ 34.229 ->
            // 34.264, @ 49.522 -> 49.557; Chronoshift @ 24.012 -> 24.045), while the channel still ends on time.
            tickDelaySeconds: PeriodicEffect.ServerFrameSeconds,
            minPartialSeconds: ability.ChannelFinalTickAlways ? 0.0 : PeriodicEffect.ServerFrameSeconds);

        if (ticks.IsActive)
        {
            channel = new ActiveChannel(ability, target, ticks);
            hero.CurrentChannel = channel;
        }
    }

    // ------------------------------------------------------------------ damage

    /// <summary>
    /// A grievous crit's multiplier is (crit chance - 1) x this + the crit multiplier (game data).
    /// </summary>
    public const double GrievousCritScalar = 1.0;

    /// <summary>The numbers of a hit at a moment: base damage (before crit and spread), crit chance and crit multiplier.</summary>
    public readonly record struct HitNumbers(double BaseDamage, double CritChance, double CritMultiplier);

    /// <summary>
    /// Deals one hit. <paramref name="compute"/> gives the hit's numbers from the current state; it is evaluated on
    /// release (to decide crit and travel) and again on landing, where damage is dealt with the stats at that moment.
    /// Crit chance above 100% is a grievous crit: base x (crit multiplier + overflow x <see cref="GrievousCritScalar"/>).
    /// <paramref name="onLanded"/> is called with the damage actually dealt, once the hit lands (0 when it hit nothing).
    /// A <paramref name="snapshot"/> hit keeps the numbers it had on release.
    /// </summary>
    public double ApplyDamage(Hero source, EnemyTarget? target, int abilityId, string abilityName, bool canCrit,
        Func<SimulationContext, HitNumbers> compute, bool? forcedCrit = null, Action<SimulationContext, double>? onLanded = null,
        bool snapshot = false)
    {
        var hit = RentHit(source, target, abilityId, abilityName, canCrit, forcedCrit, onLanded, snapshot);
        hit.FromCompute(compute);
        return ApplyDamage(hit);
    }

    /// <summary>
    /// Deals one hit with fixed numbers (no dependence on the moment it lands). A <paramref name="snapshot"/> hit lands
    /// with the time of its release.
    /// </summary>
    public double ApplyDamage(Hero source, EnemyTarget? target, double baseDamage, int abilityId, string abilityName,
        bool canCrit, double critChance, double critMultiplier, bool? forcedCrit = null, bool snapshot = false)
    {
        var hit = RentHit(source, target, abilityId, abilityName, canCrit, forcedCrit, onLanded: null, snapshot);
        hit.FromNumbers(new HitNumbers(baseDamage, critChance, critMultiplier));
        return ApplyDamage(hit);
    }

    // Speed: hits are pooled. A hit returns to the pool once it has landed; one still travelling when the fight resets
    // leaves the queue with it.
    private readonly Stack<PendingHit> _hitPool = new();

    /// <summary>A hit from the pool, to be filled with its numbers and dealt with <see cref="ApplyDamage(PendingHit)"/>.</summary>
    internal PendingHit RentHit(Hero source, EnemyTarget? target, int abilityId, string abilityName, bool canCrit, bool? forcedCrit,
        Action<SimulationContext, double>? onLanded, bool snapshot)
    {
        var hit = _hitPool.TryPop(out var pooled) ? pooled : new PendingHit();
        hit.Begin(source, target, abilityId, abilityName, canCrit, forcedCrit, onLanded, snapshot);
        return hit;
    }

    /// <summary>Releases <paramref name="hit"/>: decides its crit and travel, and lands it now or after its travel time.</summary>
    internal double ApplyDamage(PendingHit hit)
    {
        hit.TickFraction = CurrentTickFraction;
        var released = Request(hit);
        hit.Released = released;
        var outer = CurrentHit;
        CurrentHit = hit;
        var outcome = Outcomes.ResolveHit(released, this);
        CurrentHit = outer;
        hit.Outcome = outcome;

        // The hit lands (and triggers procs) after its travel time; damage uses the stats at landing.
        if (outcome.LandingDelay > SimTime.Zero)
        {
            // A damage over time's own hit lands inside the tick that released it.
            hit.Tick = ExecutingTick is { } tick && tick.DamageLogId == hit.AbilityId ? tick : null;
            Kernel.ScheduleAt(CurrentTime + outcome.LandingDelay, hit, hit.AbilityName);
            return released.BaseDamage * (outcome.IsCrit ? released.CritMultiplier : 1.0);
        }
        return Land(hit);
    }

    private HitRequest Request(PendingHit hit)
    {
        var n = hit.Compute(this);
        double chance = 0.0, baseDamage = n.BaseDamage;
        if (hit.CanCrit)
        {
            // Grievous: the crit multiplier plus the overflow (the crit multiplier applies as the hit lands).
            if (n.CritChance > 1.0 && n.CritMultiplier > 0.0)
            {
                baseDamage *= (n.CritMultiplier + (n.CritChance - 1.0) * GrievousCritScalar) / n.CritMultiplier;
            }
            chance = Math.Clamp(n.CritChance, 0.0, 1.0);
        }
        double tickFraction = hit.TickFraction;
        return new HitRequest(hit.Source, hit.Target, hit.AbilityId, hit.AbilityName, baseDamage, hit.CanCrit, chance, n.CritMultiplier, CurrentTime, tickFraction,
            hit.ForcedCrit);
    }

    /// <summary>The hit lands: deals its damage, raises <see cref="Hero.OnDamageDealt"/> and returns it to the pool.</summary>
    private double Land(PendingHit hit)
    {
        if (hit.Tick is { } tick)
        {
            hit.Tick = null;
            if (tick.IsActive && tick.LastTick == hit.Released.Time) return LandInLastTick(tick, hit);
        }
        var outcome = hit.Outcome;
        var onLanded = hit.OnLanded;
        double dealt = 0.0;
        // A hit on a target already gone hits nothing; its projectile still finishes (e.g. a swallow comes back).
        if (outcome.Missed)
        {
            onLanded?.Invoke(this, 0.0);
            ReturnHit(hit);
            return dealt;
        }
        var released = hit.Released;
        var landed = hit.Snapshot || CurrentTime == released.Time ? released : Request(hit);
        var outer = CurrentHit;
        CurrentHit = hit;
        dealt = Outcomes.Land(landed, outcome, this);
        CurrentHit = outer;

        double critChance = landed.ForcedCrit is { } forced ? (forced ? 1.0 : 0.0) : landed.CanCrit ? landed.CritChance : 0.0;
        Deal(hit.Source, hit.Target, hit.AbilityId, hit.AbilityName, dealt, outcome.IsCrit, critChance, hit.Ability?.Name);
        Outcomes.HitFinished(this);
        onLanded?.Invoke(this, dealt);
        ReturnHit(hit);
        return dealt;
    }

    // A method of its own, so Land allocates the closure only for a hit in a tick's last execution: a closure's captures
    // are allocated where their scope begins.
    private double LandInLastTick(HastedTimer tick, PendingHit hit)
    {
        double inTick = 0.0;
        tick.RunInLastTick(() => inTick = Land(hit));
        return inTick;
    }

    /// <summary>
    /// Deals a fixed amount with no <see cref="IOutcomeSource"/> call: a replay deals each logged hit no engine hit matched
    /// this way.
    /// </summary>
    public void DealLoggedHit(Hero source, EnemyTarget target, int abilityId, string abilityName, double amount, bool isCrit) =>
        Deal(source, target, abilityId, abilityName, amount, isCrit, isCrit ? 1.0 : 0.0);

    /// <summary>Adds <paramref name="dealt"/> to the totals and the target's health loss, then raises <see cref="Hero.OnDamageDealt"/>.</summary>
    private void Deal(Hero source, EnemyTarget? target, int abilityId, string abilityName, double dealt, bool isCrit, double critChance,
        string? abilityGroup = null)
    {
        TotalDamage += dealt;
        int recent = RuntimeHelpers.GetHashCode(abilityName) & (RecentDamageNames - 1);
        int slot;
        if (ReferenceEquals(_recentDamageNames[recent], abilityName)) slot = _recentDamageSlots[recent];
        else
        {
            if (!_damageSlotByInstance.TryGetValue(abilityName, out slot)) slot = DamageSlot(abilityName);
            _recentDamageNames[recent] = abilityName;
            _recentDamageSlots[recent] = slot;
        }
        CollectionsMarshal.AsSpan(_damageByName)[slot].Damage += dealt;
        if (target != null)
        {
            target.HeroDamage += dealt;
            if (target.MaxHealth > 0) target.CurrentHealth = Math.Max(0.0, target.CurrentHealth - dealt);
        }
        source.TriggerDamageDealt(new DamageContext(source, target, abilityId, abilityName, dealt, isCrit, CurrentTime, this, critChance, abilityGroup));
    }

    private void ReturnHit(PendingHit hit)
    {
        hit.Clear();
        _hitPool.Push(hit);
    }

    /// <summary>Deals one hit using the source's crit chance (plus <paramref name="bonusCritChance"/>) and crit multiplier at landing.</summary>
    public double ApplyDamage(
        Hero source,
        EnemyTarget? target,
        double baseDamage,
        bool canCrit = true,
        double bonusCritChance = 0.0,
        string abilityName = "Direct Hit",
        int abilityId = 0,
        bool? forcedCrit = null) =>
        ApplyDamage(source, target, abilityId, abilityName, canCrit,
            c => new HitNumbers(baseDamage, source.CritChanceAgainst(target, c.CurrentTime) + bonusCritChance, source.CritMultiplier(c.CurrentTime)),
            forcedCrit);

    public void Reset()
    {
        CurrentTime = SimTime.Zero;
        _lastAbilityEnd = SimTime.FromSeconds(-1.0);
        PreviousCast = null;
        PreviousGcdCast = null;
        Kernel.Clear();
        // The cleared queue lost the event of the hero's next aura expiry.
        Player.Auras.RunOutIn(this);
        TotalDamage = 0;
        _damageByName.Clear();
        _damageSlotByName.Clear();
        _damageSlotByInstance.Clear();
        Array.Clear(_recentDamageNames);
        foreach (var enemy in _enemies)
        {
            enemy.Auras.ClearAll();
            enemy.IsSleeping = false;
        }
    }
}

public sealed class ActiveCast
{
    public ActiveCast(Ability ability, EnemyTarget? target, SimTime startTime, SimTime endTime)
    {
        Ability = ability;
        Target = target;
        StartTime = startTime;
        EndTime = endTime;
    }

    public Ability Ability { get; }
    public EnemyTarget? Target { get; }
    public SimTime StartTime { get; }
    public SimTime EndTime { get; }
    internal ScheduledEvent? CompletionEvent { get; set; }
}

public sealed class ActiveChannel
{
    public ActiveChannel(Ability ability, EnemyTarget? target, PeriodicEffect ticks)
    {
        Ability = ability;
        Target = target;
        Ticks = ticks;
    }

    public Ability Ability { get; }
    public EnemyTarget? Target { get; }
    public PeriodicEffect Ticks { get; }
}

public enum CastStatus
{
    /// <summary>The ability resolved immediately (instant cast).</summary>
    Resolved,
    /// <summary>A cast with a cast time is in progress.</summary>
    Casting,
    /// <summary>The engine refused the cast (cooldown, resources, already casting...).</summary>
    Rejected
}

public readonly record struct CastAttempt(CastStatus Status, SimTime CastTime, string? Reason)
{
    public static CastAttempt Resolved(SimTime castTime) => new(CastStatus.Resolved, castTime, null);
    public static CastAttempt Casting(SimTime castTime) => new(CastStatus.Casting, castTime, null);
    public static CastAttempt Rejected(string reason) => new(CastStatus.Rejected, SimTime.Zero, reason);

    public bool Succeeded => Status != CastStatus.Rejected;
}
