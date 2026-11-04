# Benchmarks

`FellowshipSim.Benchmarks` has the engine's [BenchmarkDotNet](https://benchmarkdotnet.org) benchmarks. Every benchmark
reports its time and the bytes it allocates.

Monte Carlo is the workload the engine is tuned for: gear and talent comparisons run it for up to millions of
iterations. Free dice is an accuracy check against logged fights, so it has one benchmark: a single run.

The fight is the test fixture's Rime (`tests/FellowshipSim.Tests/Fixtures/rime_character.json`) with the action list in
`FellowshipSim.Benchmarks/Fixtures/rime.simc`, against training dummies. The benchmarks run with server GC, not
concurrent, as the tests and the server do.

The action list that idles is `rime.simc` without Frost Bolt, the action that is always ready. The hero is then free
with nothing to cast whenever every other action waits for a cooldown, a resource or a condition.

| Class | Benchmark |
|---|---|
| `MonteCarloBenchmarks` | A 300 s Monte Carlo fight at 1 and 8 targets: one fight on a new hero, ten fights each on a new hero, and ten fights on one hero, all on one thread; eighty fights on eight threads. |
| `IdleMonteCarloBenchmarks` | Ten 300 s fights on one hero and one thread at 1 and 8 targets, with the action list that idles. |
| `HeroBenchmarks` | One hero imported from the fixture's build. |
| `FreeDiceBenchmarks` | One free-dice run of a replay script on one thread. |
| `AplDecisionBenchmarks` | One evaluation of every action in the list on a hero 60 s into a fight. |
| `AuraBenchmarks` | One aura applied and removed on a hero 60 s into a fight, with a stat read after each change. |
| `HasteAuraBenchmarks` | The same with a haste aura, so every hasted timer is updated. |
| `HitBenchmarks` | One hit: Frost Bolt's `DealDamage`, `ApplyDamage` with fixed numbers, and the `OnDamageDealt` fan-out alone. |
| `EventQueueBenchmarks` | Events scheduled, cancelled and dequeued; a periodic effect started and stopped. |

## Run them

Build in Release, then run the program from its project folder. BenchmarkDotNet looks for the project from the
current folder.

```
dotnet build benchmarks/FellowshipSim.Benchmarks -c Release
cd benchmarks/FellowshipSim.Benchmarks

# everything
bin/Release/net10.0/FellowshipSim.Benchmarks --filter *

# one class
bin/Release/net10.0/FellowshipSim.Benchmarks --filter *MonteCarlo*
```

Results are in `BenchmarkDotNet.Artifacts/` (not committed). `--artifacts <folder>` puts them elsewhere.

## Free dice needs a replay script

Replay scripts are downloaded with the web page and are not committed. Set `SIMFELL_BENCH_REPLAY` to the path of a
`.replay.json` file:

```
# PowerShell
$env:SIMFELL_BENCH_REPLAY = "G:\source\SimFell\data\scripts\yMAZVJL9hTpFjqbC_fight7_p1.replay.json"
# bash
export SIMFELL_BENCH_REPLAY=data/scripts/yMAZVJL9hTpFjqbC_fight7_p1.replay.json
```

Without it, the program prints a line and runs the other benchmarks. A path is passed as an environment variable
because BenchmarkDotNet runs each benchmark in a new process. That process has the environment but not the
arguments.

## Work per fight

```
bin/Release/net10.0/FellowshipSim.Benchmarks counters
```

This prints one fight's counts at 1 and 8 targets, with the fixture's action list and the one that idles, and for the
free-dice script when `SIMFELL_BENCH_REPLAY` is set:
casts, decisions, idle wake-ups (decisions that cast nothing), aura changes, cooldown states, hits, `OnDamageDealt` subscribers and events
scheduled. A stage's time per fight is its micro-benchmark time multiplied by its count.

```
bin/Release/net10.0/FellowshipSim.Benchmarks sequence 8
```

This runs twelve fights one after another on one hero at 8 targets (or the number given), with each fight's time,
damage, casts, hits, events and hasted timers. Each fight has the same seed, so each one has the damage, casts, hits and events of the first.

```
bin/Release/net10.0/FellowshipSim.Benchmarks hero
```

This prints the time of the first hero in a new process (with the catalog's setup and the first compilation of the
import) and the mean time of the next thousand.

## Many iterations at fixed core counts

```
bin/Release/net10.0/FellowshipSim.Benchmarks scale <mc|mc8|mc-idle|mc8-idle|freedice> [iterations] [core counts]

# 100,000 Monte Carlo fights at 1 target on one core
bin/Release/net10.0/FellowshipSim.Benchmarks scale mc 100000 1
```

