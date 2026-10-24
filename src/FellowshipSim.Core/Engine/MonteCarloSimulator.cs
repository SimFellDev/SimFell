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
    /// <paramref name="progress"/> receives the count of completed fights: once per batch of about 1% of the fights (at
    /// most <see cref="MaxProgressBatch"/>) on each thread, and <paramref name="iterations"/> last. Counts only rise, and
    /// no two calls overlap. <paramref name="cancellationToken"/> stops the run between fights with an
    /// <see cref="OperationCanceledException"/>.
    /// </para>
    /// </summary>
    public MonteCarloResult Run(SimTime duration, int iterations = 100, int targetCount = 1, int firstSeed = 1,
        int? degreeOfParallelism = null, bool keepFightDamage = false, Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        double totalSeconds = Math.Max(1.0, duration.TotalSeconds);
        int seconds = (int)Math.Ceiling(totalSeconds);
        double[]? fightDamage = keepFightDamage ? new double[iterations] : null;
        var total = new Totals();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degreeOfParallelism ?? DefaultDegreeOfParallelism,
            CancellationToken = cancellationToken,
        };
        var reporter = progress == null ? null : new ProgressReporter(progress, Math.Clamp(iterations / 100, 1, MaxProgressBatch));

        Parallel.For(0, iterations, options,
            () => (Worker: _idle.TryTake(out var worker) ? worker : NewWorker(), Totals: new Totals()),
            (i, _, local) =>
            {
                double damage = Fight(local.Worker, duration, targetCount, firstSeed + i, i, seconds, local.Totals);
                local.Totals.Add(damage / totalSeconds);
                if (fightDamage != null) fightDamage[i] = damage;
                reporter?.FightDone(local.Totals);
                return local;
            },
            local =>
            {
                lock (total) total.Merge(local.Totals);
                _idle.Add(local.Worker);
            });
        reporter?.Finish(iterations);

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
            TotalMeanDamage = total.Mean * totalSeconds,
            AbilityDpsBreakdown = abilityDps,
            IterationDamage = fightDamage ?? [],
            Iterations = iterations,
            DurationSeconds = totalSeconds,
            Timeline = total.Report.Timeline(),
            Abilities = total.Report.Abilities(totalSeconds),
            Buffs = total.Report.Buffs(totalSeconds, targetCount),
        };
    }

    private Worker NewWorker()
    {
        var hero = _newHero();
        return new Worker(hero, _actions.Bind(hero), new FightRecorder(hero));
    }

    /// <summary>
    /// Fight <paramref name="fight"/> of the run on the worker's hero; returns its total damage and adds each ability's
    /// damage and the fight's report to <paramref name="totals"/>.
    /// </summary>
    private static double Fight(Worker worker, SimTime duration, int targetCount, int seed, int fight, int seconds, Totals totals)
    {
        var hero = worker.Hero;
        var ctx = new SimulationContext(hero);
        ctx.Rng.Seed(seed);
        hero.ResetForIteration();
        worker.Recorder.Begin(fight, seconds, totals.Report);
        hero.BeginFight(ctx);

        // Populate enemies
        for (int i = 1; i <= targetCount; i++)
        {
            ctx.AddEnemy(100 + i, i, $"Training Mob {i}", maxHealth: 100_000_000);
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

    /// <summary>The most fights a thread runs between two progress reports.</summary>
    public const int MaxProgressBatch = 1000;

    private sealed record Worker(Hero Hero, AplProgram Program, FightRecorder Recorder);

    /// <summary>
    /// Completed fights of a run, sent to the progress callback per batch of a thread's fights. A fight costs one
    /// comparison; a batch costs one interlocked add and one lock.
    /// </summary>
    private sealed class ProgressReporter(Action<int> progress, int batch)
    {
        private int _completed;
        private int _reported;

        public void FightDone(Totals local)
        {
            if (local.Count - local.Reported < batch) return;
            local.Reported = local.Count;
            Report(Interlocked.Add(ref _completed, batch));
        }

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
        /// <summary><see cref="Count"/> at this thread's last progress report.</summary>
        public long Reported;
        public double Mean;
        public double M2;
        public double Min = double.PositiveInfinity;
        public double Max = double.NegativeInfinity;
        public readonly Dictionary<string, double> AbilityDamage = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>The fights' report: damage over time, sources and auras.</summary>
        public readonly FightRecorder.Tally Report = new();

        public void Add(double dps)
        {
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
