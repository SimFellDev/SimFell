namespace FellowshipSim.Core.Engine;

/// <summary>
/// What a sim thread's fights did, for the run's report (<see cref="SimTimeline"/>, <see cref="AbilityReport"/>,
/// <see cref="BuffReport"/>). It listens to one hero's hits, casts and auras, and to each fight's targets' auras, and adds
/// each fight to a <see cref="Tally"/> (one per thread, merged when the run ends).
/// </summary>
internal sealed class FightRecorder
{
    /// <summary>The fights (the run's first, by seed) whose damage per second is kept for the 5-95% band.</summary>
    public const int BandFights = 1000;
    /// <summary>The window of the DPS timeline's rolling average.</summary>
    public const double RollingSeconds = 10.0;

    private Tally? _tally;
    private int _fight;
    private double[] _damage = [];
    // Speed: hits of one ability come in runs, so the last source is kept; active auras are found by the aura itself.
    private string? _lastName;
    private SourceTally? _lastSource;
    private readonly Dictionary<Aura, AuraState> _active = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(AuraContainer Owner, string Key), SimTime> _lastEnd = new();

    public FightRecorder(Hero hero)
    {
        hero.OnDamageDealt += OnHit;
        hero.OnCastCompleted += (ability, _, _) =>
        {
            if (_tally != null) _tally.Source(ability.Name).Casts++;
        };
        Watch(hero.Auras, onTarget: false);
    }

    /// <summary>Starts fight <paramref name="fight"/> (its index in the run), <paramref name="seconds"/> long, added to <paramref name="tally"/>.</summary>
    public void Begin(int fight, int seconds, Tally tally)
    {
        _tally = tally;
        _fight = fight;
        _lastName = null;
        if (_damage.Length == seconds) Array.Clear(_damage);
        else _damage = new double[seconds];
        _active.Clear();
        _lastEnd.Clear();
    }

    /// <summary>Listens to the debuffs on the fight's targets.</summary>
    public void WatchTargets(SimulationContext ctx)
    {
        foreach (var enemy in ctx.AllEnemies) Watch(enemy.Auras, onTarget: true);
    }

    /// <summary>Ends the fight at <paramref name="duration"/>: the auras still up count until then (or their expiry).</summary>
    public void End(SimTime duration)
    {
        var tally = _tally!;
        foreach (var state in _active.Values) Close(state, Earlier(state.Aura.ExpiresAt, duration));
        _active.Clear();
        tally.AddFight(_fight, _damage);
        _tally = null;
    }

    private void OnHit(in DamageContext hit)
    {
        if (_tally == null) return;
        int second = Math.Clamp((int)hit.Timestamp.TotalSeconds, 0, _damage.Length - 1);
        _damage[second] += hit.Amount;
        if (!ReferenceEquals(hit.AbilityName, _lastName))
        {
            _lastName = hit.AbilityName;
            _lastSource = _tally.Source(hit.AbilityName);
        }
        var source = _lastSource!;
        source.Damage += hit.Amount;
        source.Hits++;
        source.Crits += hit.CritChance >= 0 ? hit.CritChance : hit.IsCrit ? 1.0 : 0.0;
    }

    private void Watch(AuraContainer auras, bool onTarget)
    {
        auras.OnGained += (aura, _, now) => Gained(auras, onTarget, aura, now);
        auras.OnStacksLost += (aura, _, now) =>
        {
            if (_tally != null && Active(auras, aura) is { } state) Restack(state, aura, now);
        };
        auras.OnRemoved += (aura, now) =>
        {
            if (_tally == null || Active(auras, aura) is not { } state) return;
            _active.Remove(state.Aura);
            var end = Earlier(aura.ExpiresAt, now);
            Close(state, end);
            _lastEnd[(auras, aura.Key)] = end;
        };
    }

    /// <summary>
    /// The active aura's state. An aura re-applied with other values is a new <see cref="Aura"/> of the same key: its
    /// state carries over to it.
    /// </summary>
    private AuraState? Active(AuraContainer owner, Aura aura)
    {
        if (_active.TryGetValue(aura, out var state)) return state;
        foreach (var other in _active.Values)
        {
            if (other.Owner != owner || other.Aura.Key != aura.Key) continue;
            _active.Remove(other.Aura);
            other.Aura = aura;
            _active[aura] = other;
            return other;
        }
        return null;
    }

