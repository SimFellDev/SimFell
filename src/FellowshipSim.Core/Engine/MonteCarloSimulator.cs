using System.Collections.Concurrent;
using FellowshipSim.Core.APL;
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
    /// <summary>The casts of one typical fight (the sampled fight nearest the mean damage), in order.</summary>
    public IReadOnlyList<CastReport> Casts { get; init; } = [];
    /// <summary>The hero's Spirit Points over the fight.</summary>
    public SpiritTimeline? Spirit { get; init; }
    /// <summary>The first target's DPS: its damage in every fight over their lengths.</summary>
    public double PrimaryTargetDps { get; init; }
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
    /// </summary>
    public MonteCarloResult Run(SimTime duration, int iterations = 100, int targetCount = 1, int firstSeed = 1,
        int? degreeOfParallelism = null, bool keepFightDamage = false, Action<int>? progress = null,
        CancellationToken cancellationToken = default, FightOptions? fight = null)
    {
        fight ??= FightOptions.Default;
        int seconds = (int)Math.Ceiling(Math.Max(1.0, fight.LongestLength(duration).TotalSeconds));
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
        int chunkSize = Math.Clamp(iterations / 100, 1, MaxProgressBatch);
        int chunkCount = (iterations + chunkSize - 1) / chunkSize;
        var finished = new Totals?[chunkCount];
        int nextToMerge = 0;

        Parallel.For(0, chunkCount, options,
            () => _idle.TryTake(out var worker) ? worker : NewWorker(),
            (chunk, _, worker) =>
            {
                var totals = new Totals();
                int end = Math.Min(iterations, (chunk + 1) * chunkSize);
                for (int i = chunk * chunkSize; i < end; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var length = fight.Length(duration, firstSeed + i);
                    double damage = Fight(worker, length, targetCount, firstSeed + i, i, seconds, fight, totals);
                    double lengthSeconds = Math.Max(1.0, length.TotalSeconds);
                    totals.Add(damage / lengthSeconds, damage, lengthSeconds);
                    if (fightDamage != null) fightDamage[i] = damage;
                }
                lock (finished)
                {
                    finished[chunk] = totals;
                    while (nextToMerge < chunkCount && finished[nextToMerge] is { } next)
                    {
                        total.Merge(next);
                        finished[nextToMerge++] = null;
                    }
                }
                reporter?.FightsDone(end - chunk * chunkSize);
                return worker;
            },
            worker => _idle.Add(worker));
        reporter?.Finish(iterations);

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
            Buffs = total.Report.Buffs(totalSeconds, targetCount),
            Casts = total.Report.Casts(),
            Spirit = total.Report.Spirit(),
            PrimaryTargetDps = total.LengthSum > 0 ? total.Report.PrimaryDamage / total.LengthSum : 0,
        };
    }

    private Worker NewWorker()
    {
        var hero = _newHero();
        return new Worker(hero, _actions.Bind(hero), new FightRecorder(hero));
    }

    /// <summary>
    /// Fight <paramref name="fight"/> of the run on the worker's hero, <paramref name="duration"/> long; returns its total
    /// damage and adds each ability's damage and the fight's report to <paramref name="totals"/>.
    /// </summary>
    private static double Fight(Worker worker, SimTime duration, int targetCount, int seed, int fight, int seconds,
        FightOptions options, Totals totals)
    {
        var hero = worker.Hero;
        var ctx = new SimulationContext(hero);
        ctx.Rng.Seed(seed);
        hero.ResetForIteration();
        worker.Recorder.Begin(fight, seconds, totals.Report);
        hero.BeginFight(ctx);
        if (options.StartingSpirit > 0) hero.GainResource(Hero.SpiritPoints, options.StartingSpirit, ctx);

        // Populate enemies
        for (int i = 1; i <= targetCount; i++)
        {
            var enemy = ctx.AddEnemy(100 + i, i, $"Training Mob {i}", maxHealth: 100_000_000);
            if (options.HealthFollowsTime) enemy.FollowTime(ctx, duration);
        }
        ctx.Player.CurrentTarget = ctx.AllEnemies.First();
        worker.Recorder.WatchTargets(ctx);

        // Main loop: whenever the hero is free to act, run the first ready APL action, then let the
        // event queue carry time forward to the next decision point.
        while (ctx.CurrentTime < duration)
        {
            bool acted = IsFreeToAct(hero, ctx) && worker.Program.TryAct(ctx);

            var next = NextDecisionTime(hero);
            if (!acted && next <= ctx.CurrentTime)
            {
                // Nothing was cast: decide again at the next event or when something the action list reads changes.
                next = worker.Program.NextDecision(ctx);
            }
            ctx.RunUntil(next < duration ? next : duration);
        }

        hero.Auras.FinalizeUptimes(duration);
        worker.Recorder.End(duration);

        foreach (var (ability, dmg) in ctx.AbilityDamage)
        {
            totals.AbilityDamage.TryGetValue(ability, out double cur);
            totals.AbilityDamage[ability] = cur + dmg;
        }
        return ctx.TotalDamage;
    }

    private static bool IsFreeToAct(Hero hero, SimulationContext ctx) =>
        hero.CurrentCast == null && hero.CurrentChannel == null && ctx.CurrentTime >= hero.GlobalCooldownEndTime;

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
        public readonly FightRecorder.Tally Report = new();

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
    }
}
