using System.Collections.Concurrent;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Debugging;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Core.Engine;

public class MonteCarloResult
{
    public double MeanDps { get; init; }
    public double MinDps { get; init; }
    public double MaxDps { get; init; }
    public double StandardDeviation { get; init; }
    public double TotalMeanDamage { get; init; }
    public Dictionary<string, double> AbilityDpsBreakdown { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Total damage of each fight, in seed order. Empty unless the run was asked to keep it.</summary>
    public IReadOnlyList<double> IterationDamage { get; init; } = [];
    public int Iterations { get; init; }
    public double DurationSeconds { get; init; }
    /// <summary>Damage over the fight: the mean of every fight and the 5-95% band of a sample.</summary>
    public SimTimeline? Timeline { get; init; }
    /// <summary>Each source of damage, most first, as means per fight.</summary>
    public IReadOnlyList<AbilityReport> Abilities { get; init; } = [];
    /// <summary>Each buff on the hero and debuff on the targets, as means per fight.</summary>
    public IReadOnlyList<BuffReport> Buffs { get; init; } = [];
    /// <summary>
    /// The casts of one typical fight (the sampled fight nearest the mean damage), in order: rolled
    /// (<see cref="MonteCarloSimulator.RollFight"/>) for a run asked to roll it, and as the run fought it otherwise.
    /// </summary>
    public IReadOnlyList<CastReport> Casts { get; init; } = [];
    /// <summary>The typical fight's index in the run (its seed is the first seed plus it), or null for a run of no fights.</summary>
    public int? TypicalFight { get; init; }
    /// <summary>The rolled typical fight's hits, in order (<see cref="MonteCarloSimulator.RollFight"/>). Empty for a run not asked to roll it.</summary>
    public IReadOnlyList<HitReport> Hits { get; init; } = [];
    /// <summary>The hero's Spirit Points over the fight.</summary>
    public SpiritTimeline? Spirit { get; init; }
    /// <summary>The first target's DPS: its damage in every fight over their lengths.</summary>
    public double PrimaryTargetDps { get; init; }
    /// <summary>The DPS to bosses over the time of boss fighting in every fight; 0 when the fights had no enemies besides bosses, or no boss.</summary>
    public double BossDps { get; init; }
}

/// <summary>
/// Monte Carlo fights of one build. Fights run on several threads: each thread has its own hero and bound action list,
/// reused across its fights, and its own running totals, merged when the run ends. Fight i has seed first seed + i, so
/// each fight's result is the same at every degree of parallelism.
/// </summary>
public class MonteCarloSimulator
{
    private readonly Func<Hero> _newHero;
    private readonly AplActionLists _actions;
    // Speed: heroes are built once and kept between runs. A Parallel.For worker can start more than once per run.
    private readonly ConcurrentBag<Worker> _idle = [];

    /// <summary>
    /// A simulator of the heroes <paramref name="newHero"/> builds, casting by <paramref name="actions"/>. Each call of
    /// <paramref name="newHero"/> must return a new hero. The first hero is built here, so an action naming no ability of
    /// the hero throws here.
    /// </summary>
    public MonteCarloSimulator(Func<Hero> newHero, AplActionLists actions)
    {
        _newHero = newHero;
        _actions = actions;
        _idle.Add(NewWorker());
    }

    /// <summary>A simulator of heroes imported from <paramref name="build"/>.</summary>
    public MonteCarloSimulator(CharacterBuild build, AplActionLists actions) : this(() => HeroImporter.Import(build), actions) { }

    /// <summary>
    /// Threads of a run given no degree of parallelism: 80% of <see cref="Environment.ProcessorCount"/>, rounded down, and
    /// at least 1. The other processors stay free for the rest of the machine.
    /// </summary>
    public static int DefaultDegreeOfParallelism => Math.Max(1, Environment.ProcessorCount * 4 / 5);