    private void Gained(AuraContainer owner, bool onTarget, Aura aura, SimTime now)
    {
        if (_tally == null) return;
        if (Active(owner, aura) is { } state)
        {
            state.Tally.Refreshes++;
            state.Tally.MaxStacks = Math.Max(state.Tally.MaxStacks, aura.Stacks);
            Restack(state, aura, now);
            return;
        }
        var key = (owner, aura.Key);
        var tally = _tally.Aura(aura.Name, onTarget);
        tally.MaxStacks = Math.Max(tally.MaxStacks, aura.Stacks);
        tally.StackCap = Math.Max(tally.StackCap, aura.MaxStacks);
        tally.Applications++;
        if (_lastEnd.TryGetValue(key, out var lastEnd))
        {
            tally.Gaps++;
            tally.GapSeconds += (now - lastEnd).TotalSeconds;
        }
        _active[aura] = new AuraState(owner, aura, tally) { Start = now, Changed = now, Stacks = aura.Stacks };
    }

    private static void Restack(AuraState state, Aura aura, SimTime now)
    {
        Advance(state, now);
        state.Stacks = aura.Stacks;
    }

    private static void Close(AuraState state, SimTime end)
    {
        Advance(state, end);
        state.Tally.UpSeconds += (end - state.Start).TotalSeconds;
    }

    /// <summary>Adds the stacks held since the last change, up to <paramref name="now"/>.</summary>
    private static void Advance(AuraState state, SimTime now)
    {
        double seconds = (now - state.Changed).TotalSeconds;
        state.Tally.StackSeconds += state.Stacks * seconds;
        if (state.Aura.MaxStacks > 1 && state.Stacks >= state.Aura.MaxStacks) state.Tally.AtMaxSeconds += seconds;
        state.Changed = now;
    }

    private static SimTime Earlier(SimTime a, SimTime b) => a < b ? a : b;

    private sealed class AuraState(AuraContainer owner, Aura aura, AuraTally tally)
    {
        public AuraContainer Owner { get; } = owner;
        public Aura Aura { get; set; } = aura;
        public AuraTally Tally { get; } = tally;
        public SimTime Start;
        public SimTime Changed;
        public int Stacks;
    }

    /// <summary>One damage source's totals over a thread's fights.</summary>
    internal sealed class SourceTally
    {
        /// <summary>Crits counted by each hit's chance to crit.</summary>
        public double Damage, Crits;
        public long Casts, Hits;

        public void Add(SourceTally other)
        {
            Damage += other.Damage;
            Casts += other.Casts;
            Hits += other.Hits;
            Crits += other.Crits;
        }
    }

    /// <summary>One buff's (or debuff's) totals over a thread's fights.</summary>
    internal sealed class AuraTally
    {
        public double UpSeconds, StackSeconds, AtMaxSeconds, GapSeconds;
        public long Applications, Refreshes, Gaps;
        public int MaxStacks, StackCap;

        public void Add(AuraTally other)
        {
            UpSeconds += other.UpSeconds;
            StackSeconds += other.StackSeconds;
            AtMaxSeconds += other.AtMaxSeconds;
            GapSeconds += other.GapSeconds;
            Applications += other.Applications;
            Refreshes += other.Refreshes;
            Gaps += other.Gaps;
            MaxStacks = Math.Max(MaxStacks, other.MaxStacks);
            StackCap = Math.Max(StackCap, other.StackCap);
        }
    }

    /// <summary>A thread's fights: damage per second (summed, and each of the band's fights), each source and each aura.</summary>
    internal sealed class Tally
    {
        public int Fights;
        public double[] DamageSum = [];
        public readonly List<(int Fight, float[] Damage)> Band = [];
        public readonly Dictionary<string, SourceTally> Sources = new(StringComparer.Ordinal);
        public readonly Dictionary<(string Name, bool OnTarget), AuraTally> Auras = new();

        public SourceTally Source(string name)
        {
            if (!Sources.TryGetValue(name, out var source)) Sources[name] = source = new SourceTally();
            return source;
        }

        public AuraTally Aura(string name, bool onTarget)
        {
            if (!Auras.TryGetValue((name, onTarget), out var aura)) Auras[(name, onTarget)] = aura = new AuraTally();
            return aura;
        }

        public void AddFight(int fight, double[] damage)
        {
            Fights++;
            if (DamageSum.Length != damage.Length) DamageSum = new double[damage.Length];
            for (int i = 0; i < damage.Length; i++) DamageSum[i] += damage[i];
            if (fight < BandFights) Band.Add((fight, Array.ConvertAll(damage, d => (float)d)));
        }

