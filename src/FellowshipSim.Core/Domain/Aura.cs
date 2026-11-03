using System.Numerics;
using System.Runtime.CompilerServices;

namespace FellowshipSim.Core.Domain;

/// <summary>
/// The definition of an aura (buff/debuff): identity, duration, stacking and stat modifiers.
/// <see cref="LogId"/> is the ID combat logs use for it (effect ID + 1,000,000), or 0 for engine-internal auras.
/// </summary>
public sealed partial record AuraDef(string Name, int LogId, SimTime Duration, int MaxStacks, IReadOnlyList<StatModifier> Modifiers)
{
    // Speed: Key is built once. Setting Name, LogId or SourceId rebuilds it, so a `with` copy has its own Key.
    public string Name { get; init { field = value; Key = KeyOf(value, LogId, SourceId); } } = Name;
    public int LogId { get; init { field = value; Key = KeyOf(Name, value, SourceId); } } = LogId;

    /// <summary>Who applied it, when the same aura from different sources stacks as separate instances (0 = one instance).</summary>
    public int SourceId { get; init { field = value; Key = KeyOf(Name, LogId, value); } }

    /// <summary>
    /// On refresh, up to this fraction of <see cref="Duration"/> of the time left carries over (0 = a refresh restarts the duration).
    /// </summary>
    public double RefreshCarryoverFraction { get; init; }

    /// <summary>When above 0, a re-application adds this many seconds to the time left instead of restarting the duration.</summary>
    public double ReapplyExtendsSeconds { get; init; }

    /// <summary>When true, a re-application adds its stacks and leaves the time left as it is.</summary>
    public bool ReapplyKeepsExpiry { get; init; }

    /// <summary>
    /// When true, the aura stays when the hero dies. Combat logs remove every other aura of the hero at its death.
    /// </summary>
    public bool KeepsOnDeath { get; init; }

    /// <summary>
    /// When true, the aura's expiry takes one stack and restarts its duration; the expiry of its last stack removes it.
    /// </summary>
    public bool ExpiryRemovesOneStack { get; init; }

    /// <summary>
    /// True for a proc: an aura a random chance applies. An action list sees a gain of it only after the proc reaction time
    /// (<see cref="Aura.Seen"/>).
    /// </summary>
    public bool IsProc { get; init; }

    public string Key { get; private init { field = value; Slot = AuraSlots.Of(value); } } = KeyOf(Name, LogId, 0);

    /// <summary>The slot of <see cref="Key"/> (<see cref="AuraSlots"/>).</summary>
    internal int Slot { get; private init; } = AuraSlots.Of(KeyOf(Name, LogId, 0));

    // Speed: StatMask is built once. Setting Modifiers rebuilds it.
    public IReadOnlyList<StatModifier> Modifiers { get; init { field = value; StatMask = MaskOf(value); } } = Modifiers;

    /// <summary>The stats the aura's modifiers change: bit <c>1 &lt;&lt; (int)type</c> for each <see cref="StatType"/>.</summary>
    public int StatMask { get; private init; } = MaskOf(Modifiers);

    /// <summary>True when one of the aura's modifiers changes one of the stats in <paramref name="mask"/>.</summary>
    public bool Changes(int mask) => (StatMask & mask) != 0;

    public static int MaskOf(StatType type) => 1 << (int)type;

    private static int MaskOf(IReadOnlyList<StatModifier> modifiers)
    {
        int mask = 0;
        for (int i = 0; i < modifiers.Count; i++) mask |= MaskOf(modifiers[i].Type);
        return mask;
    }

    private static string KeyOf(string name, int logId, int sourceId) =>
        logId > 0 ? (sourceId > 0 ? $"#{logId}@{sourceId}" : $"#{logId}") : name;

    public AuraDef WithDuration(double seconds) => this with { Duration = SimTime.FromSeconds(seconds) };
    public AuraDef WithModifiers(params StatModifier[] modifiers) => this with { Modifiers = modifiers };
    public AuraDef WithMaxStacks(int maxStacks) => this with { MaxStacks = maxStacks };

    /// <summary>
    /// An aura from a game effect, logged as the effect ID + 1,000,000. A <paramref name="durationSeconds"/> of 0 lasts until
    /// removed.
    /// </summary>
    public static AuraDef Effect(string name, int effectId, double durationSeconds = 0, int maxStacks = 1, params StatModifier[] modifiers) =>
        new(name, LogIds.Effect(effectId), durationSeconds > 0 ? SimTime.FromSeconds(durationSeconds) : SimTime.Infinite, maxStacks, modifiers);