This runs `iterations` (default 100,000) through `MonteCarloSimulator.Run` (`mc` is 1 target, `mc8` is 8 targets, and
`mc-idle` and `mc8-idle` have the action list that idles) or
`FreeDiceReplay.Run`, once per core count. A Monte Carlo run has one thread per core, each with its own hero. The mode
prints wall time, CPU time, iterations per second, scaling efficiency,
bytes allocated, GC counts, the share of wall time in GC pauses and the peak working set. The default core counts are
1, 2, 4 and 8; `1,8` runs only those. A run on every logical processor leaves the machine unusable while it runs. A run
whose working set passes 8 GB is stopped.

Each core count runs in its own process. The process is pinned to that many logical processors, and its runtime
processor count and server GC heap count equal the core count, so code that sizes its parallelism from the processor
count runs at that degree of parallelism. The logical processors are taken in this order: one per performance core,
then one per efficiency core, then the second of each performance core. On an i7-14700KF that is 0, 2, 4 ... 14 (the
8 performance cores), 16-27 (the 12 efficiency cores), then 1, 3 ... 15. The program prints the order it found.

Scaling efficiency is iterations per second divided by the core count times the 1-core rate. A run has a fixed cost of
a few seconds at 1 core, so runs much shorter than 100,000 iterations read low at 1 core and high in efficiency.

## Profiles

`profile <mc|mc8|mc-idle|mc8-idle|freedice> <seconds>` runs fights back to back on one thread for that long. `mc` is
1 target and `mc8` is 8 targets; the `-idle` workloads have the action list that idles. Record a trace of it with
`dotnet-trace` (`dotnet tool install -g dotnet-trace`). On Windows, `dotnet-trace` needs the program's full path with
backslashes:

```
# allocations by type
dotnet-trace collect --profile gc-verbose -o mc-gc.nettrace -- <full path>\FellowshipSim.Benchmarks.exe profile mc 30
# stack samples
dotnet-trace collect --profile dotnet-sampled-thread-time,dotnet-common -o mc-cpu.nettrace -- <full path>\FellowshipSim.Benchmarks.exe profile mc 30
```

The stack samples are taken when the runtime suspends threads, so they gather at allocations and calls. Shares of
small methods read high or low; the micro-benchmarks and `counters` give the cost of each stage.

`TraceSummary.cs` prints a trace's top allocating types, the share of CPU samples in each engine stage, and the top
methods:

```
dotnet run benchmarks/TraceSummary.cs -- mc-cpu.nettrace
```

Trace files are not committed.

## A browser worker's memory

`wasm/worker-memory.mjs` runs one sim worker of the browser app in Node: the published app's .NET runtime, started as
`sim/worker.js` starts it, with `SimWorker`'s calls. Publish the app as the Deploy web app workflow does (with AOT), then
give the harness the publish folder and one or more workloads. The AOT publish needs the `wasm-tools` workload in the
SDK that `global.json` selects (`dotnet --version` in the repository), and each publish goes to an empty folder: a
reused one keeps the last build's fingerprinted `dotnet.*.js`.

```
dotnet workload install wasm-tools
dotnet publish src/FellowshipSim.Web -c Release -p:RunAOTCompilation=true -o publish

# a 1,000-fight sim at 1 and at 8 targets, then 100 fights of the default dungeon
node benchmarks/wasm/worker-memory.mjs publish sim:1000 sim:1000:8 dungeon:100

# a replay of a fight the browser keeps, and a first replay with the fight downloaded
node benchmarks/wasm/worker-memory.mjs publish review:data/fights/rf7qpV3tATCx86hN_fight6.raw.json:83
node benchmarks/wasm/worker-memory.mjs publish download:data/fights/rf7qpV3tATCx86hN_fight6.raw.json:83
```

After the start and after each workload it prints the worker's WebAssembly memory, the process's resident memory, the
managed heap after a full collection, the collections so far and the workload's time. A runtime's WebAssembly memory
never shrinks and stops at 2 GB, so each line's WebAssembly memory is the worker's peak so far, and a workload past 2 GB
fails with "Out of memory". The memory grows in steps of 20%.

A `build` workload builds the player's replay script alone (`WorkerMeasures.BuildScript`), without the replay.

A `download` workload has FellowshipLogs' answers from the raw fight, in pages of 10,000 events, through
`WorkerMeasures.ReviewFight`. `WorkerMeasures` is the harness's part of the app: its calls force a full collection, and
the app never makes them. `MONO_GC_PARAMS` in the environment reaches the worker's GC.

Run each workload in a process of its own for its peak: a review after another workload starts from that workload's
memory.