        public void Merge(Tally other)
        {
            if (other.Fights == 0) return;
            Fights += other.Fights;
            if (DamageSum.Length == 0) DamageSum = new double[other.DamageSum.Length];
            for (int i = 0; i < other.DamageSum.Length; i++) DamageSum[i] += other.DamageSum[i];
            Band.AddRange(other.Band);
            foreach (var (name, source) in other.Sources) Source(name).Add(source);
            foreach (var ((name, onTarget), aura) in other.Auras) Aura(name, onTarget).Add(aura);
        }

        /// <summary>The run's damage over time: the mean of every fight, and the band's 5th and 95th percentiles.</summary>
        public SimTimeline Timeline()
        {
            int seconds = DamageSum.Length;
            double[] mean = DamageSum.Select(d => Fights == 0 ? 0 : d / Fights).ToArray();
            var cumulative = Cumulative(mean);
            var rolling = Rolling(mean);
            var bandCumulative = Band.Select(b => Cumulative(Array.ConvertAll(b.Damage, d => (double)d))).ToList();
            var bandRolling = Band.Select(b => Rolling(Array.ConvertAll(b.Damage, d => (double)d))).ToList();
            return new SimTimeline(cumulative, Percentile(bandCumulative, seconds, 0.05), Percentile(bandCumulative, seconds, 0.95),
                rolling, Percentile(bandRolling, seconds, 0.05), Percentile(bandRolling, seconds, 0.95), RollingSeconds);
        }

        /// <summary>Each damage source, most damage first; values are means per fight.</summary>
        public List<AbilityReport> Abilities(double seconds) => Sources
            .Where(s => s.Value.Damage > 0)
            .OrderByDescending(s => s.Value.Damage)
            .Select(s =>
            {
                var t = s.Value;
                double fights = Math.Max(1, Fights);
                return new AbilityReport(s.Key, t.Damage / fights, t.Damage / fights / seconds, t.Casts / fights, t.Hits / fights,
                    t.Hits > 0 ? t.Crits / t.Hits : 0, t.Hits > 0 ? t.Damage / t.Hits : 0);
            })
            .ToList();

        /// <summary>
        /// Each buff and debuff, buffs first, longest up first. An aura up the whole of every fight from its single
        /// application (a passive) is left out.
        /// </summary>
        public List<BuffReport> Buffs(double seconds, int targets) => Auras
            .Select(a =>
            {
                var (name, onTarget) = a.Key;
                var t = a.Value;
                double fights = Math.Max(1, Fights);
                double span = fights * seconds * (onTarget ? Math.Max(1, targets) : 1);
                return new BuffReport(name, onTarget, t.UpSeconds / span, t.Applications / fights, t.Refreshes / fights,
                    t.Applications > 0 ? t.UpSeconds / t.Applications : 0, t.UpSeconds > 0 ? t.StackSeconds / t.UpSeconds : 0,
                    t.MaxStacks, t.StackCap > 1 ? t.AtMaxSeconds / span : 0, t.Gaps > 0 ? t.GapSeconds / t.Gaps : 0);
            })
            .Where(b => !(b.Uptime >= 0.999 && b.Applications <= 1.0 && b.Refreshes == 0))
            .OrderBy(b => b.OnTarget)
            .ThenByDescending(b => b.Uptime)
            .ToList();

        private static double[] Cumulative(double[] perSecond)
        {
            var sum = new double[perSecond.Length];
            double total = 0;
            for (int i = 0; i < perSecond.Length; i++) sum[i] = total += perSecond[i];
            return sum;
        }

        private static double[] Rolling(double[] perSecond)
        {
            int window = (int)RollingSeconds;
            var dps = new double[perSecond.Length];
            double sum = 0;
            for (int i = 0; i < perSecond.Length; i++)
            {
                sum += perSecond[i];
                if (i >= window) sum -= perSecond[i - window];
                dps[i] = sum / Math.Min(i + 1, window);
            }
            return dps;
        }

        private static double[] Percentile(List<double[]> fights, int seconds, double p)
        {
            var result = new double[seconds];
            if (fights.Count == 0) return result;
            var values = new double[fights.Count];
            int rank = (int)Math.Round(p * (fights.Count - 1));
            for (int s = 0; s < seconds; s++)
            {
                for (int f = 0; f < fights.Count; f++) values[f] = fights[f][s];
                Array.Sort(values);
                result[s] = values[rank];
            }
            return result;
        }
    }
}