    /// <summary>
    /// Runs <paramref name="iterations"/> fights on up to <paramref name="degreeOfParallelism"/> threads
    /// (<see cref="DefaultDegreeOfParallelism"/> when null). Fight i uses seed <paramref name="firstSeed"/> + i, so a run
    /// can be repeated exactly. <paramref name="keepFightDamage"/> keeps each fight's damage in
    /// <see cref="MonteCarloResult.IterationDamage"/>.
    /// <para>
    /// <paramref name="progress"/> receives the count of completed fights: once per chunk of about 1% of the fights (at
    /// most <see cref="MaxProgressBatch"/>), and <paramref name="iterations"/> last. Counts only rise, and no two calls
    /// overlap. The totals are the same on any number of threads. <paramref name="cancellationToken"/> stops the run between fights with an
    /// <see cref="OperationCanceledException"/>.
    /// </para>
    /// <paramref name="fight"/> sets each fight's length, the targets' health and the starting Spirit
    /// (<see cref="FightOptions.Default"/> when null). A fight's DPS is its damage over its own length.
    /// <paramref name="rollTypicalFight"/> fights the typical fight once more with dice (<see cref="RollFight"/>) for the
    /// result's casts and hits; the hero's events hear that fight too.
    /// </summary>
    public MonteCarloResult Run(SimTime duration, int iterations = 100, int targetCount = 1, int firstSeed = 1,
        int? degreeOfParallelism = null, bool keepFightDamage = false, Action<int>? progress = null,
        CancellationToken cancellationToken = default, FightOptions? fight = null, bool rollTypicalFight = false)
    {
        fight ??= FightOptions.Default;
        double[]? fightDamage = keepFightDamage ? new double[iterations] : null;
        var total = new Totals();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degreeOfParallelism ?? DefaultDegreeOfParallelism,
            CancellationToken = cancellationToken,
        };
        var reporter = progress == null ? null : new ProgressReporter(progress);

        // Fights run in chunks of consecutive fights, and the chunks' totals merge in chunk order whatever thread ran
        // them, so a run gives the same totals to the last bit on any number of threads.
        int chunkCount = ChunkCount(iterations);
        var finished = new Totals?[chunkCount];
        int nextToMerge = 0;

        Parallel.For(0, chunkCount, options,
            () => _idle.TryTake(out var worker) ? worker : NewWorker(),
            (chunk, _, worker) =>
            {
                var totals = FightChunk(worker, chunk, duration, iterations, targetCount, firstSeed, fight, fightDamage, cancellationToken);
                lock (finished)
                {
                    finished[chunk] = totals;
                    while (nextToMerge < chunkCount && finished[nextToMerge] is { } next)
                    {
                        total.Merge(next);
                        finished[nextToMerge++] = null;
                    }
                }
                reporter?.FightsDone((int)totals.Count);
                return worker;
            },
            worker => _idle.Add(worker));
        reporter?.Finish(iterations);
        int? typical = total.Report.TypicalFight();
        return Result(total, fightDamage, duration, iterations,
            rollTypicalFight && typical is { } t ? RollFight(t, duration, targetCount, firstSeed, fight) : null);
    }

    /// <summary>
    /// The chunks a run of <paramref name="iterations"/> fights is split into: consecutive fights, about 1% of them each
    /// (at most <see cref="MaxProgressBatch"/>).
    /// </summary>
    public static int ChunkCount(int iterations) => (iterations + ChunkSize(iterations) - 1) / ChunkSize(iterations);

    private static int ChunkSize(int iterations) => Math.Clamp(iterations / 100, 1, MaxProgressBatch);

