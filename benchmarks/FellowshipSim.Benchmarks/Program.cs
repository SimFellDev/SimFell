using System.Diagnostics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using FellowshipSim.Benchmarks;
using FellowshipSim.Core.Engine;
using FellowshipSim.Logs.Replay;

// counters: the work done in one fight. sequence <targets>: fights one after another on one hero, with each one's time
// and work. profile <mc|mc8|freedice> <seconds>: fights back to back on one thread, for a trace.
// Anything else goes to BenchmarkDotNet (e.g. --filter *MonteCarlo*).
switch (args.FirstOrDefault())
{
    case "counters":
        Counters.Run();
        return;
    case "sequence":
        Counters.FightSequence(int.Parse(args.ElementAtOrDefault(1) ?? "1"));
        return;
    case "profile":
        Profile(args.ElementAtOrDefault(1) ?? "mc", double.Parse(args.ElementAtOrDefault(2) ?? "30", System.Globalization.CultureInfo.InvariantCulture));
        return;
}

var types = new List<Type>
{
    typeof(MonteCarloBenchmarks), typeof(FreeDiceBenchmarks),
    typeof(AplDecisionBenchmarks), typeof(AuraBenchmarks), typeof(HasteAuraBenchmarks), typeof(HitBenchmarks), typeof(EventQueueBenchmarks),
};
if (FreeDiceBenchmarks.ScriptPath == null)
{
    Console.WriteLine($"{FreeDiceBenchmarks.ScriptVariable} names no replay script; the free-dice benchmark is skipped.");
    types.Remove(typeof(FreeDiceBenchmarks));
}

// The same garbage collector as the tests and the server: server GC, not concurrent. A job given on the command line
// (--job dry) replaces it.
IConfig config = args.Any(a => a is "--job" or "-j")
    ? DefaultConfig.Instance
    : DefaultConfig.Instance.AddJob(Job.Default.WithGcServer(true).WithGcConcurrent(false));
BenchmarkSwitcher.FromTypes(types.ToArray()).Run(args, config);

static void Profile(string workload, double seconds)
{
    var clock = Stopwatch.StartNew();
    int fights = 0;
    switch (workload)
    {
        case "mc":
        case "mc8":
            int targets = workload == "mc8" ? 8 : 1;
            var hero = Fight.NewHero();
            var simulator = new MonteCarloSimulator(hero, Fight.Actions());
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                simulator.Run(Fight.Duration, iterations: 10, targetCount: targets);
                fights += 10;
            }
            break;
        case "freedice":
            var script = ReplayScript.Load(FreeDiceBenchmarks.ScriptPath
                ?? throw new InvalidOperationException($"{FreeDiceBenchmarks.ScriptVariable} names no replay script"));
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                FreeDiceBenchmarks.Run(script, seed: 1 + fights);
                fights++;
            }
            break;
        default:
            throw new ArgumentException($"Unknown profile workload '{workload}' (mc, mc8 or freedice)");
    }
    Console.WriteLine($"{workload}: {fights} fights in {clock.Elapsed.TotalSeconds:F1} s ({clock.Elapsed.TotalMilliseconds / fights:F2} ms per fight)");
}