    /// <summary>An engine-internal aura that combat logs never show.</summary>
    public static AuraDef Internal(string name, double durationSeconds, int maxStacks = 1, params StatModifier[] modifiers) =>
        new(name, 0, durationSeconds <= 0 ? SimTime.Infinite : SimTime.FromSeconds(durationSeconds), maxStacks, modifiers);

    /// <summary>
    /// Every aura declared as a <c>public static readonly AuraDef</c> on <paramref name="types"/> or their base classes. The
    /// table of declarations is written at build time by FellowshipSim.Core.Generators' DeclaredAurasGenerator.
    /// </summary>
    public static IEnumerable<AuraDef> DeclaredOn(IEnumerable<Type> types) =>
        types.Distinct().SelectMany(t => Declared.GetValueOrDefault(t) ?? []);
}

public class Aura
{
    public Aura(AuraDef def)
    {
        Def = def;
    }

    /// <summary>An engine-internal aura by name (not a game effect).</summary>
    public Aura(string name, SimTime duration, params StatModifier[] modifiers)
        : this(new AuraDef(name, 0, duration, 1, modifiers)) { }

    public Aura(string name, SimTime duration, int maxStacks, params StatModifier[] modifiers)
        : this(new AuraDef(name, 0, duration, maxStacks, modifiers)) { }

    public AuraDef Def { get; }
    public string Name => Def.Name;
    /// <summary>The aura's action-list name ("glacial_assault"). Not on <see cref="AuraDef"/>: a cached field would join its record equality.</summary>
    public string AplSlug => field ??= APL.AplNames.Slug(Def.Name);
    public int LogId => Def.LogId;
    public string Key => Def.Key;
    public SimTime Duration => Def.Duration;
    public int MaxStacks => Def.MaxStacks;
    public IReadOnlyList<StatModifier> Modifiers => Def.Modifiers;

    public SimTime AppliedAt { get; internal set; } = SimTime.Zero;
    // Only AuraContainer changes these, so it can cache its folds and next expiry.
    public SimTime ExpiresAt { get; internal set; } = SimTime.Infinite;
    public int Stacks { get; internal set; } = 1;

    /// <summary>The time of the last gain: the aura's application, a refresh or a stack.</summary>
    public SimTime GainedAt { get; internal set; }

    /// <summary>The stacks before the last gain; 0 when the gain applied the aura.</summary>
    public int StacksBeforeGain { get; internal set; }

    /// <summary>The expiry before the last gain.</summary>
    public SimTime ExpiresBeforeGain { get; internal set; }

    public bool IsExpired(SimTime now) => now >= ExpiresAt;

    /// <summary>
    /// The aura as an action list sees it at <paramref name="now"/> with <paramref name="reaction"/> as the proc reaction
    /// time. A proc gained less than <paramref name="reaction"/> ago is in its state before the gain: its stacks and expiry
    /// then (no more stacks than it has now), or null when it was down or that expiry has passed. Any other aura is as it is.
    /// </summary>
    public TargetEffect? Seen(SimTime now, SimTime reaction)
    {
        if (!Def.IsProc || now >= GainedAt + reaction) return TargetEffect.Of(this);
        if (StacksBeforeGain == 0 || ExpiresBeforeGain <= now) return null;
        return new TargetEffect(ExpiresBeforeGain, Math.Min(StacksBeforeGain, Stacks), MaxStacks, Duration);
    }

    /// <summary>
    /// The time an action list sees the last gain with <paramref name="reaction"/> as the proc reaction time, when it is
    /// after <paramref name="now"/>; otherwise <see cref="SimTime.Infinite"/>.
    /// </summary>
    public SimTime SeenGainAt(SimTime now, SimTime reaction) =>
        Def.IsProc && now < GainedAt + reaction ? GainedAt + reaction : SimTime.Infinite;
}

public class AuraContainer
{
    private readonly Dictionary<string, Aura> _auras = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _uptimeMs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimTime> _activeSince = new(StringComparer.Ordinal);

