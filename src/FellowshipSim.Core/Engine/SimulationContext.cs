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

    /// <summary>How early an ability may come off the global cooldown (a replay allows server-frame and latency slack).</summary>
    public double GlobalCooldownToleranceSeconds { get; set; }
    public SimulationRng Rng { get; } = new();
    public IOutcomeSource Outcomes { get; set; }

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
    /// Advances the clock to <paramref name="time"/>, firing every queued event due on the way in time order (the hero's
    /// aura expiries among them). This is the only way simulation time moves forward.
    /// </summary>
    public void RunUntil(SimTime time)
    {
        while (Kernel.TryDequeueDue(time, out var ev))
        {
            if (ev.Time > CurrentTime) CurrentTime = ev.Time;
            ev.HasFired = true;
            if (ev.Hit is { } hit) Land(hit);
            else ev.Action!(this);
        }
        if (time > CurrentTime) CurrentTime = time;
    }

    // ------------------------------------------------------------------ enemies

    public EnemyTarget GetOrSpawnEnemy(int id, int instanceId, string name = "Target")
    {
        var key = (id, instanceId);
        if (!_enemyLookup.TryGetValue(key, out var enemy))
        {
            enemy = new EnemyTarget(id, instanceId, name) { SpiritPointValue = Enemies.CreatureSpiritPoints.For(name) };
            _enemyLookup[key] = enemy;
            _enemies.Add(enemy);
            enemy.SleepChanged = () => _activeEnemies = null;
            _activeEnemies = null;
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

    /// <summary>An enemy dies or leaves combat.</summary>
    public void RemoveEnemy(EnemyTarget enemy)
    {
        if (enemy.IsSleeping) return;
        EnemyDemising?.Invoke(this, enemy);
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
        if (!ability.CanCast(this, out var reason))
        {
            if (!force) return CastAttempt.Rejected(reason ?? "cannot cast");
            if (ability.Cooldown > SimTime.Zero) hero.CooldownOf(ability).GrantCharge(CurrentTime);
        }

        if (hero.CurrentCast != null && !ability.IgnoresGcd)
        {
            return CastAttempt.Rejected($"already casting {hero.CurrentCast.Ability.Name}");
        }

        if (!ability.IgnoresGcd)
        {
            double left = (hero.GlobalCooldownEndTime - CurrentTime).TotalSeconds;
            if (left > GlobalCooldownToleranceSeconds && !force)
            {
                return CastAttempt.Rejected($"on global cooldown ({left:F2}s remaining)");
            }
            StopChannel(hero);
            double gcd = ability.GlobalCooldownSeconds ?? hero.GlobalCooldownSeconds;
            if (hero.GlobalCooldownScalesWithHaste) gcd /= 1.0 + hero.GetHaste(CurrentTime);
            hero.GlobalCooldownEndTime = CurrentTime + SimTime.FromSeconds(gcd);
        }

        var castTime = ability.GetAdjustedCastTime(CurrentTime);
        hero.TriggerCastStarted(ability, target, CurrentTime);

        if (castTime == SimTime.Zero)
        {
            FinishCast(hero, ability, target);
            return CastAttempt.Resolved(castTime);
        }

        var cast = new ActiveCast(ability, target, CurrentTime, CurrentTime + castTime);
        cast.CompletionEvent = ScheduleAt(cast.EndTime, c => c.CompleteCast(hero), ability.Name);
        hero.CurrentCast = cast;
        return CastAttempt.Casting(castTime);
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
        return true;
    }

    /// <summary>Cancels the hero's current cast without any of its effects. Returns false if nothing was being cast.</summary>
    public bool CancelCast(Hero hero)
    {
        var cast = hero.CurrentCast;
        if (cast == null) return false;
        cast.CompletionEvent?.Cancel();
        hero.CurrentCast = null;
        // Cancelling a cast frees the global cooldown it started (combat logs, 10 Rime logs: all 46 instants cast
        // 0.2-1.0s after a cancelled Frost Bolt, inside its global cooldown).
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
            },
            // Combat logs: channel ticks come one server frame after the channel starts (Freezing Torrent @ 34.229 ->
            // 34.264, @ 49.522 -> 49.557; Chronoshift @ 24.012 -> 24.045), while the channel still ends on time.
            tickDelaySeconds: PeriodicEffect.ServerFrameSeconds);

        if (ticks.IsActive)
        {
            channel = new ActiveChannel(ability, target, ticks);
            hero.CurrentChannel = channel;
        }
    }

    // ------------------------------------------------------------------ damage

    /// <summary>The numbers of a hit at a moment: base damage (before crit and spread), crit chance and crit multiplier.</summary>
    public readonly record struct HitNumbers(double BaseDamage, double CritChance, double CritMultiplier);

    /// <summary>
    /// Deals one hit. <paramref name="compute"/> gives the hit's numbers from the current state; it is evaluated on
    /// release (to decide crit and travel) and again on landing, where damage is dealt with the stats at that moment.
    /// Crit chance above 100% is a grievous crit: "increases the base damage of the hit by an amount equal to the
    /// overflowing critical strike chance": base x (2 + overflow) x (1 + crit power) (community measurements on a training
    /// dummy and Season 3 logs), i.e. the overflow joins the 2x crit double and only crit power scales it.
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
        var outcome = hit.ForcedCrit.HasValue ? new HitOutcome(hit.ForcedCrit.Value) : Outcomes.ResolveHit(released, this);
        hit.Outcome = outcome;

        // The hit lands (and triggers procs) after its travel time; damage uses the stats at landing.
        if (outcome.LandingDelay > SimTime.Zero)
        {
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
            // Grievous: base x (2 + overflow) x (1 + crit power), i.e. the overflow is added to the crit double.
            if (n.CritChance > 1.0) baseDamage *= (StatBlock.BaseCritMultiplier + n.CritChance - 1.0) / StatBlock.BaseCritMultiplier;
            chance = Math.Clamp(n.CritChance, 0.0, 1.0);
        }
        double tickFraction = hit.TickFraction;
        return new HitRequest(hit.Source, hit.Target, hit.AbilityId, hit.AbilityName, baseDamage, hit.CanCrit, chance, n.CritMultiplier, CurrentTime, tickFraction);
    }

    /// <summary>The hit lands: deals its damage, raises <see cref="Hero.OnDamageDealt"/> and returns it to the pool.</summary>
    private double Land(PendingHit hit)
    {
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
        bool forced = hit.ForcedCrit.HasValue;
        double multiplier = forced
            ? (outcome.IsCrit ? landed.CritMultiplier : 1.0)
            : Outcomes.Land(landed, outcome, this);
        dealt = landed.BaseDamage * multiplier;

        var source = hit.Source;
        var target = hit.Target;
        string abilityName = hit.AbilityName;
        TotalDamage += dealt;
        if (!_damageSlotByInstance.TryGetValue(abilityName, out int slot)) slot = DamageSlot(abilityName);
        CollectionsMarshal.AsSpan(_damageByName)[slot].Damage += dealt;
        if (target != null && target.MaxHealth > 0)
        {
            target.CurrentHealth = Math.Max(0.0, target.CurrentHealth - dealt);
        }
        source.TriggerDamageDealt(new DamageContext(source, target, hit.AbilityId, abilityName, dealt, outcome.IsCrit, CurrentTime, this));
        if (!forced) Outcomes.HitFinished(this);
        onLanded?.Invoke(this, dealt);
        ReturnHit(hit);
        return dealt;
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
        Kernel.Clear();
        // The cleared queue lost the event of the hero's next aura expiry.
        Player.Auras.RunOutIn(this);
        TotalDamage = 0;
        _damageByName.Clear();
        _damageSlotByName.Clear();
        _damageSlotByInstance.Clear();
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