    /// <summary>
    /// Fights chunk <paramref name="chunk"/> (of <see cref="ChunkCount"/>) of a run, as <see cref="Run"/> would with the same
    /// arguments, and returns its totals; <see cref="Combine"/> merges a run's chunks. A run can so be split between
    /// processes and still give <see cref="Run"/>'s result.
    /// </summary>
    public MonteCarloChunk RunChunk(int chunk, SimTime duration, int iterations, int targetCount = 1, int firstSeed = 1,
        bool keepFightDamage = false, CancellationToken cancellationToken = default, FightOptions? fight = null)
    {
        var worker = _idle.TryTake(out var idle) ? idle : NewWorker();
        try
        {
            int size = ChunkSize(iterations);
            double[]? fightDamage = keepFightDamage ? new double[iterations] : null;
            var totals = FightChunk(worker, chunk, duration, iterations, targetCount, firstSeed, fight ?? FightOptions.Default,
                fightDamage, cancellationToken);
            int start = chunk * size;
            return totals.ToChunk(chunk, fightDamage == null ? [] : fightDamage[start..Math.Min(iterations, start + size)]);
        }
        finally
        {
            _idle.Add(worker);
        }
    }

    /// <summary>
    /// The result of a run from its chunks (<see cref="RunChunk"/>), every one of them, in chunk order: the same as
    /// <see cref="Run"/> with the same arguments.
    /// </summary>
    public static MonteCarloResult Combine(IReadOnlyList<MonteCarloChunk> chunks, SimTime duration, int iterations,
        bool keepFightDamage = false)
    {
        if (chunks.Count != ChunkCount(iterations) || chunks.Where((c, i) => c.Index != i).Any())
        {
            throw new ArgumentException($"A run of {iterations} fights has {ChunkCount(iterations)} chunks, in order.", nameof(chunks));
        }
        var total = new Totals();
        foreach (var chunk in chunks) total.Merge(Totals.From(chunk));
        double[]? fightDamage = keepFightDamage ? chunks.SelectMany(c => c.FightDamage).ToArray() : null;
        return Result(total, fightDamage, duration, iterations);
    }

    // The fights of one chunk on one worker. A kept fight's damage goes in its place in fightDamage.
    private static Totals FightChunk(Worker worker, int chunk, SimTime duration, int iterations, int targetCount, int firstSeed,
        FightOptions fight, double[]? fightDamage, CancellationToken cancellationToken)
    {
        int seconds = (int)Math.Ceiling(Math.Max(1.0, fight.LongestLength(duration).TotalSeconds));
        int size = ChunkSize(iterations);
        var totals = new Totals();
        int end = Math.Min(iterations, (chunk + 1) * size);
        for (int i = chunk * size; i < end; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = fight.Length(duration, firstSeed + i);
            var (damage, fightEnd) = Fight(worker, length, targetCount, firstSeed + i, i, seconds, fight, totals);
            double lengthSeconds = Math.Max(1.0, fightEnd.TotalSeconds);
            totals.Add(damage / lengthSeconds, damage, lengthSeconds);
            if (fightDamage != null) fightDamage[i] = damage;
        }
        return totals;
    }

    /// <summary>
    /// Fight <paramref name="fight"/> of a run with these arguments again, with its seed: its casts and hits. A run's fights
    /// keep no hits, since the typical fight is known only once every fight is done.
    /// </summary>
    public RolledFight RollFight(int fight, SimTime duration, int targetCount = 1, int firstSeed = 1, FightOptions? options = null)
    {
        options ??= FightOptions.Default;
        var worker = _idle.TryTake(out var idle) ? idle : NewWorker();
        try
        {
            int seconds = (int)Math.Ceiling(Math.Max(1.0, options.LongestLength(duration).TotalSeconds));
            worker.Recorder.KeepFight = true;
            Fight(worker, options.Length(duration, firstSeed + fight), targetCount, firstSeed + fight, fight, seconds, options, new Totals());
            return worker.Recorder.Fight;
        }
        finally
        {
            worker.Recorder.KeepFight = false;
            _idle.Add(worker);
        }
    }

