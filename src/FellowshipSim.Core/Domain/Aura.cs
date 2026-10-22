namespace FellowshipSim.Core.Domain;

/// <summary>
/// The definition of an aura (buff/debuff): identity, duration, stacking and stat modifiers.
/// <see cref="LogId"/> is the ID combat logs use for it (effect ID + 1,000,000), or 0 for engine-internal auras.
/// </summary>
public sealed record AuraDef(string Name, int LogId, SimTime Duration, int MaxStacks, IReadOnlyList<StatModifier> Modifiers)
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

    public string Key { get; private init; } = KeyOf(Name, LogId, 0);

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

    /// <summary>Every aura declared as a <c>public static readonly AuraDef</c> on <paramref name="types"/> or their base classes.</summary>
    public static IEnumerable<AuraDef> DeclaredOn(IEnumerable<Type> types) =>
        types.Distinct()
            .SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy))
            .Where(f => f.FieldType == typeof(AuraDef))
            .Select(f => (AuraDef)f.GetValue(null)!);
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
    public int LogId => Def.LogId;
    public string Key => Def.Key;
    public SimTime Duration => Def.Duration;
    public int MaxStacks => Def.MaxStacks;
    public IReadOnlyList<StatModifier> Modifiers => Def.Modifiers;

    public SimTime AppliedAt { get; internal set; } = SimTime.Zero;
    // Only AuraContainer changes these, so it can cache its folds and next expiry.
    public SimTime ExpiresAt { get; internal set; } = SimTime.Infinite;
    public int Stacks { get; internal set; } = 1;

    public bool IsExpired(SimTime now) => now >= ExpiresAt;
}

public class AuraContainer
{
    private readonly Dictionary<string, Aura> _auras = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _uptimeMs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimTime> _activeSince = new(StringComparer.Ordinal);

    // Speed: _version changes whenever an aura or its stacks change, which invalidates the cached folds; _nextExpiry is
    // never later than the earliest expiry, so CleanExpired only looks at the auras once one may have run out.
    private int _version;
    private SimTime _nextExpiry = SimTime.Infinite;
    private readonly ModifierFold[] _folds = new ModifierFold[Enum.GetValues<StatType>().Length];
    private readonly int[] _foldVersions = Enumerable.Repeat(-1, Enum.GetValues<StatType>().Length).ToArray();
    private readonly double[] _bonuses = new double[Enum.GetValues<StatType>().Length];
    private readonly int[] _bonusVersions = Enumerable.Repeat(-1, Enum.GetValues<StatType>().Length).ToArray();

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

    /// <summary>Raised whenever an aura is applied, stacked or refreshed (aura, stacks before, time).</summary>
    public event Action<Aura, int, SimTime>? OnGained;

    /// <summary>Raised when an aura is removed or expires.</summary>
    public event Action<Aura, SimTime>? OnRemoved;

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
        if (_auras.TryGetValue(def.Key, out var existing))
        {
            int before = existing.Stacks;
            var previousExpiry = existing.ExpiresAt;
            if (!ReferenceEquals(existing.Def, def) && existing.Def != def)
            {
                // Re-applied with different values (e.g. a bonus recomputed from current stats): the new values win.
                existing = new Aura(def) { AppliedAt = existing.AppliedAt, Stacks = existing.Stacks };
                _auras[def.Key] = existing;
            }
            if (def.ReapplyExtendsSeconds > 0 && expires != SimTime.Infinite && previousExpiry > now)
            {
                expires = previousExpiry + SimTime.FromSeconds(def.ReapplyExtendsSeconds);
            }
            else if (def.RefreshCarryoverFraction > 0 && expires != SimTime.Infinite && previousExpiry > now)
            {
                double left = (previousExpiry - now).TotalSeconds;
                expires += SimTime.FromSeconds(Math.Min(left, def.Duration.TotalSeconds * def.RefreshCarryoverFraction));
            }
            existing.ExpiresAt = expires;
            existing.Stacks = Math.Clamp(setStacks ?? (existing.Stacks + stacks), 1, existing.MaxStacks);
            Changed(expires);
            OnGained?.Invoke(existing, before, now);
            NotifyTimers(existing);
            return existing;
        }

