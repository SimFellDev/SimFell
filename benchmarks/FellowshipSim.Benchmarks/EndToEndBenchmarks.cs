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
/// the first two compare commits before and after it. Heroes are built before the measurement.
/// </summary>
[MemoryDiagnoser]
public class MonteCarloBenchmarks
{
    private const int Fights = 10;

    private readonly Hero[] _heroes = new Hero[Fights];
    private AplActionLists _actions = null!;

    [Params(1, 8)]
    public int Targets { get; set; }

    [GlobalSetup]
    public void GlobalSetup() => _actions = Fight.Actions();

    [IterationSetup]
    public void IterationSetup()
    {
        for (int i = 0; i < Fights; i++) _heroes[i] = Fight.NewHero();
    }

    [Benchmark]
    [InvocationCount(1)]
    public double MonteCarloFight() =>
        new MonteCarloSimulator(_heroes[0], _actions).Run(Fight.Duration, iterations: 1, targetCount: Targets).TotalMeanDamage;

    [Benchmark(OperationsPerInvoke = Fights)]
    [InvocationCount(1)]
    public double TenFightsNewHeroes()
    {
        double damage = 0;
        foreach (var hero in _heroes)
        {
            damage += new MonteCarloSimulator(hero, _actions).Run(Fight.Duration, iterations: 1, targetCount: Targets).TotalMeanDamage;
        }
        return damage;
    }

    [Benchmark(OperationsPerInvoke = Fights)]
    [InvocationCount(1)]
    public double TenFightsOneHero() =>
        new MonteCarloSimulator(_heroes[0], _actions).Run(Fight.Duration, iterations: Fights, targetCount: Targets).TotalMeanDamage;
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