    /// <summary>
    /// Fight <paramref name="fight"/> of a run with these arguments again, with its seed, on a new hero whose action lists
    /// log every decision (<see cref="AplProgram.BindTraced"/>): each decision's hero, enemies, steps and cast, and the
    /// fight's casts. Its decisions and casts are those of the same fight in <see cref="RollFight"/>.
    /// </summary>
    public AplFightTrace TraceFight(int fight, SimTime duration, int targetCount = 1, int firstSeed = 1, FightOptions? options = null)
    {
        options ??= FightOptions.Default;
        var hero = _newHero();
        var program = AplProgram.BindTraced(_actions, hero);
        var worker = new Worker(hero, program, new FightRecorder(hero)) { Recorder = { KeepFight = true } };
        int seconds = (int)Math.Ceiling(Math.Max(1.0, options.LongestLength(duration).TotalSeconds));
        var (_, end) = Fight(worker, options.Length(duration, firstSeed + fight), targetCount, firstSeed + fight, fight, seconds, options, new Totals());
        return program.Log!.Trace(end.TotalSeconds, worker.Recorder.Fight.Casts);
    }

    private static MonteCarloResult Result(Totals total, double[]? fightDamage, SimTime duration, int iterations,
        RolledFight? rolled = null)
    {
        // The fights' mean length: every fight's, or the duration for a run of none.
        double totalSeconds = total.Count > 0 ? total.LengthSum / total.Count : Math.Max(1.0, duration.TotalSeconds);
        var abilityDps = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (ability, totalDmg) in total.AbilityDamage)
        {
            abilityDps[ability] = (totalDmg / iterations) / totalSeconds;
        }

