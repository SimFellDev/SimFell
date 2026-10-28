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
    /// <summary>The fights (the run's first, by seed) whose aura spans are kept, for one typical fight's timeline.</summary>
    public const int SpanFights = 100;

    private Tally? _tally;
    private int _fight;
    private double[] _damage = [];
    // Speed: hits of one ability come in runs, so the last source is kept; active auras are found by the aura itself.
    private string? _lastName;
    private SourceTally? _lastSource;
    private readonly Dictionary<Aura, AuraState> _active = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(AuraContainer Owner, string Key), SimTime> _lastEnd = new();
    /// <summary>This fight's aura spans (start and end seconds in turn), by name and side; null for a fight not sampled.</summary>
    private Dictionary<(string Name, bool OnTarget), List<double>>? _spans;
    /// <summary>This fight's casts in order; null for a fight not sampled.</summary>
    private List<CastReport>? _casts;
    private readonly Hero _hero;
    /// <summary>This fight's Spirit Points at the start of each second, filled up to <see cref="_spiritFilled"/>.</summary>
    private double[] _spirit = [];
    private int _spiritFilled;
    private double _lastSpirit;
    private double _spiritMax;

    public FightRecorder(Hero hero)
    {
        _hero = hero;
        hero.OnResourceGained += (resource, _, _, ctx) =>
        {
            if (resource == Hero.SpiritPoints) SpiritChanged(ctx.CurrentTime);
        };
        hero.OnResourceSpent += (resource, _, now) =>
        {
            if (resource == Hero.SpiritPoints) SpiritChanged(now);
        };
        hero.OnDamageDealt += OnHit;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (_tally == null) return;
            _tally.Source(ability.Name).Casts++;
            _casts?.Add(new CastReport(now.TotalSeconds, ability.Name, ability.Id));
            // A Spirit ability takes its Spirit Points from the pool with no event of its own.
            if (_hero.ResourceAmount(Hero.SpiritPoints) != _lastSpirit) SpiritChanged(now);
        };
        hero.SpiritOfHeroismStarted += ctx =>
        {
            if (_hero.ResourceAmount(Hero.SpiritPoints) != _lastSpirit) SpiritChanged(ctx.CurrentTime);
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
        _spans = fight < SpanFights ? new() : null;
        _casts = fight < SpanFights ? new() : null;
        if (_spirit.Length == seconds) Array.Clear(_spirit);
        else _spirit = new double[seconds];
        _spiritFilled = 0;
        _lastSpirit = _hero.ResourceAmount(Hero.SpiritPoints);
        _spiritMax = _hero.Resources.Get(Hero.SpiritPoints).Max;
    }

    /// <summary>
    /// Spirit changed at <paramref name="now"/>: each second that started before then had the amount before the change (a
    /// second starting at that moment has the new one).
    /// </summary>
    private void SpiritChanged(SimTime now)
    {
        if (_tally == null) return;
        FillSpirit(Math.Min(_spirit.Length, (int)Math.Ceiling(now.TotalSeconds)));
        _lastSpirit = _hero.ResourceAmount(Hero.SpiritPoints);
    }

    private void FillSpirit(int upTo)
    {
        for (; _spiritFilled < upTo; _spiritFilled++) _spirit[_spiritFilled] = _lastSpirit;
    }

    /// <summary>Listens to the debuffs on the fight's targets.</summary>
    public void WatchTargets(SimulationContext ctx)
    {
        foreach (var enemy in ctx.AllEnemies) Watch(enemy.Auras, onTarget: true);
        _primary = ctx.AllEnemies.FirstOrDefault();
    }

    /// <summary>The fight's first target, whose damage is also added up on its own.</summary>
    private EnemyTarget? _primary;

    /// <summary>Ends the fight at <paramref name="duration"/>: the auras still up count until then (or their expiry).</summary>
    public void End(SimTime duration)
    {
        var tally = _tally!;
        foreach (var state in _active.Values) Close(state, Earlier(state.Aura.ExpiresAt, duration));
        _active.Clear();
        int seconds = Math.Min(_spirit.Length, (int)Math.Ceiling(duration.TotalSeconds));
        FillSpirit(seconds);
        tally.AddSpirit(_spirit, seconds, _spiritMax);
        tally.AddFight(_fight, _damage, _spans, _casts, _spans != null ? _spirit[..seconds] : null);
        _tally = null;
    }

    private void OnHit(in DamageContext hit)
    {
        if (_tally == null) return;
        int second = Math.Clamp((int)hit.Timestamp.TotalSeconds, 0, _damage.Length - 1);
        _damage[second] += hit.Amount;
        if (ReferenceEquals(hit.Target, _primary)) _tally.PrimaryDamage += hit.Amount;
        if (!ReferenceEquals(hit.AbilityName, _lastName))
        {
            _lastName = hit.AbilityName;
            _lastSource = _tally.Source(hit.AbilityName);
        }
        var source = _lastSource!;
        if (source.Id == 0) source.Id = hit.AbilityId;
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
        if (tally.LogId == 0) tally.LogId = aura.LogId;
        tally.MaxStacks = Math.Max(tally.MaxStacks, aura.Stacks);
        tally.StackCap = Math.Max(tally.StackCap, aura.MaxStacks);
        tally.Applications++;
        if (_lastEnd.TryGetValue(key, out var lastEnd))
        {
            tally.Gaps++;
            tally.GapSeconds += (now - lastEnd).TotalSeconds;
        }
        _active[aura] = new AuraState(owner, onTarget, aura, tally) { Start = now, Changed = now, Stacks = aura.Stacks };
    }

    private static void Restack(AuraState state, Aura aura, SimTime now)
    {
        Advance(state, now);
        state.Stacks = aura.Stacks;
    }

    private void Close(AuraState state, SimTime end)
    {
        Advance(state, end);
        state.Tally.UpSeconds += (end - state.Start).TotalSeconds;
        if (_spans == null) return;
        var key = (state.Aura.Name, state.OnTarget);
        if (!_spans.TryGetValue(key, out var spans)) _spans[key] = spans = [];
        spans.Add(state.Start.TotalSeconds);
        spans.Add(end.TotalSeconds);
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

    private sealed class AuraState(AuraContainer owner, bool onTarget, Aura aura, AuraTally tally)
    {
        public AuraContainer Owner { get; } = owner;
        public bool OnTarget { get; } = onTarget;
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
        /// <summary>The damage log ID of the first hit.</summary>
        public int Id;

        public void Add(SourceTally other)
        {
            if (Id == 0) Id = other.Id;
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
        public int MaxStacks, StackCap, LogId;

        public void Add(AuraTally other)
        {
            if (LogId == 0) LogId = other.LogId;
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
        /// <summary>The sampled fights' total damage and aura spans.</summary>
        public readonly List<(int Fight, double Damage, Dictionary<(string Name, bool OnTarget), List<double>> Spans, List<CastReport> Casts, double[] Spirit)> Samples = [];
        /// <summary>Spirit Points at the start of each second, added up over the fights still going then, and their count.</summary>
        public double[] SpiritSum = [];
        public int[] SpiritFights = [];
        public double SpiritMax;

        /// <summary>Adds a fight's Spirit Points for its first <paramref name="seconds"/> seconds.</summary>
        public void AddSpirit(double[] spirit, int seconds, double max)
        {
            Grow(spirit.Length);
            for (int i = 0; i < seconds; i++)
            {
                SpiritSum[i] += spirit[i];
                SpiritFights[i]++;
            }
            SpiritMax = Math.Max(SpiritMax, max);
        }

        private void Grow(int seconds)
        {
            if (SpiritSum.Length >= seconds) return;
            Array.Resize(ref SpiritSum, seconds);
            Array.Resize(ref SpiritFights, seconds);
        }

        /// <summary>The run's Spirit Points over the fight: the mean, and the typical fight's.</summary>
        public SpiritTimeline Spirit()
        {
            int seconds = SpiritFights.Count(n => n > 0);
            var mean = Enumerable.Range(0, seconds).Select(i => SpiritSum[i] / SpiritFights[i]).ToList();
            return new SpiritTimeline(mean, Typical()?.Spirit ?? [], SpiritMax);
        }
        public readonly Dictionary<string, SourceTally> Sources = new(StringComparer.Ordinal);
        public readonly Dictionary<(string Name, bool OnTarget), AuraTally> Auras = new();
        /// <summary>The damage to each fight's first target, added up.</summary>
        public double PrimaryDamage;

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

        public void AddFight(int fight, double[] damage, Dictionary<(string Name, bool OnTarget), List<double>>? spans, List<CastReport>? casts,
            double[]? spirit)
        {
            Fights++;
            if (spans != null) Samples.Add((fight, damage.Sum(), spans, casts ?? [], spirit ?? []));
            if (DamageSum.Length != damage.Length) DamageSum = new double[damage.Length];
            for (int i = 0; i < damage.Length; i++) DamageSum[i] += damage[i];
            if (fight < BandFights) Band.Add((fight, Array.ConvertAll(damage, d => (float)d)));
        }

        public void Merge(Tally other)
        {
            if (other.Fights == 0) return;
            Fights += other.Fights;
            PrimaryDamage += other.PrimaryDamage;
            if (DamageSum.Length == 0) DamageSum = new double[other.DamageSum.Length];
            for (int i = 0; i < other.DamageSum.Length; i++) DamageSum[i] += other.DamageSum[i];
            Band.AddRange(other.Band);
            Samples.AddRange(other.Samples);
            Grow(other.SpiritSum.Length);
            for (int i = 0; i < other.SpiritSum.Length; i++)
            {
                SpiritSum[i] += other.SpiritSum[i];
                SpiritFights[i] += other.SpiritFights[i];
            }
            SpiritMax = Math.Max(SpiritMax, other.SpiritMax);
            foreach (var (name, source) in other.Sources) Source(name).Add(source);
            foreach (var ((name, onTarget), aura) in other.Auras) Aura(name, onTarget).Add(aura);
        }

        /// <summary>These totals in a chunk's form (<see cref="MonteCarloChunk.Report"/>).</summary>
        public MonteCarloChunk.ReportTotals ToTotals() => new()
        {
            Fights = Fights,
            DamageSum = DamageSum,
            Band = Band.Select(b => new MonteCarloChunk.BandFight(b.Fight, b.Damage)).ToList(),
            Samples = Samples.Select(s => new MonteCarloChunk.SampleFight(s.Fight, s.Damage,
                s.Spans.Select(a => new MonteCarloChunk.AuraSpans(a.Key.Name, a.Key.OnTarget, a.Value)).ToList(), s.Casts, s.Spirit)).ToList(),
            SpiritSum = SpiritSum,
            SpiritFights = SpiritFights,
            SpiritMax = SpiritMax,
            Sources = Sources.Select(s => new MonteCarloChunk.SourceTotals(s.Key, s.Value.Damage, s.Value.Crits, s.Value.Casts, s.Value.Hits, s.Value.Id)).ToList(),
            Auras = Auras.Select(a => new MonteCarloChunk.AuraTotals(a.Key.Name, a.Key.OnTarget, a.Value.UpSeconds, a.Value.StackSeconds,
                a.Value.AtMaxSeconds, a.Value.GapSeconds, a.Value.Applications, a.Value.Refreshes, a.Value.Gaps, a.Value.MaxStacks,
                a.Value.StackCap, a.Value.LogId)).ToList(),
            PrimaryDamage = PrimaryDamage,
        };

        /// <summary>The totals of a chunk's form.</summary>
        public static Tally From(MonteCarloChunk.ReportTotals totals)
        {
            var tally = new Tally
            {
                Fights = totals.Fights,
                DamageSum = totals.DamageSum,
                SpiritSum = totals.SpiritSum,
                SpiritFights = totals.SpiritFights,
                SpiritMax = totals.SpiritMax,
                PrimaryDamage = totals.PrimaryDamage,
            };
            tally.Band.AddRange(totals.Band.Select(b => (b.Fight, b.Damage)));
            tally.Samples.AddRange(totals.Samples.Select(s => (s.Fight, s.Damage,
                s.Spans.ToDictionary(a => (a.Name, a.OnTarget), a => a.Spans), s.Casts, s.Spirit)));
            foreach (var s in totals.Sources)
            {
                tally.Sources[s.Name] = new SourceTally { Damage = s.Damage, Crits = s.Crits, Casts = s.Casts, Hits = s.Hits, Id = s.Id };
            }
            foreach (var a in totals.Auras)
            {
                tally.Auras[(a.Name, a.OnTarget)] = new AuraTally
                {
                    UpSeconds = a.UpSeconds, StackSeconds = a.StackSeconds, AtMaxSeconds = a.AtMaxSeconds, GapSeconds = a.GapSeconds,
                    Applications = a.Applications, Refreshes = a.Refreshes, Gaps = a.Gaps, MaxStacks = a.MaxStacks,
                    StackCap = a.StackCap, LogId = a.LogId,
                };
            }
            return tally;
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
                    t.Hits > 0 ? t.Crits / t.Hits : 0, t.Hits > 0 ? t.Damage / t.Hits : 0, t.Id);
            })
            .ToList();

        /// <summary>
        /// Each buff and debuff, buffs first, longest up first. An aura up the whole of every fight from its single
        /// application (a passive) is left out, and so is an engine-internal one (no log ID, such as a marker on a target).
        /// </summary>
        public List<BuffReport> Buffs(double seconds, int targets)
        {
            var typical = Typical()?.Spans;
            return Auras
            .Where(a => a.Value.LogId != 0)
            .Select(a =>
            {
                var (name, onTarget) = a.Key;
                var t = a.Value;
                double fights = Math.Max(1, Fights);
                double span = fights * seconds * (onTarget ? Math.Max(1, targets) : 1);
                return new BuffReport(name, onTarget, t.UpSeconds / span, t.Applications / fights, t.Refreshes / fights,
                    t.Applications > 0 ? t.UpSeconds / t.Applications : 0, t.UpSeconds > 0 ? t.StackSeconds / t.UpSeconds : 0,
                    t.MaxStacks, t.StackCap > 1 ? t.AtMaxSeconds / span : 0, t.Gaps > 0 ? t.GapSeconds / t.Gaps : 0, t.LogId,
                    typical != null && typical.TryGetValue(a.Key, out var spans) ? Union(spans) : []);
            })
            .Where(b => !(b.Uptime >= 0.999 && b.Applications <= 1.0 && b.Refreshes == 0))
            .OrderBy(b => b.OnTarget)
            .ThenByDescending(b => b.Uptime)
            .ToList();
        }

        /// <summary>The typical fight's casts, in order.</summary>
        public List<CastReport> Casts() => Typical()?.Casts ?? [];

        /// <summary>The typical fight: the sampled one whose damage is nearest the mean (the first such by seed).</summary>
        private (int Fight, double Damage, Dictionary<(string Name, bool OnTarget), List<double>> Spans, List<CastReport> Casts, double[] Spirit)? Typical()
        {
            if (Samples.Count == 0) return null;
            double mean = Fights == 0 ? 0 : DamageSum.Sum() / Fights;
            return Samples.OrderBy(s => Math.Abs(s.Damage - mean)).ThenBy(s => s.Fight).First();
        }

        /// <summary>Spans (start and end in turn) merged where they overlap or touch, in time order: a debuff on several targets.</summary>
        private static List<double> Union(List<double> spans)
        {
            var ordered = Enumerable.Range(0, spans.Count / 2).Select(i => (Start: spans[2 * i], End: spans[2 * i + 1])).OrderBy(s => s.Start);
            var merged = new List<double>();
            foreach (var (start, end) in ordered)
            {
                if (merged.Count > 0 && start <= merged[^1]) merged[^1] = Math.Max(merged[^1], end);
                else
                {
                    merged.Add(start);
                    merged.Add(end);
                }
            }
            return merged;
        }

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
