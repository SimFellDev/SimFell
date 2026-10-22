#:package Microsoft.Diagnostics.Tracing.TraceEvent@3.1.19

// Summarises a dotnet-trace .nettrace file: allocations by type (GCAllocationTick samples, one per ~100 KB allocated)
// and the share of CPU samples in each engine stage.
// Run with: dotnet run benchmarks/TraceSummary.cs -- <trace.nettrace>

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

if (args.Length < 1)
{
    Console.WriteLine("usage: dotnet run TraceSummary.cs -- <trace.nettrace> [top]");
    return;
}
int top = args.Length > 1 ? int.Parse(args[1]) : 20;

// Stage of a method, matched on its full name. Closures carry their method's name ("<ApplyDamage>g__Land"), so a
// substring match counts them with their method. A sample belongs to the stage of the innermost frame that matches
// one ("innermost"); "inclusive" counts every sample with a frame of the stage anywhere on its stack.
// Casting is the action list's cast and the ability's own effects: the work a decision starts, not the decision. It
// comes first, since AplAction.Execute is also in the action list's namespace.
(string Stage, string[] Patterns)[] stages =
[
    ("Casting", ["AplAction.Execute", "SimulationContext.BeginCast", "SimulationContext.FinishCast", "Ability.ResolveCast"]),
    ("A: action list", ["FellowshipSim.Core.APL."]),
    ("B: auras, stats, cooldowns", ["AuraContainer", "CooldownState", "ModifierFold", "HastedTimer.Update", "Hero.CooldownRate", "Hero.AdvanceCooldowns",
        "Hero.GetHaste", "Hero.CritChance", "Hero.PrimaryAttribute", "Hero.Expertise", "Hero.DamageScale", "Hero.CritMultiplier", "Hero.SpiritPercent"]),
    ("C: hit pipeline", ["SimulationContext.ApplyDamage", "<ApplyDamage>", "Ability.DealDamage", "<DealDamage>", "Ability.DealAoeDamage",
        "Hero.TriggerDamageDealt", "OutcomeSource.ResolveHit", "OutcomeSource.Land", "Ability.BaseDamage",
        "(class FellowshipSim.Core.Domain.DamageContext)"]),
    ("D: event queue", ["SimulationKernel.", "PriorityQueue", "ScheduledEvent", "HastedTimer.Reschedule", "HastedTimer.Start",
        "PeriodicEffect.Begin(", "PeriodicEffect.BeginHasted("]),
    ("Replay driver", ["FellowshipSim.Logs."]),
];

string etlx = TraceLog.CreateFromEventPipeDataFile(args[0]);
using var log = new TraceLog(etlx);

var allocated = new Dictionary<string, long>();
var allocatedAt = new Dictionary<string, long>();
long allocatedTotal = 0;
var inclusive = new long[stages.Length];
var innermost = new long[stages.Length];
var leaf = new Dictionary<string, long>();
var methodInclusive = new Dictionary<string, long>();
long engineSamples = 0;

foreach (TraceEvent ev in log.Events)
{
    if (ev is GCAllocationTickTraceData tick)
    {
        string type = tick.TypeName ?? "?";
        allocated[type] = allocated.GetValueOrDefault(type) + tick.AllocationAmount64;
        allocatedTotal += tick.AllocationAmount64;
        // A closure's type name ("<>c__DisplayClass62_0") does not name its class, so the allocating engine method does.
        string? site = null;
        for (var frame = ev.CallStack(); frame != null && site == null; frame = frame.Caller)
        {
            string? method = frame.CodeAddress.FullMethodName;
            if (method != null && method.Contains("FellowshipSim.")) site = method;
        }
        string key = $"{type}  <-  {site ?? "?"}";
        allocatedAt[key] = allocatedAt.GetValueOrDefault(key) + tick.AllocationAmount64;
        continue;
    }
    if (ev.ProviderName != "Microsoft-DotNETCore-SampleProfiler" || ev.EventName != "Thread/Sample") continue;

    var frames = new List<string>();
    for (var frame = ev.CallStack(); frame != null; frame = frame.Caller)
    {
        frames.Add(frame.CodeAddress.FullMethodName ?? frame.CodeAddress.ModuleName ?? "?");
    }
    // Only samples of the engine's own thread: a stack with an engine method on it.
    if (!frames.Any(f => f.Contains("FellowshipSim."))) continue;
    engineSamples++;
    leaf[frames[0]] = leaf.GetValueOrDefault(frames[0]) + 1;
    foreach (string method in frames.Distinct()) methodInclusive[method] = methodInclusive.GetValueOrDefault(method) + 1;

    bool attributed = false;
    var seen = new bool[stages.Length];
    foreach (string frame in frames)
    {
        for (int s = 0; s < stages.Length; s++)
        {
            if (!stages[s].Patterns.Any(frame.Contains)) continue;
            if (!attributed) { innermost[s]++; attributed = true; }
            seen[s] = true;
        }
    }
    for (int s = 0; s < stages.Length; s++) if (seen[s]) inclusive[s]++;
}

Console.WriteLine($"Allocations: {allocatedTotal / 1048576.0:N0} MB sampled");
foreach (var (type, bytes) in allocated.OrderByDescending(kv => kv.Value).Take(top))
{
    Console.WriteLine($"  {bytes * 100.0 / Math.Max(1, allocatedTotal),5:F1}%  {bytes / 1048576.0,8:N1} MB  {type}");
}

Console.WriteLine();
Console.WriteLine("Allocations by type and the engine method allocating it:");
foreach (var (key, bytes) in allocatedAt.OrderByDescending(kv => kv.Value).Take(top))
{
    Console.WriteLine($"  {bytes * 100.0 / Math.Max(1, allocatedTotal),5:F1}%  {key}");
}

Console.WriteLine();
Console.WriteLine($"CPU:{engineSamples} samples on the engine thread");
Console.WriteLine("  stage                          inclusive  innermost");
for (int s = 0; s < stages.Length; s++)
{
    Console.WriteLine($"  {stages[s].Stage,-30} {inclusive[s] * 100.0 / Math.Max(1, engineSamples),8:F1}%  {innermost[s] * 100.0 / Math.Max(1, engineSamples),8:F1}%");
}
long none = engineSamples - innermost.Sum();
Console.WriteLine($"  {"(no stage)",-30} {"",9}  {none * 100.0 / Math.Max(1, engineSamples),8:F1}%");

Console.WriteLine();
Console.WriteLine("Top methods by own samples:");
foreach (var (method, n) in leaf.OrderByDescending(kv => kv.Value).Take(top))
{
    Console.WriteLine($"  {n * 100.0 / Math.Max(1, engineSamples),5:F1}%  {method}");
}

Console.WriteLine();
Console.WriteLine("Top engine methods by inclusive samples:");
foreach (var (method, n) in methodInclusive.Where(kv => kv.Key.Contains("FellowshipSim.")).OrderByDescending(kv => kv.Value).Take(top * 2))
{
    Console.WriteLine($"  {n * 100.0 / Math.Max(1, engineSamples),5:F1}%  {method}");
}
