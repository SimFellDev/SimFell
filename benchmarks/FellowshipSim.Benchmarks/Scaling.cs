using System.Diagnostics;
using System.Runtime.InteropServices;
using FellowshipSim.Core.Engine;
using FellowshipSim.Logs.Replay;

namespace FellowshipSim.Benchmarks;

/// <summary>
/// Many iterations through the public entry points (<see cref="MonteCarloSimulator.Run"/>, <see cref="FreeDiceReplay.Run"/>)
/// at fixed core counts. Each core count runs in its own process: the process is pinned to those logical processors,
/// and the runtime's processor count and server GC heap count equal the core count. Code that sizes its parallelism from
/// <see cref="Environment.ProcessorCount"/> therefore runs at that degree of parallelism.
/// </summary>
public static class Scaling
{
    private const string RunMode = "scale-run";

    /// <summary>
    /// <c>scale &lt;mc|mc8|freedice&gt; [iterations] [core counts]</c>: one process per core count (default 1, 2, 4 and 8),
    /// then a table.
    /// </summary>
    public static void Run(string[] args)
    {
        string workload = args.ElementAtOrDefault(1) ?? "mc";
        int iterations = int.Parse(args.ElementAtOrDefault(2) ?? "100000");
        var order = ProcessorOrder();
        var counts = args.ElementAtOrDefault(3) is { } list
            ? list.Split(',').Select(int.Parse).ToList()
            : new[] { 1, 2, 4, 8 }.Where(n => n <= order.Count).ToList();

        Console.WriteLine($"{workload}, {iterations:N0} iterations. Logical processors, first chosen first: {string.Join(",", order)}");
        Console.WriteLine("cores  logical processors        wall s    CPU s   iter/s  efficiency  allocated GB   gen0  gen1  gen2  GC pause  peak GB");
        double? single = null;
        foreach (int cores in counts)
        {
            var chosen = order.Take(cores).ToList();
            long mask = chosen.Aggregate(0L, (m, p) => m | (1L << p));
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            foreach (string a in new[] { RunMode, workload, iterations.ToString(), mask.ToString() }) start.ArgumentList.Add(a);
            start.Environment["DOTNET_PROCESSOR_COUNT"] = cores.ToString();
            start.Environment["DOTNET_GCHeapCount"] = cores.ToString("x");
            start.Environment["DOTNET_GCHeapAffinitizeMask"] = mask.ToString("x");
            using var child = Process.Start(start)!;
            var output = child.StandardOutput.ReadToEndAsync();
            var clock = Stopwatch.StartNew();
            long peak = 0;
            while (!child.WaitForExit(1000))
            {
                child.Refresh();
                peak = Math.Max(peak, child.WorkingSet64);
                if (peak <= WorkingSetLimitBytes) continue;
                child.Kill(entireProcessTree: true);
                child.WaitForExit();
                Console.WriteLine($"{cores,5}  {Ranges(chosen),-22} aborted: working set passed {WorkingSetLimitBytes / 1e9:F0} GB after {clock.Elapsed.TotalSeconds:F0} s");
                break;
            }
            if (peak > WorkingSetLimitBytes) continue;
            var m = Measurement.Parse(output.Result.Trim().Split('\n').Last().Trim());
            double rate = iterations / m.WallSeconds;
            single ??= cores == 1 ? rate : null;
            string efficiency = single is { } one ? $"{rate / (one * cores),9:P0}" : "        -";
            Console.WriteLine($"{cores,5}  {Ranges(chosen),-22} {m.WallSeconds,9:F1} {m.CpuSeconds,8:F1} {rate,8:N0}  {efficiency}  {m.AllocatedBytes / 1e9,12:F1} {m.Gen0,6} {m.Gen1,5} {m.Gen2,5} {m.PausePercent,8:P1} {m.PeakWorkingSetBytes / 1e9,8:F2}");
        }
    }

    /// <summary>A run whose working set passes this is stopped (a fight that leaks state into the next grows without bound).</summary>
    private const long WorkingSetLimitBytes = 8_000_000_000;

    /// <summary>The child process: pins itself, runs the workload once and prints one line of measurements.</summary>
    public static void RunChild(string[] args)
    {
        string workload = args[1];
        int iterations = int.Parse(args[2]);
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            Process.GetCurrentProcess().ProcessorAffinity = (nint)long.Parse(args[3]);
        }

        Action work = workload switch
        {
            "mc" or "mc8" => MonteCarlo(iterations, workload == "mc8" ? 8 : 1, System.Numerics.BitOperations.PopCount(ulong.Parse(args[3]))),
            "freedice" => FreeDice(iterations),
            _ => throw new ArgumentException($"Unknown workload '{workload}' (mc, mc8 or freedice)"),
        };