    // Speed: each stat has a version. It changes when an aura with a modifier of that stat is added or removed, or changes
    // its stacks or its values, which invalidates that stat's cached fold. A refresh or an extension that changes only the
    // expiry keeps every fold. An unchanged stat keeps the same auras in the same order, so its fold would come out the
    // same. _nextExpiry is never later than the earliest expiry, so CleanExpired only looks at the auras once one may have
    // run out.
    private static readonly int StatTypes = Enum.GetValues<StatType>().Length;
    private readonly int[] _statVersions = new int[StatTypes];
    private SimTime _nextExpiry = SimTime.Infinite;
    private readonly ModifierFold[] _folds = new ModifierFold[StatTypes];
    private readonly int[] _foldVersions = Enumerable.Repeat(-1, StatTypes).ToArray();
    private readonly double[] _bonuses = new double[StatTypes];
    private readonly int[] _bonusVersions = Enumerable.Repeat(-1, StatTypes).ToArray();

    // The fight whose events run the auras out (a hero's auras), and the event queued for the next expiry.
    private SimulationContext? _fight;
    private ScheduledEvent? _expiry;
    private Action<SimulationContext>? _runOut;

    /// <summary>
    /// From now on the auras run out in events of <paramref name="ctx"/>, each at its expiry, so what an expiry changes
    /// (haste, a cooldown's rate) changes at that time. Without it an aura runs out when the auras are next read.
    /// </summary>
    internal void RunOutIn(SimulationContext ctx)
    {
        _fight = ctx;
        _expiry?.Cancel();
        _expiry = null;
        QueueExpiry();
    }

    /// <summary>Queues the event of the next expiry, unless one is queued for that time or earlier.</summary>
    private void QueueExpiry()
    {
        if (_fight == null || _nextExpiry == SimTime.Infinite) return;
        if (_expiry is { HasFired: false, IsCancelled: false } queued)
        {
            if (queued.Time <= _nextExpiry) return;
            queued.Cancel();
        }
        _expiry = _fight.ScheduleAt(_nextExpiry, _runOut ??= c => CleanExpired(c.CurrentTime), "Aura expiry");
    }

    // Hasted timers re-read haste on every aura change. They are kept in a list rather than subscribing to the events,
    // since adding and removing event handlers copies the handler list each time (many timers start and stop per second).
    private readonly List<HastedTimer> _timers = new();

    private int _finishedTimers;
    private int _notifying;

    internal void AddTimer(HastedTimer timer) => _timers.Add(timer);

    /// <summary>
    /// A new fight starts: the last fight's timers stop receiving updates. A timer reads haste at its own fight's time, so a
    /// stale timer would expire this fight's auras at the end time of the last one.
    /// </summary>
    internal void ClearTimers()
    {
        _timers.Clear();
        _finishedTimers = 0;
    }

    /// <summary>A timer finished: finished timers are cleared out in batches (they ignore updates meanwhile).</summary>
    internal void RemoveTimer(HastedTimer timer)
    {
        if (++_finishedTimers < 32 || _finishedTimers * 2 < _timers.Count || _notifying > 0) return;
        _timers.RemoveAll(t => !t.IsActive);
        _finishedTimers = 0;
    }

    /// <summary>Timers only depend on haste, so only an aura with a haste modifier can change them.</summary>
    private void NotifyTimers(Aura aura)
    {
        var mods = aura.Modifiers;
        bool haste = false;
        for (int i = 0; i < mods.Count && !haste; i++) haste = mods[i].Type is StatType.Haste or StatType.HasteRating;
        if (!haste) return;
        // Timers started meanwhile are new (they read haste themselves); finished ones are only cleared out between updates.
        _notifying++;
        try
        {
            int count = _timers.Count;
            for (int i = 0; i < count; i++) _timers[i].Update();
        }
        finally { _notifying--; }
    }

    /// <summary>
    /// Raised whenever an aura is applied, stacked or refreshed, or loses a stack on expiry with its duration restarting
    /// (<see cref="AuraDef.ExpiryRemovesOneStack"/>): aura, stacks before, time.
    /// </summary>
    public event Action<Aura, int, SimTime>? OnGained;

    /// <summary>Raised when an aura is removed or expires.</summary>
    public event Action<Aura, SimTime>? OnRemoved;

    /// <summary>Raised when an aura loses stacks but stays (aura, stacks before, time).</summary>
    public event Action<Aura, int, SimTime>? OnStacksLost;

    public IReadOnlyDictionary<string, long> UptimeMs => _uptimeMs;