        return new MonteCarloResult
        {
            MeanDps = total.Mean,
            MinDps = total.Min,
            MaxDps = total.Max,
            StandardDeviation = Math.Sqrt(total.M2 / total.Count),
            TotalMeanDamage = total.Count > 0 ? total.DamageSum / total.Count : 0,
            AbilityDpsBreakdown = abilityDps,
            IterationDamage = fightDamage ?? [],
            Iterations = iterations,
            DurationSeconds = totalSeconds,
            Timeline = total.Report.Timeline(),
            Abilities = total.Report.Abilities(totalSeconds),
            Buffs = total.Report.Buffs(totalSeconds),
            Casts = rolled?.Casts ?? total.Report.Casts(),
            TypicalFight = total.Report.TypicalFight(),
            Hits = rolled?.Hits ?? [],
            Spirit = total.Report.Spirit(),
            PrimaryTargetDps = total.LengthSum > 0 ? total.Report.PrimaryDamage / total.LengthSum : 0,
            // Only when the fights had other enemies besides their bosses: otherwise it is the mean DPS.
            BossDps = total.Report.BossSeconds > 0 && total.Report.BossSeconds < total.LengthSum * 0.999 ? total.Report.BossDamage / total.Report.BossSeconds : 0,
        };
    }

    private Worker NewWorker()
    {
        var hero = _newHero();
        return new Worker(hero, _actions.Bind(hero), new FightRecorder(hero));
    }

    /// <summary>
    /// Fight <paramref name="fight"/> of the run on the worker's hero, <paramref name="duration"/> long (a dungeon's time
    /// limit); returns its total damage and its end, and adds each ability's damage and the fight's report to
    /// <paramref name="totals"/>.
    /// </summary>
    private static (double Damage, SimTime End) Fight(Worker worker, SimTime duration, int targetCount, int seed, int fight, int seconds,
        FightOptions options, Totals totals)
    {
        var hero = worker.Hero;
        var ctx = new SimulationContext(hero);
        ctx.Rng.Seed(seed);
        hero.ResetForIteration();
        worker.Recorder.Begin(fight, seconds, totals.Report);
        hero.BeginFight(ctx);
        if (options.StartingSpirit > 0) hero.GainResource(Hero.SpiritPoints, options.StartingSpirit, ctx);
        ctx.FightEnd = duration;

        double targetSeconds, bossSeconds;
        var end = duration;
        if (options.Dungeon is { } dungeon)
        {
            // A dungeon has no main target: its enemies come and go.
            worker.Recorder.WatchTargets(ctx);
            (end, targetSeconds, bossSeconds) = new DungeonFight(hero, ctx, dungeon, options.VarianceScale(seed), options.HeroShare(seed),
                (until, stop) => RunUntil(worker, ctx, until, stop)).Run();
        }
        else
        {
            if (!options.Boss) foreach (var aura in hero.BossAuras) hero.Auras.Remove(aura, SimTime.Zero);
            ctx.Boss = options.Boss;
            for (int i = 1; i <= targetCount; i++)
            {
                var enemy = ctx.AddEnemy(100 + i, i, $"Training Dummy {i}", maxHealth: TrainingDummyHealth);
                enemy.IsBoss = options.Boss;
                if (options.HealthFollowsTime) enemy.FollowTime(ctx, SimTime.Zero, duration);
            }
            ctx.Player.CurrentTarget = ctx.AllEnemies.First();
            worker.Recorder.WatchTargets(ctx);
            RunUntil(worker, ctx, duration);
            targetSeconds = targetCount * duration.TotalSeconds;
            bossSeconds = options.Boss ? duration.TotalSeconds : 0;
        }

        hero.Auras.FinalizeUptimes(end);
        worker.Recorder.End(end, targetSeconds, bossSeconds);

        foreach (var (ability, dmg) in ctx.AbilityDamage)
        {
            totals.AbilityDamage.TryGetValue(ability, out double cur);
            totals.AbilityDamage[ability] = cur + dmg;
        }
        return (ctx.TotalDamage, end);
    }

    /// <summary>The health of every training dummy; it falls with time, so its size only sets damage-to-health ratios.</summary>
    private const double TrainingDummyHealth = 100_000_000;

    /// <summary>
    /// The hero acts until <paramref name="end"/>: whenever they are free to act, the first ready APL action runs, then the
    /// event queue carries time forward to the next decision point.
    /// </summary>
    private static void RunUntil(Worker worker, SimulationContext ctx, SimTime end) => RunUntil(worker, ctx, end, null);

    /// <summary>
    /// The hero acts as <see cref="RunUntil(Worker, SimulationContext, SimTime)"/> has them, and stops right after the
    /// event at which <paramref name="stop"/> holds. Returns true when it stopped there.
    /// </summary>
    private static bool RunUntil(Worker worker, SimulationContext ctx, SimTime end, Func<bool>? stop)
    {
        var hero = worker.Hero;
        bool castsDuringGcd = hero.Abilities.Any(a => a.UsableDuringGcd);
        while (ctx.CurrentTime < end)
        {
            if (stop != null && stop()) return true;
            bool acted = IsFreeToAct(hero, ctx, castsDuringGcd) && worker.Program.TryAct(ctx);

            var next = NextDecisionTime(hero);
            if (!acted && next <= ctx.CurrentTime)
            {
                // Nothing was cast: decide again at the next event or when something the action list reads changes.
                next = worker.Program.NextDecision(ctx);
            }
            else if (castsDuringGcd && OnGcdOnly(hero, ctx))
            {
                // An ability usable during the global cooldown can go now, after the cast that started it, or once it
                // becomes ready before the global cooldown ends.
                var decision = acted ? ctx.CurrentTime : worker.Program.NextDecision(ctx);
                if (decision < next) next = decision;
            }
            if (ctx.RunUntil(next < end ? next : end, stop)) return true;
        }
        return stop != null && stop();
    }

    // While the global cooldown runs, a hero with an ability usable during it acts too: the others refuse
    // (SimulationContext.Activate).
    private static bool IsFreeToAct(Hero hero, SimulationContext ctx, bool castsDuringGcd) =>
        hero.CurrentCast == null && hero.CurrentChannel == null && (castsDuringGcd || ctx.CurrentTime >= hero.GlobalCooldownEndTime);

    // The hero is held by the global cooldown alone.
    private static bool OnGcdOnly(Hero hero, SimulationContext ctx) =>
        hero.CurrentCast == null && hero.CurrentChannel == null && ctx.CurrentTime < hero.GlobalCooldownEndTime;

    private static SimTime NextDecisionTime(Hero hero)
    {
        var next = hero.GlobalCooldownEndTime;
        if (hero.CurrentCast != null && hero.CurrentCast.EndTime > next) next = hero.CurrentCast.EndTime;
        if (hero.CurrentChannel != null && hero.CurrentChannel.Ticks.End > next) next = hero.CurrentChannel.Ticks.End;
        return next;
    }

    /// <summary>The most fights in a chunk of a run, and so between two progress reports.</summary>
    public const int MaxProgressBatch = 1000;

    private sealed record Worker(Hero Hero, AplProgram Program, FightRecorder Recorder);

    /// <summary>
    /// Completed fights of a run, sent to the progress callback per chunk of fights. A chunk costs one interlocked add and
    /// one lock.
    /// </summary>
    private sealed class ProgressReporter(Action<int> progress)
    {
        private int _completed;
        private int _reported;

        public void FightsDone(int fights) => Report(Interlocked.Add(ref _completed, fights));

        public void Finish(int iterations) => Report(iterations);

        private void Report(int completed)
        {
            lock (this)
            {
                if (completed <= _reported) return;
                _reported = completed;
                progress(completed);
            }
        }
    }

    /// <summary>Running totals of fight DPS (Welford's mean and sum of squared deviations) and of each ability's damage.</summary>
    private sealed class Totals
    {
        public long Count;
        public double Mean;
        public double M2;
        public double Min = double.PositiveInfinity;
        public double Max = double.NegativeInfinity;
        /// <summary>The fights' damage and lengths in seconds, added up.</summary>
        public double DamageSum, LengthSum;
        public readonly Dictionary<string, double> AbilityDamage = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>The fights' report: damage over time, sources and auras.</summary>
        public readonly FightRecorder.Tally Report;

        public Totals() : this(new FightRecorder.Tally()) { }

        public Totals(FightRecorder.Tally report) => Report = report;

        public void Add(double dps, double damage, double seconds)
        {
            DamageSum += damage;
            LengthSum += seconds;
            Count++;
            double delta = dps - Mean;
            Mean += delta / Count;
            M2 += delta * (dps - Mean);
            Min = Math.Min(Min, dps);
            Max = Math.Max(Max, dps);
        }

        /// <summary>Adds <paramref name="other"/>'s fights (Chan's parallel formula for the mean and squared deviations).</summary>
        public void Merge(Totals other)
        {
            if (other.Count == 0) return;
            long count = Count + other.Count;
            double delta = other.Mean - Mean;
            Mean += delta * other.Count / count;
            M2 += other.M2 + delta * delta * ((double)Count * other.Count / count);
            Count = count;
            DamageSum += other.DamageSum;
            LengthSum += other.LengthSum;
            Min = Math.Min(Min, other.Min);
            Max = Math.Max(Max, other.Max);
            foreach (var (ability, damage) in other.AbilityDamage)
            {
                AbilityDamage.TryGetValue(ability, out double cur);
                AbilityDamage[ability] = cur + damage;
            }
            Report.Merge(other.Report);
        }

        public MonteCarloChunk ToChunk(int index, double[] fightDamage) => new()
        {
            Index = index,
            Count = Count,
            Mean = Mean,
            M2 = M2,
            Min = Min,
            Max = Max,
            DamageSum = DamageSum,
            LengthSum = LengthSum,
            AbilityDamage = new Dictionary<string, double>(AbilityDamage, StringComparer.OrdinalIgnoreCase),
            FightDamage = fightDamage,
            Report = Report.ToTotals(),
        };

        public static Totals From(MonteCarloChunk chunk)
        {
            var totals = new Totals(FightRecorder.Tally.From(chunk.Report))
            {
                Count = chunk.Count,
                Mean = chunk.Mean,
                M2 = chunk.M2,
                Min = chunk.Min,
                Max = chunk.Max,
                DamageSum = chunk.DamageSum,
                LengthSum = chunk.LengthSum,
            };
            foreach (var (ability, damage) in chunk.AbilityDamage) totals.AbilityDamage[ability] = damage;
            return totals;
        }
    }
}