        var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        long allocated = GC.GetTotalAllocatedBytes(precise: true);
        var pause = GC.GetTotalPauseDuration();
        int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
        var clock = Stopwatch.StartNew();
        work();
        clock.Stop();
        process.Refresh();
        var m = new Measurement(
            clock.Elapsed.TotalSeconds,
            (process.TotalProcessorTime - cpu).TotalSeconds,
            GC.GetTotalAllocatedBytes(precise: true) - allocated,
            GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2,
            (GC.GetTotalPauseDuration() - pause).TotalSeconds / clock.Elapsed.TotalSeconds,
            process.PeakWorkingSet64);
        Console.WriteLine(m.ToString());
    }

    /// <summary>One Monte Carlo run of <paramref name="iterations"/> fights on <paramref name="cores"/> threads.</summary>
    private static Action MonteCarlo(int iterations, int targets, int cores)
    {
        var simulator = new MonteCarloSimulator(Fight.NewHero, Fight.Actions());
        return () => simulator.Run(Fight.Duration, iterations, targets, degreeOfParallelism: cores);
    }

    private static Action FreeDice(int iterations)
    {
        var script = ReplayScript.Load(FreeDiceBenchmarks.ScriptPath
            ?? throw new InvalidOperationException($"{FreeDiceBenchmarks.ScriptVariable} names no replay script"));
        return () => FreeDiceReplay.Run(script, iterations);
    }

    private sealed record Measurement(double WallSeconds, double CpuSeconds, long AllocatedBytes, int Gen0, int Gen1, int Gen2, double PausePercent, long PeakWorkingSetBytes)
    {
        public override string ToString() =>
            FormattableString.Invariant($"{WallSeconds:R} {CpuSeconds:R} {AllocatedBytes} {Gen0} {Gen1} {Gen2} {PausePercent:R} {PeakWorkingSetBytes}");

        public static Measurement Parse(string line)
        {
            var p = line.Split(' ');
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return new(double.Parse(p[0], c), double.Parse(p[1], c), long.Parse(p[2], c), int.Parse(p[3], c), int.Parse(p[4], c), int.Parse(p[5], c), double.Parse(p[6], c), long.Parse(p[7], c));
        }
    }

    /// <summary>
    /// Logical processors in the order core counts take them: one logical processor of each performance core, then one of
    /// each efficiency core, then the second logical processor of each core (Windows CPU sets; other systems: 0, 1, 2...).
    /// </summary>
    private static List<int> ProcessorOrder()
    {
        if (!OperatingSystem.IsWindows()) return Enumerable.Range(0, Environment.ProcessorCount).ToList();
        GetSystemCpuSetInformation(IntPtr.Zero, 0, out int length, IntPtr.Zero, 0);
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!GetSystemCpuSetInformation(buffer, length, out length, IntPtr.Zero, 0)) throw new InvalidOperationException("GetSystemCpuSetInformation failed");
            var sets = new List<(int Logical, int Core, int Efficiency)>();
            for (int offset = 0; offset < length;)
            {
                int size = Marshal.ReadInt32(buffer, offset);
                // SYSTEM_CPU_SET_INFORMATION: LogicalProcessorIndex at 14, CoreIndex at 15, EfficiencyClass at 18.
                sets.Add((Marshal.ReadByte(buffer, offset + 14), Marshal.ReadByte(buffer, offset + 15), Marshal.ReadByte(buffer, offset + 18)));
                offset += size;
            }
            return sets
                .GroupBy(s => s.Core)
                .SelectMany(core => core.OrderBy(s => s.Logical).Select((s, i) => (s.Logical, Thread: i, s.Efficiency, s.Core)))
                .OrderBy(s => s.Thread).ThenByDescending(s => s.Efficiency).ThenBy(s => s.Core)
                .Select(s => s.Logical)
                .ToList();
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    /// <summary>"0,2,4,6" as "0-6 step 2"; every logical processor as "all"; any other set as its list.</summary>
    private static string Ranges(List<int> processors)
    {
        if (processors.Count == Environment.ProcessorCount && processors.Count > 2) return $"all {processors.Count}";
        if (processors.Count <= 2) return string.Join(",", processors);
        int step = processors[1] - processors[0];
        bool even = step > 0 && processors.Zip(processors.Skip(1)).All(p => p.Second - p.First == step);
        return even ? $"{processors[0]}-{processors[^1]}" + (step > 1 ? $" step {step}" : "") : string.Join(",", processors);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemCpuSetInformation(IntPtr information, int bufferLength, out int returnedLength, IntPtr process, uint flags);
}