    /// <summary>
    /// The game ends timed auras on the server frame after their duration runs out: across 10 Rime logs, auras last
    /// 0.006-0.026s longer than their duration (never a full 33ms frame), so on average half a frame.
    /// </summary>
    public const double ExpiryLagSeconds = 1.0 / 60.0;

    /// <summary>
    /// Applies <paramref name="def"/>: a new aura starts with <paramref name="stacks"/> stacks; an existing one is
    /// refreshed and gains <paramref name="stacks"/> (capped at max), or is set to <paramref name="setStacks"/>.
    /// </summary>
    public Aura Apply(AuraDef def, SimTime now, int stacks = 1, int? setStacks = null)
    {
        CleanExpired(now);
        var expires = def.Duration == SimTime.Infinite ? SimTime.Infinite : now + def.Duration + SimTime.FromSeconds(ExpiryLagSeconds);
        if (Find(def) is { } existing)
        {
            int before = existing.Stacks;
            var previousExpiry = existing.ExpiresAt;
            int changedStats = 0;
            if (!ReferenceEquals(existing.Def, def) && existing.Def != def)
            {
                // Re-applied with different values (e.g. a bonus recomputed from current stats): the new values win.
                changedStats = existing.Def.StatMask | def.StatMask;
                existing = new Aura(def) { AppliedAt = existing.AppliedAt, Stacks = existing.Stacks };
                _auras[def.Key] = existing;
                Place(def.Slot, existing);
                Membership++;
            }
            if (def.ReapplyKeepsExpiry && previousExpiry > now)
            {
                expires = previousExpiry;
            }
            else if (def.ReapplyExtendsSeconds > 0 && expires != SimTime.Infinite && previousExpiry > now)
            {
                expires = previousExpiry + SimTime.FromSeconds(def.ReapplyExtendsSeconds);
            }
            else if (def.RefreshCarryoverFraction > 0 && expires != SimTime.Infinite && previousExpiry > now)
            {
                double left = (previousExpiry - now).TotalSeconds;
                expires += SimTime.FromSeconds(Math.Min(left, def.Duration.TotalSeconds * def.RefreshCarryoverFraction));
            }
            existing.GainedAt = now;
            existing.StacksBeforeGain = before;
            existing.ExpiresBeforeGain = previousExpiry;
            existing.ExpiresAt = expires;
            existing.Stacks = Math.Clamp(setStacks ?? (existing.Stacks + stacks), 1, existing.MaxStacks);
            if (existing.Stacks != before) changedStats |= existing.Def.StatMask;
            StatsChanged(changedStats);
            ExpiryChanged(expires);
            OnGained?.Invoke(existing, before, now);
            NotifyTimers(existing);
            return existing;
        }

        var aura = new Aura(def)
        {
            AppliedAt = now,
            GainedAt = now,
            ExpiresAt = expires,
            Stacks = Math.Clamp(setStacks ?? stacks, 1, def.MaxStacks)
        };
        _auras[def.Key] = aura;
        Place(def.Slot, aura);
        Membership++;
        _activeSince[def.Key] = now;
        StatsChanged(def.StatMask);
        ExpiryChanged(expires);
        OnGained?.Invoke(aura, 0, now);
        NotifyTimers(aura);
        return aura;
    }

    /// <summary>The cached folds of the stats in <paramref name="mask"/> (an <see cref="AuraDef.StatMask"/>) are stale.</summary>
    private void StatsChanged(int mask)
    {
        for (; mask != 0; mask &= mask - 1) _statVersions[BitOperations.TrailingZeroCount(mask)]++;
    }

    /// <summary>An aura's expiry was set: it may now be the next to run out.</summary>
    private void ExpiryChanged(SimTime expires)
    {
        if (expires >= _nextExpiry) return;
        _nextExpiry = expires;
        QueueExpiry();
    }

    /// <summary>Changes whenever the auras with a modifier of <paramref name="type"/> or their stacks change.</summary>
    internal int StatVersion(StatType type) => _statVersions[(int)type];

    /// <summary>
    /// Changes whenever an aura is added or removed, or is replaced by an aura of other values. Between two changes
    /// <see cref="FindActive"/> finds the same aura for a name.
    /// </summary>
    internal int Membership { get; private set; }

    /// <summary>Applies an engine-internal aura built inline (not a game effect).</summary>
    public void Apply(Aura aura, SimTime now = default, int? explicitStacks = null) =>
        Apply(aura.Def, now, aura.Stacks, explicitStacks);

