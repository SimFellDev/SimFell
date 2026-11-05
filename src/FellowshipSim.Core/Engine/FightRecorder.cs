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
    // Speed: each watched container's aura keys this fight, found by the key alone (a tuple key runs in the browser
    // build's interpreter).
    private int _generation;
    /// <summary>This fight's aura spans (start and end seconds in turn), by name and side; null for a fight not sampled.</summary>
    private Dictionary<(string Name, bool OnTarget), List<double>>? _spans;
    /// <summary>This fight's casts in the order they started; null for a fight not sampled.</summary>
    private List<CastReport>? _casts;
    /// <summary>When each cast still going started: a cast is listed at its start, once it completes.</summary>
    private readonly Dictionary<Ability, double> _castStarts = new(ReferenceEqualityComparer.Instance);
    /// <summary>This fight's hits in order, while <see cref="KeepFight"/> holds; null otherwise.</summary>
    private List<HitReport>? _hits;
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
        hero.OnCastStarted += (ability, _, now) =>
        {
            if (_casts != null) _castStarts[ability] = now.TotalSeconds;
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (_tally == null) return;
            _tally.Source(ability.Name).Casts++;
            if (_casts != null)
            {
                // At its start, so a cast with a cast time comes before what was used as it finished (an off-GCD
                // ability, a channel beginning).
                double start = _castStarts.Remove(ability, out double started) ? started : now.TotalSeconds;
                int at = _casts.Count;
                while (at > 0 && _casts[at - 1].Time > start) at--;
                _casts.Insert(at, new CastReport(start, ability.Name, ability.Id));
            }
            // A Spirit ability takes its Spirit Points from the pool with no event of its own.
            if (_hero.ResourceAmount(Hero.SpiritPoints) != _lastSpirit) SpiritChanged(now);
        };
        hero.SpiritOfHeroismStarted += ctx =>
        {
            if (_hero.ResourceAmount(Hero.SpiritPoints) != _lastSpirit) SpiritChanged(ctx.CurrentTime);
        };
        Watch(hero.Auras, onTarget: false);
    }

    /// <summary>True when the fights begun from now keep their casts and hits (<see cref="Fight"/>).</summary>
    public bool KeepFight { get; set; }

    /// <summary>The last fight's casts and hits in order, when <see cref="KeepFight"/> held as it began.</summary>
    public RolledFight Fight => new(_casts?.ToList() ?? [], _hits?.ToList() ?? []);

    /// <summary>Starts fight <paramref name="fight"/> (its index in the run), <paramref name="seconds"/> long, added to <paramref name="tally"/>.</summary>
    public void Begin(int fight, int seconds, Tally tally)
    {
        _tally = tally;
        _fight = fight;
        _lastName = null;
        if (_damage.Length == seconds) Array.Clear(_damage);
        else _damage = new double[seconds];
        _active.Clear();
        _generation++;
        _spans = fight < SpanFights ? new() : null;
        _castStarts.Clear();
        _casts = fight < SpanFights || KeepFight ? new() : null;
        _hits = KeepFight ? new() : null;
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

    /// <summary>
    /// Listens to the debuffs on the fight's targets: those in it now, and each one entering it from now on. The first of
    /// those in it now is the main target.
    /// </summary>
    public void WatchTargets(SimulationContext ctx)
    {
        _primary = ctx.AllEnemies.FirstOrDefault();
        foreach (var enemy in ctx.AllEnemies) Watch(enemy.Auras, onTarget: true);
        ctx.EnemySpawned += enemy => Watch(enemy.Auras, onTarget: true);
    }

    /// <summary>The fight's main target, whose damage is also added up on its own; null for a dungeon.</summary>
    private EnemyTarget? _primary;

    /// <summary>
    /// Ends the fight at <paramref name="duration"/>: the auras still up count until then (or their expiry).
    /// <paramref name="targetSeconds"/> is the seconds each enemy was in combat, summed, and <paramref name="bossSeconds"/>
    /// the seconds of boss fighting. <paramref name="party"/> is a dungeon fight's enemies, for the party's damage: the health
    /// each lost each second up to <paramref name="duration"/>, and the hero's damage to each up to that health.
    /// </summary>
    public void End(SimTime duration, double targetSeconds, double bossSeconds, IReadOnlyList<PartyEnemy>? party = null)
    {
        var tally = _tally!;
        tally.TargetSeconds += targetSeconds;
        tally.BossSeconds += bossSeconds;
        if (party != null) tally.AddParty(party, duration);
        foreach (var state in _active.Values) Close(state, Earlier(state.Aura.ExpiresAt, duration));
        _active.Clear();
        int seconds = Math.Min(_spirit.Length, (int)Math.Ceiling(duration.TotalSeconds));
        tally.LongestSeconds = Math.Max(tally.LongestSeconds, seconds);
        FillSpirit(seconds);
        tally.AddSpirit(_spirit, seconds, _spiritMax);
        tally.AddFight(_fight, _damage, seconds, _spans, _casts, _spans != null ? _spirit[..seconds] : null);
        _tally = null;
    }

    private void OnHit(in DamageContext hit)
    {
        if (_tally == null) return;
        int second = Math.Clamp((int)hit.Timestamp.TotalSeconds, 0, _damage.Length - 1);
        _damage[second] += hit.Amount;
        if (ReferenceEquals(hit.Target, _primary)) _tally.PrimaryDamage += hit.Amount;
        if (hit.Target is { IsBoss: true }) _tally.BossDamage += hit.Amount;
        if (!ReferenceEquals(hit.AbilityName, _lastName))
        {
            _lastName = hit.AbilityName;
            _lastSource = _tally.Source(hit.AbilityName);
        }
        _hits?.Add(new HitReport(hit.Timestamp.TotalSeconds, hit.AbilityName, hit.AbilityId, hit.Amount, hit.IsCrit));
        var source = _lastSource!;
        if (source.Id == 0) source.Id = hit.AbilityId;
        // A hit under another name than its ability's is part of that ability.
        if (source.Group == null && hit.AbilityGroup is { } group && group != hit.AbilityName) source.Group = group;
        source.Damage += hit.Amount;
        source.Hits++;
        source.Crits += hit.CritChance >= 0 ? hit.CritChance : hit.IsCrit ? 1.0 : 0.0;
    }

    private void Watch(AuraContainer auras, bool onTarget)
    {
        var watch = new AuraWatch(onTarget);
        auras.OnGained += (aura, _, now) => Gained(watch, aura, now);
        auras.OnStacksLost += (aura, _, now) =>
        {
            if (_tally != null && Active(watch, aura) is { } state) Restack(state, aura, now);
        };
        auras.OnRemoved += (aura, now) =>
        {
            if (_tally == null || Active(watch, aura) is not { } state) return;
            _active.Remove(state.Aura);
            var end = Earlier(aura.ExpiresAt, now);
            Close(state, end);
            state.Key.Active = null;
            state.Key.LastEnd = end;
            state.Key.HasEnded = true;
        };
    }

    /// <summary>
    /// The active aura's state. An aura re-applied with other values is a new <see cref="Aura"/> of the same key: its
    /// state carries over to it.
    /// </summary>
    private AuraState? Active(AuraWatch watch, Aura aura)
    {
        if (!KeysOf(watch).TryGetValue(aura.Key, out var key) || key.Active is not { } state) return null;
        if (!ReferenceEquals(state.Aura, aura))
        {
            _active.Remove(state.Aura);
            state.Aura = aura;
            _active[aura] = state;
        }
        return state;
    }

    private void Gained(AuraWatch watch, Aura aura, SimTime now)
    {
        if (_tally == null) return;
        if (Active(watch, aura) is { } state)
        {
            state.Tally.Refreshes++;
            state.Tally.MaxStacks = Math.Max(state.Tally.MaxStacks, aura.Stacks);
            Restack(state, aura, now);
            return;
        }
        if (!KeysOf(watch).TryGetValue(aura.Key, out var key)) watch.Keys[aura.Key] = key = new AuraKeyState();
        AuraTally tally;
        if (ReferenceEquals(key.TallyOf, _tally) && ReferenceEquals(key.TallyName, aura.Name)) tally = key.Tally!;
        else
        {
            tally = _tally.Aura(aura.Name, watch.OnTarget);
            (key.Tally, key.TallyOf, key.TallyName) = (tally, _tally, aura.Name);
        }
        if (tally.LogId == 0) tally.LogId = aura.LogId;
        tally.MaxStacks = Math.Max(tally.MaxStacks, aura.Stacks);
        tally.StackCap = Math.Max(tally.StackCap, aura.MaxStacks);
        tally.Applications++;
        if (key.HasEnded)
        {
            tally.Gaps++;
            tally.GapSeconds += (now - key.LastEnd).TotalSeconds;
        }
        // The key is inactive here, so its state from an earlier application is free.
        var applied = key.State ??= new AuraState(key, watch.OnTarget);
        (applied.Aura, applied.Tally, applied.Start, applied.Changed, applied.Stacks) = (aura, tally, now, now, aura.Stacks);
        key.Active = applied;
        _active[aura] = applied;
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

    /// <summary><paramref name="watch"/>'s aura keys this fight (a key from an earlier fight is gone).</summary>
    private Dictionary<string, AuraKeyState> KeysOf(AuraWatch watch)
    {
        if (watch.Generation != _generation)
        {
            watch.Keys.Clear();
            watch.Generation = _generation;
        }
        return watch.Keys;
    }

    /// <summary>One watched aura container: the hero's (<see cref="OnTarget"/> false) or an enemy's.</summary>
    private sealed class AuraWatch(bool onTarget)
    {
        public bool OnTarget { get; } = onTarget;
        /// <summary>The fight <see cref="Keys"/> is of.</summary>
        public int Generation = -1;
        public readonly Dictionary<string, AuraKeyState> Keys = new(StringComparer.Ordinal);
    }

    /// <summary>One aura key of a watched container this fight: its active state, its last end and its tally.</summary>
    private sealed class AuraKeyState
    {
        public AuraState? Active;
        /// <summary>The state of every application of the key, <see cref="Active"/> while one is up.</summary>
        public AuraState? State;
        public bool HasEnded;
        public SimTime LastEnd;
        /// <summary>The tally of <see cref="TallyName"/> in <see cref="TallyOf"/>.</summary>
        public AuraTally? Tally;
        public Tally? TallyOf;
        public string? TallyName;
    }

    private sealed class AuraState(AuraKeyState key, bool onTarget)
    {
        public AuraKeyState Key { get; } = key;
        public bool OnTarget { get; } = onTarget;
        public Aura Aura = null!;
        public AuraTally Tally = null!;
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
        /// <summary>The ability this source is part of, when its hits have another name than the ability (Thunder Call: Hammer Storm of Hammer Storm).</summary>
        public string? Group;

        public void Add(SourceTally other)
        {
            if (Id == 0) Id = other.Id;
            Group ??= other.Group;
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
        /// <summary>The damage to bosses and the seconds of boss fighting, each added up over the fights.</summary>
        public double BossDamage, BossSeconds;
        /// <summary>The seconds each enemy was in combat, added up over the fights and enemies.</summary>
        public double TargetSeconds;
        /// <summary>The whole seconds of the longest fight: the timeline ends there.</summary>
        public int LongestSeconds;
        /// <summary>
        /// A dungeon's party damage each second (the health its enemies lost), added up over the fights; empty for training
        /// dummies.
        /// </summary>
        public double[] PartyDamageSum = [];
        /// <summary>
        /// The party's damage, and the hero's damage to each enemy up to the health it lost, each added up over the fights.
        /// </summary>
        public double PartyDamage, HeroPartyDamage;

        /// <summary>Adds a dungeon fight's party damage up to <paramref name="end"/>.</summary>
        public void AddParty(IReadOnlyList<PartyEnemy> enemies, SimTime end)
        {
            int seconds = (int)Math.Ceiling(end.TotalSeconds);
            if (PartyDamageSum.Length < seconds) Array.Resize(ref PartyDamageSum, seconds);
            foreach (var enemy in enemies)
            {
                double lost = enemy.HealthLost(end);
                PartyDamage += lost;
                HeroPartyDamage += Math.Min(enemy.HeroDamage, lost);
                int first = Math.Max(0, (int)enemy.Start.TotalSeconds);
                for (int s = first; s < seconds; s++)
                {
                    var from = SimTime.FromSeconds(s);
                    var to = SimTime.FromSeconds(s + 1) < end ? SimTime.FromSeconds(s + 1) : end;
                    if (from >= enemy.DiesAt) break;
                    PartyDamageSum[s] += enemy.HealthLost(to) - enemy.HealthLost(from);
                }
            }
        }

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

        /// <summary>
        /// Adds a fight's damage each second, <paramref name="seconds"/> of them up to its end; the band keeps those seconds
        /// alone.
        /// </summary>
        public void AddFight(int fight, double[] damage, int seconds, Dictionary<(string Name, bool OnTarget), List<double>>? spans,
            List<CastReport>? casts, double[]? spirit)
        {
            Fights++;
            if (spans != null) Samples.Add((fight, damage.Sum(), spans, casts ?? [], spirit ?? []));
            if (DamageSum.Length < damage.Length) Array.Resize(ref DamageSum, damage.Length);
            for (int i = 0; i < damage.Length; i++) DamageSum[i] += damage[i];
            if (fight < BandFights) Band.Add((fight, Array.ConvertAll(damage[..Math.Min(seconds, damage.Length)], d => (float)d)));
        }

        public void Merge(Tally other)
        {
            if (other.Fights == 0) return;
            Fights += other.Fights;
            PrimaryDamage += other.PrimaryDamage;
            BossDamage += other.BossDamage;
            BossSeconds += other.BossSeconds;
            TargetSeconds += other.TargetSeconds;
            LongestSeconds = Math.Max(LongestSeconds, other.LongestSeconds);
            PartyDamage += other.PartyDamage;
            HeroPartyDamage += other.HeroPartyDamage;
            if (PartyDamageSum.Length < other.PartyDamageSum.Length) Array.Resize(ref PartyDamageSum, other.PartyDamageSum.Length);
            for (int i = 0; i < other.PartyDamageSum.Length; i++) PartyDamageSum[i] += other.PartyDamageSum[i];
            if (DamageSum.Length < other.DamageSum.Length) Array.Resize(ref DamageSum, other.DamageSum.Length);
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

        /// <summary>
        /// These totals in a chunk's form (<see cref="MonteCarloChunk.Report"/>). The seconds after the longest fight's end
        /// are all 0 and stay behind, so a dungeon's chunk is the length of its fights, not of its time limit.
        /// </summary>
        public MonteCarloChunk.ReportTotals ToTotals() => new()
        {
            Fights = Fights,
            DamageSum = UpToLongest(DamageSum),
            Band = Band.Select(b => new MonteCarloChunk.BandFight(b.Fight, b.Damage)).ToList(),
            Samples = Samples.Select(s => new MonteCarloChunk.SampleFight(s.Fight, s.Damage,
                s.Spans.Select(a => new MonteCarloChunk.AuraSpans(a.Key.Name, a.Key.OnTarget, a.Value)).ToList(), s.Casts, s.Spirit)).ToList(),
            SpiritSum = UpToLongest(SpiritSum),
            SpiritFights = UpToLongest(SpiritFights),
            SpiritMax = SpiritMax,
            Sources = Sources.Select(s => new MonteCarloChunk.SourceTotals(s.Key, s.Value.Damage, s.Value.Crits, s.Value.Casts, s.Value.Hits, s.Value.Id, s.Value.Group)).ToList(),
            Auras = Auras.Select(a => new MonteCarloChunk.AuraTotals(a.Key.Name, a.Key.OnTarget, a.Value.UpSeconds, a.Value.StackSeconds,
                a.Value.AtMaxSeconds, a.Value.GapSeconds, a.Value.Applications, a.Value.Refreshes, a.Value.Gaps, a.Value.MaxStacks,
                a.Value.StackCap, a.Value.LogId)).ToList(),
            PrimaryDamage = PrimaryDamage,
            BossDamage = BossDamage,
            BossSeconds = BossSeconds,
            TargetSeconds = TargetSeconds,
            LongestSeconds = LongestSeconds,
            PartyDamageSum = UpToLongest(PartyDamageSum),
            PartyDamage = PartyDamage,
            HeroPartyDamage = HeroPartyDamage,
        };

        private T[] UpToLongest<T>(T[] perSecond) =>
            LongestSeconds > 0 && LongestSeconds < perSecond.Length ? perSecond[..LongestSeconds] : perSecond;

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
                BossDamage = totals.BossDamage,
                BossSeconds = totals.BossSeconds,
                TargetSeconds = totals.TargetSeconds,
                LongestSeconds = totals.LongestSeconds,
                PartyDamageSum = totals.PartyDamageSum,
                PartyDamage = totals.PartyDamage,
                HeroPartyDamage = totals.HeroPartyDamage,
            };
            tally.Band.AddRange(totals.Band.Select(b => (b.Fight, b.Damage)));
            tally.Samples.AddRange(totals.Samples.Select(s => (s.Fight, s.Damage,
                s.Spans.ToDictionary(a => (a.Name, a.OnTarget), a => a.Spans), s.Casts, s.Spirit)));
            foreach (var s in totals.Sources)
            {
                tally.Sources[s.Name] = new SourceTally { Damage = s.Damage, Crits = s.Crits, Casts = s.Casts, Hits = s.Hits, Id = s.Id, Group = s.Group };
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
            // The seconds up to the longest fight's end: a dungeon's fights end before its time limit.
            int seconds = LongestSeconds > 0 ? Math.Min(LongestSeconds, DamageSum.Length) : DamageSum.Length;
            double[] mean = DamageSum.Take(seconds).Select(d => Fights == 0 ? 0 : d / Fights).ToArray();
            var cumulative = Cumulative(mean);
            var rolling = Rolling(mean);
            var bandCumulative = Band.Select(b => Cumulative(Seconds(b.Damage, seconds))).ToList();
            var bandRolling = Band.Select(b => Rolling(Seconds(b.Damage, seconds))).ToList();
            double[]? partyDps = PartyDamageSum.Length == 0 ? null
                : Rolling(Enumerable.Range(0, seconds).Select(i => i < PartyDamageSum.Length && Fights > 0 ? PartyDamageSum[i] / Fights : 0).ToArray());
            return new SimTimeline(cumulative, Percentile(bandCumulative, seconds, 0.05), Percentile(bandCumulative, seconds, 0.95),
                rolling, Percentile(bandRolling, seconds, 0.05), Percentile(bandRolling, seconds, 0.95), RollingSeconds, partyDps);
        }

        /// <summary>
        /// Each damage source, most damage first; values are means per fight. An ability whose hits all have other names
        /// is there with its casts and no damage, for the sources that are part of it.
        /// </summary>
        public List<AbilityReport> Abilities(double seconds)
        {
            var groups = Sources.Where(s => s.Value.Damage > 0 && s.Value.Group != null).Select(s => s.Value.Group!).ToHashSet();
            return Sources
                .Where(s => s.Value.Damage > 0 || groups.Contains(s.Key))
                .OrderByDescending(s => s.Value.Damage)
                .Select(s =>
                {
                    var t = s.Value;
                    double fights = Math.Max(1, Fights);
                    return new AbilityReport(s.Key, t.Damage / fights, t.Damage / fights / seconds, t.Casts / fights, t.Hits / fights,
                        t.Hits > 0 ? t.Crits / t.Hits : 0, t.Hits > 0 ? t.Damage / t.Hits : 0, t.Id, t.Group);
                })
                .ToList();
        }

        /// <summary>
        /// Each buff and debuff, buffs first, longest up first. An aura up the whole of every fight from its single
        /// application (a passive) is left out, and so is an engine-internal one (no log ID, such as a marker on a target).
        /// </summary>
        public List<BuffReport> Buffs(double seconds)
        {
            var typical = Typical()?.Spans;
            return Auras
            .Where(a => a.Value.LogId != 0)
            .Select(a =>
            {
                var (name, onTarget) = a.Key;
                var t = a.Value;
                double fights = Math.Max(1, Fights);
                // A debuff's uptime is over the time each enemy was in combat.
                double span = onTarget && TargetSeconds > 0 ? TargetSeconds : fights * seconds;
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

        /// <summary>The typical fight's index in the run, or null for a run of no sampled fight.</summary>
        public int? TypicalFight() => Typical()?.Fight;

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

        // A band fight's damage over the first seconds; it has none after its end.
        private static double[] Seconds(float[] damage, int seconds)
        {
            var result = new double[seconds];
            for (int i = 0; i < Math.Min(seconds, damage.Length); i++) result[i] = damage[i];
            return result;
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