        var aura = new Aura(def)
        {
            AppliedAt = now,
            ExpiresAt = expires,
            Stacks = Math.Clamp(setStacks ?? stacks, 1, def.MaxStacks)
        };
        _auras[def.Key] = aura;
        _activeSince[def.Key] = now;
        Changed(expires);
        OnGained?.Invoke(aura, 0, now);
        NotifyTimers(aura);
        return aura;
    }

    /// <summary>An aura was added or changed: cached folds are stale, and it may now be the next to run out.</summary>
    private void Changed(SimTime expires)
    {
        _version++;
        if (expires < _nextExpiry) _nextExpiry = expires;
    }

    /// <summary>Applies an engine-internal aura built inline (not a game effect).</summary>
    public void Apply(Aura aura, SimTime now = default, int? explicitStacks = null) =>
        Apply(aura.Def, now, aura.Stacks, explicitStacks);

    public bool HasAura(AuraDef def, SimTime now = default) => HasAura(def.Key, now);
    public int GetStacks(AuraDef def, SimTime now = default) => GetStacks(def.Key, now);
    public void Remove(AuraDef def, SimTime now = default) => Remove(def.Key, now);
    public void ConsumeStack(AuraDef def, SimTime now = default, int count = 1) => ConsumeStack(def.Key, now, count);
    public void Extend(AuraDef def, SimTime duration) => Extend(def.Key, duration);
    public Aura? Get(AuraDef def, SimTime now = default) => Get(def.Key, now);

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
            Changed(aura.ExpiresAt);
        }
    }

    public void ConsumeStack(string key, SimTime now = default, int count = 1)
    {
        CleanExpired(now);
        if (_auras.TryGetValue(key, out var aura))
        {
            if (aura.Stacks > count)
            {
                aura.Stacks -= count;
                _version++;
            }
            else Remove(key, now);
        }
    }

    public void Remove(string key, SimTime now = default)
    {
        if (_auras.Remove(key, out var aura))
        {
            _version++;
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

    public IEnumerable<Aura> Active(SimTime now = default)
    {
        CleanExpired(now);
        return _auras.Values;
    }

    public Dictionary<string, int> GetActiveAuraStacks(SimTime now = default)
    {
        CleanExpired(now);
        return _auras.ToDictionary(kv => kv.Key, kv => kv.Value.Stacks, StringComparer.Ordinal);
    }

    public void CleanExpired(SimTime now)
    {
        if (now == default || now < _nextExpiry) return;
        List<Aura>? expired = null;
        var next = SimTime.Infinite;
        foreach (var aura in _auras.Values)
        {
            if (aura.IsExpired(now))
            {
                (expired ??= new()).Add(aura);
            }
            else if (aura.ExpiresAt < next)
            {
                next = aura.ExpiresAt;
            }
        }
        _nextExpiry = next;
        if (expired == null) return;
        foreach (var aura in expired)
        {
            Remove(aura.Key, aura.ExpiresAt);
        }
    }

    /// <summary>The earliest time after <paramref name="now"/> an active aura runs out (Infinite if none does).</summary>
    public SimTime NextExpiry(SimTime now)
    {
        var next = SimTime.Infinite;
        foreach (var aura in _auras.Values)
        {
            if (aura.ExpiresAt > now && aura.ExpiresAt < next) next = aura.ExpiresAt;
        }
        return next;
    }

    /// <summary>Applies every active modifier of <paramref name="type"/> to <paramref name="baseValue"/>.</summary>
    public double Fold(StatType type, double baseValue, SimTime now = default)
    {
        CleanExpired(now);
        int t = (int)type;
        if (_foldVersions[t] != _version)
        {
            var fold = new ModifierFold();
            foreach (var aura in _auras.Values)
            {
                var mods = aura.Modifiers;
                for (int i = 0; i < mods.Count; i++)
                {
                    if (mods[i].Type == type) fold.Accept(mods[i], aura.Stacks);
                }
            }
            _folds[t] = fold;
            _foldVersions[t] = _version;
        }
        return _folds[t].Apply(baseValue);
    }

    /// <summary>
    /// Sum of (value - 1) over every active modifier of <paramref name="type"/>, ignoring stacks and how it combines (how
    /// cooldown recovery multipliers add up).
    /// </summary>
    public double SumBonuses(StatType type, SimTime now = default)
    {
        CleanExpired(now);
        int t = (int)type;
        if (_bonusVersions[t] != _version)
        {
            double sum = 0.0;
            foreach (var aura in _auras.Values)
            {
                var mods = aura.Modifiers;
                for (int i = 0; i < mods.Count; i++)
                {
                    if (mods[i].Type == type) sum += mods[i].Value - 1.0;
                }
            }
            _bonuses[t] = sum;
            _bonusVersions[t] = _version;
        }
        return _bonuses[t];
    }

    /// <summary>Sum of additive modifiers of <paramref name="type"/>.</summary>
    public double GetTotalModifier(StatType type, SimTime now = default) => Fold(type, 0.0, now);

    public void ClearAll()
    {
        _auras.Clear();
        _activeSince.Clear();
        _version++;
        _nextExpiry = SimTime.Infinite;
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