    public bool HasAura(AuraDef def, SimTime now = default)
    {
        CleanExpired(now);
        return Find(def) != null;
    }

    public int GetStacks(AuraDef def, SimTime now = default)
    {
        CleanExpired(now);
        return Find(def)?.Stacks ?? 0;
    }

    public void Remove(AuraDef def, SimTime now = default) => Remove(def.Key, now);
    public void ConsumeStack(AuraDef def, SimTime now = default, int count = 1) => ConsumeStack(def.Key, now, count);
    public void Extend(AuraDef def, SimTime duration) => Extend(def.Key, duration);

    public Aura? Get(AuraDef def, SimTime now = default)
    {
        CleanExpired(now);
        return Find(def);
    }

    // Speed: the auras of _auras by their key's slot too, so a read by definition hashes no string.
    private Aura?[] _bySlot = [];

    /// <summary>The aura of <paramref name="def"/>'s key, run out or not.</summary>
    private Aura? Find(AuraDef def)
    {
        int slot = def.Slot;
        return slot < _bySlot.Length ? _bySlot[slot] : null;
    }

    private void Place(int slot, Aura? aura)
    {
        if (slot >= _bySlot.Length)
        {
            if (aura == null) return;
            Array.Resize(ref _bySlot, Math.Max(slot + 1, AuraSlots.Count));
        }
        _bySlot[slot] = aura;
    }

    public Aura? Get(string key, SimTime now = default)
    {
        CleanExpired(now);
        return _auras.TryGetValue(key, out var aura) ? aura : null;
    }

    public void Extend(string key, SimTime duration)
    {
        if (_auras.TryGetValue(key, out var aura) && aura.ExpiresAt != SimTime.Infinite)
        {
            aura.ExpiresAt += duration;
            ExpiryChanged(aura.ExpiresAt);
        }
    }

    public void ConsumeStack(string key, SimTime now = default, int count = 1)
    {
        CleanExpired(now);
        if (_auras.TryGetValue(key, out var aura))
        {
            if (aura.Stacks > count)
            {
                int before = aura.Stacks;
                aura.Stacks -= count;
                StatsChanged(aura.Def.StatMask);
                OnStacksLost?.Invoke(aura, before, now);
            }
            else Remove(key, now);
        }
    }

    public void Remove(string key, SimTime now = default)
    {
        if (_auras.Remove(key, out var aura))
        {
            Place(aura.Def.Slot, null);
            Membership++;
            StatsChanged(aura.Def.StatMask);
            if (_activeSince.Remove(key, out var start))
            {
                var endTime = aura.ExpiresAt != SimTime.Infinite && aura.ExpiresAt < now ? aura.ExpiresAt : now;
                long dur = Math.Max(0, (endTime - start).Milliseconds);
                _uptimeMs[key] = _uptimeMs.GetValueOrDefault(key, 0L) + dur;
            }
            OnRemoved?.Invoke(aura, now);
            NotifyTimers(aura);
        }
    }

    public bool HasAura(string key, SimTime now = default)
    {
        CleanExpired(now);
        return _auras.ContainsKey(key);
    }

    public int GetStacks(string key, SimTime now = default)
    {
        CleanExpired(now);
        return _auras.TryGetValue(key, out var aura) ? aura.Stacks : 0;
    }

    /// <summary>The active auras. A <c>foreach</c> over them allocates no enumerator.</summary>
    public Dictionary<string, Aura>.ValueCollection Active(SimTime now = default)
    {
        CleanExpired(now);
        return _auras.Values;
    }

    /// <summary>The first active aura whose action-list name or key is <paramref name="slug"/>, or null.</summary>
    public Aura? FindActive(string slug, SimTime now)
    {
        CleanExpired(now);
        foreach (var aura in _auras.Values)
        {
            if (aura.AplSlug == slug || aura.Key == slug) return aura;
        }
        return null;
    }

    public Dictionary<string, int> GetActiveAuraStacks(SimTime now = default)
    {
        CleanExpired(now);
        return _auras.ToDictionary(kv => kv.Key, kv => kv.Value.Stacks, StringComparer.Ordinal);
    }

    /// <summary>Removes the auras run out at <paramref name="now"/>.</summary>
    // Speed: every read of the auras makes this check, inlined; the scan runs only once an aura may have run out.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CleanExpired(SimTime now)
    {
        if (now == default || now < _nextExpiry) return;
        RunOut(now);
    }

