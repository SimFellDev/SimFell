using BenchmarkDotNet.Attributes;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Logs.Replay;

namespace FellowshipSim.Benchmarks;

/// <summary>
/// One 300 s Monte Carlo fight, three ways. <see cref="MonteCarloFight"/> is one fight on a new hero.
/// <see cref="TenFightsNewHeroes"/> is ten fights, each on its own new hero, and its time is per fight.
/// <see cref="TenFightsOneHero"/> is ten fights on one hero, as a Monte Carlo run has them. Before 02e1759 a fight that
/// followed another on the same hero started from the earlier fight's state and dealt less than half the damage, so only
/// the first two compare commits before and after it. These three run on one thread. <see cref="EightyFightsEightThreads"/>
/// is eighty fights on eight threads, one hero each, and its time is per fight. Heroes are built before the measurement.
/// </summary>
[MemoryDiagnoser]
public class MonteCarloBenchmarks
{
    private const int Fights = 10;
    private const int Threads = 8;
    private const int ParallelFights = 80;

    private readonly MonteCarloSimulator[] _simulators = new MonteCarloSimulator[Fights];
    private MonteCarloSimulator _parallel = null!;
    private AplActionLists _actions = null!;

    [Params(1, 8)]
    public int Targets { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _actions = Fight.Actions();
        _parallel = new MonteCarloSimulator(Fight.NewHero, _actions);
        // Builds the simulator's eight heroes.
        _parallel.Run(Fight.Duration, ParallelFights, Targets, degreeOfParallelism: Threads);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        for (int i = 0; i < Fights; i++) _simulators[i] = new MonteCarloSimulator(Fight.NewHero, _actions);
    }

    [Benchmark]
    [InvocationCount(1)]
    public double MonteCarloFight() =>
        _simulators[0].Run(Fight.Duration, iterations: 1, targetCount: Targets, degreeOfParallelism: 1).TotalMeanDamage;

    [Benchmark(OperationsPerInvoke = Fights)]
    [InvocationCount(1)]
    public double TenFightsNewHeroes()
    {
        double damage = 0;
        foreach (var simulator in _simulators)
        {
            damage += simulator.Run(Fight.Duration, iterations: 1, targetCount: Targets, degreeOfParallelism: 1).TotalMeanDamage;
        }
        return damage;
    }

    [Benchmark(OperationsPerInvoke = Fights)]
    [InvocationCount(1)]
    public double TenFightsOneHero() =>
        _simulators[0].Run(Fight.Duration, iterations: Fights, targetCount: Targets, degreeOfParallelism: 1).TotalMeanDamage;

    [Benchmark(OperationsPerInvoke = ParallelFights)]
    [InvocationCount(1)]
    public double EightyFightsEightThreads() =>
        _parallel.Run(Fight.Duration, ParallelFights, Targets, degreeOfParallelism: Threads).TotalMeanDamage;
}

/// <summary>
/// One hero imported from the fixture's build: its class, talents, gear and abilities. Monte Carlo builds one per thread.
/// </summary>
[MemoryDiagnoser]
public class HeroBenchmarks
{
    [Benchmark]
    public Hero ImportHero() => Fight.NewHero();
}

/// <summary>
/// One free-dice run of a replay script (the path in <see cref="ScriptVariable"/>), on one thread: a new hero, then the
/// replay with the engine's own dice.
/// </summary>
[MemoryDiagnoser]
public class FreeDiceBenchmarks
{
    /// <summary>The environment variable with the path of a .replay.json script.</summary>
    public const string ScriptVariable = "SIMFELL_BENCH_REPLAY";

    public static string? ScriptPath =>
        Environment.GetEnvironmentVariable(ScriptVariable) is { Length: > 0 } path && File.Exists(path) ? path : null;

    private ReplayScript _script = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _script = ReplayScript.Load(ScriptPath ?? throw new InvalidOperationException($"{ScriptVariable} names no file"));
        // The first hero sets up the shared class catalog.
        _ = ReplayDriver.CreateHero(_script);
    }

    [Benchmark]
    public double FreeDiceRun() => Run(_script, seed: 1);

    public static double Run(ReplayScript script, int seed)
    {
        var hero = ReplayDriver.CreateHero(script);
        var options = new ReplayOptions { FreeDiceSeed = seed, StopOnFirstFailure = false, ForceCasts = true };
        return new ReplayDriver(hero, script, options).Run().EngineTotalDamage;
    }
}
