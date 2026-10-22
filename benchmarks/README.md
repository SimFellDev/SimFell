# Benchmarks

`FellowshipSim.Benchmarks` has the engine's [BenchmarkDotNet](https://benchmarkdotnet.org) benchmarks. Every benchmark
reports its time and the bytes it allocates.

The fight is the test fixture's Rime (`tests/FellowshipSim.Tests/Fixtures/rime_character.json`) with the action list in
`FellowshipSim.Benchmarks/Fixtures/rime.simc`, against training mobs. The benchmarks run with server GC, not
concurrent, as the tests and the server do.

| Class | Benchmark |
|---|---|
| `MonteCarloBenchmarks` | A 300 s Monte Carlo fight at 1 and 8 targets: one fight on a new hero, ten fights each on a new hero, and ten fights on one hero. |
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

This prints one fight's counts at 1 and 8 targets, and for the free-dice script when `SIMFELL_BENCH_REPLAY` is set:
casts, decisions, fixed 0.1 s idle steps, aura changes, cooldown states, hits, `OnDamageDealt` subscribers and events
scheduled. A stage's time per fight is its micro-benchmark time multiplied by its count.

```
bin/Release/net10.0/FellowshipSim.Benchmarks sequence 8
```

This runs twelve fights one after another on one hero at 8 targets (or the number given), with each fight's time,
damage, casts, hits, events and hasted timers. Each fight has the same seed, so each one has the damage, casts, hits and events of the first.

## Profiles

`profile <mc|mc8|freedice> <seconds>` runs fights back to back on one thread for that long. `mc` is 1 target and `mc8`
is 8 targets. Record a trace of it with `dotnet-trace` (`dotnet tool install -g dotnet-trace`). On Windows,
`dotnet-trace` needs the program's full path with backslashes:

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