    private void RunOut(SimTime now)
    {
        // Speed: the list of run-out auras is kept for the next scan, unless a removal's handlers start a scan of their own.
        var expired = _expired ?? new List<Aura>();
        _expired = null;
        var next = SimTime.Infinite;
        foreach (var aura in _auras.Values)
        {
            if (aura.IsExpired(now))
            {
                expired.Add(aura);
            }
            else if (aura.ExpiresAt < next)
            {
                next = aura.ExpiresAt;
            }
        }
        _nextExpiry = next;
        QueueExpiry();
        foreach (var aura in expired)
        {
            if (aura.Def.ExpiryRemovesOneStack && aura.Stacks > 1) LoseStackOnExpiry(aura);
            else Remove(aura.Key, aura.ExpiresAt);
        }
        expired.Clear();
        _expired = expired;
    }

    /// <summary>The aura's duration ran out: it loses a stack, and its duration restarts from the expiry.</summary>
    private void LoseStackOnExpiry(Aura aura)
    {
        var at = aura.ExpiresAt;
        int before = aura.Stacks;
        aura.Stacks--;
        aura.ExpiresAt = at + aura.Duration + SimTime.FromSeconds(ExpiryLagSeconds);
        StatsChanged(aura.Def.StatMask);
        ExpiryChanged(aura.ExpiresAt);
        OnGained?.Invoke(aura, before, at);
        NotifyTimers(aura);
    }

    private List<Aura>? _expired;

    /// <summary>Applies every active modifier of <paramref name="type"/> to <paramref name="baseValue"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Fold(StatType type, double baseValue, SimTime now = default)
    {
        CleanExpired(now);
        int t = (int)type;
        if (_foldVersions[t] != _statVersions[t]) Refold(type);
        return _folds[t].Apply(baseValue);
    }

    private void Refold(StatType type)
    {
        int t = (int)type;
        int stat = AuraDef.MaskOf(type);
        var fold = new ModifierFold();
        foreach (var aura in _auras.Values)
        {
            // An aura with no modifier of the stat adds nothing to its fold.
            if (!aura.Def.Changes(stat)) continue;
            var mods = aura.Modifiers;
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].Type == type) fold.Accept(mods[i], aura.Stacks);
            }
        }
        _folds[t] = fold;
        _foldVersions[t] = _statVersions[t];
    }

    /// <summary>
    /// Sum of (value - 1) over every active modifier of <paramref name="type"/>, ignoring stacks and how it combines (how
    /// cooldown recovery multipliers add up).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double SumBonuses(StatType type, SimTime now = default)
    {
        CleanExpired(now);
        int t = (int)type;
        if (_bonusVersions[t] != _statVersions[t]) Resum(type);
        return _bonuses[t];
    }

    private void Resum(StatType type)
    {
        int t = (int)type;
        int stat = AuraDef.MaskOf(type);
        double sum = 0.0;
        foreach (var aura in _auras.Values)
        {
            if (!aura.Def.Changes(stat)) continue;
            var mods = aura.Modifiers;
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].Type == type) sum += mods[i].Value - 1.0;
            }
        }
        _bonuses[t] = sum;
        _bonusVersions[t] = _statVersions[t];
    }

    /// <summary>Sum of additive modifiers of <paramref name="type"/>.</summary>
    public double GetTotalModifier(StatType type, SimTime now = default) => Fold(type, 0.0, now);

    public void ClearAll()
    {
        _auras.Clear();
        Array.Clear(_bySlot);
        Membership++;
        _activeSince.Clear();
        for (int t = 0; t < StatTypes; t++) _statVersions[t]++;
        _nextExpiry = SimTime.Infinite;
        _expiry?.Cancel();
        _expiry = null;
    }

    public void FinalizeUptimes(SimTime combatEndTime)
    {
        foreach (var (key, start) in _activeSince)
        {
            long dur = Math.Max(0, (combatEndTime - start).Milliseconds);
            _uptimeMs[key] = _uptimeMs.GetValueOrDefault(key, 0L) + dur;
        }
        _activeSince.Clear();
    }

    public double GetUptimePercent(string key, SimTime totalCombatDuration)
    {
        if (totalCombatDuration.Milliseconds <= 0) return 0.0;
        return (double)_uptimeMs.GetValueOrDefault(key, 0L) / totalCombatDuration.Milliseconds * 100.0;
    }
}
