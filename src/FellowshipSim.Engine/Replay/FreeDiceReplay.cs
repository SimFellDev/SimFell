using System.Diagnostics;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// One damage source in a free-dice run: the log's damage, hits and crits against the engine's average over the runs
/// (with the spread of its damage).
/// </summary>
public sealed record FreeDiceAbility(
    int Id, string Name,
    double LogDamage, int LogHits, int LogCrits,
    double EngineMean, double Engine5, double Engine95, double EngineHits, double EngineCrits);

/// <summary>
/// "Replay with free dice": the logged fight (casts, enemies, situational auras) replayed many times while the engine
/// rolls its own crits, procs and damage spread. The spread of the engine's totals shows whether the log's real damage is
/// a plausible outcome of the engine's random model; a replay that follows the log's dice cannot show that.
/// </summary>
public static class FreeDiceReplay
{
    public sealed record Result(
        string PlayerName, string FightName, double LogDamage, IReadOnlyList<double> EngineTotals,
        double MeanForcedCasts, TimeSpan Elapsed, IReadOnlyList<FreeDiceAbility> Abilities)
    {
        public double Mean => EngineTotals.Average();
        public double StdDev => Math.Sqrt(EngineTotals.Sum(t => (t - Mean) * (t - Mean)) / Math.Max(1, EngineTotals.Count - 1));
        public double Percentile(double p) => FreeDiceReplay.Percentile(EngineTotals, p);

        /// <summary>The share of runs that dealt less than the log (0.5 = the log is the typical outcome).</summary>
        public double LogPercentile => EngineTotals.Count(t => t < LogDamage) / (double)EngineTotals.Count;

        public FreeDiceSummary ToSummary() =>
            new(EngineTotals.Count, LogDamage, Mean, Percentile(0.05), Percentile(0.95), LogPercentile, DateTime.UtcNow);

        public override string ToString() =>
            $"{PlayerName} - {FightName}: {EngineTotals.Count} runs in {Elapsed.TotalSeconds:F1}s " +
            $"({Elapsed.TotalMilliseconds / EngineTotals.Count:F0} ms/run)\n" +
            $"  log damage    {LogDamage,12:N0}\n" +
            $"  engine mean   {Mean,12:N0}  ({Mean / LogDamage:P1} of log)\n" +
            $"  engine stddev {StdDev,12:N0}  ({StdDev / Mean:P1})\n" +
            $"  engine 5-95%  {Percentile(0.05),12:N0} - {Percentile(0.95):N0}\n" +
            $"  log sits at the {LogPercentile:P0} point of the engine's runs\n" +
            $"  casts forced to follow the log (engine short of resources): {MeanForcedCasts:F1} per run";
    }

    private sealed class Tally
    {
        public string Name = "";
        public double Damage;
        public int Hits;
        public int Crits;
    }

    /// <summary>
    /// Runs <paramref name="iterations"/> replays, in parallel across the CPU's cores (each run has its own hero and
    /// engine; run i uses seed <paramref name="firstSeed"/> + i, so results do not depend on the thread count).
    /// <paramref name="maxThreads"/> caps the threads (null: every core).
    /// </summary>
    public static Result Run(ReplayScript script, int iterations, int firstSeed = 1, int? maxThreads = null)
    {
        var totals = new double[iterations];
        var forced = new int[iterations];
        var perRun = new Dictionary<int, Tally>[iterations];
        var clock = Stopwatch.StartNew();
        // Building one hero first sets up the shared class catalog before the threads start.
        _ = ReplayDriver.CreateHero(script);
        Parallel.For(0, iterations, new ParallelOptions { MaxDegreeOfParallelism = maxThreads ?? -1 }, i =>
        {
            var hero = ReplayDriver.CreateHero(script);
            var tallies = new Dictionary<int, Tally>();
            hero.OnDamageDealt += (in dmg) =>
            {
                if (!tallies.TryGetValue(dmg.AbilityId, out var t)) tallies[dmg.AbilityId] = t = new Tally { Name = dmg.AbilityName };
                t.Damage += dmg.Amount;
                t.Hits++;
                if (dmg.IsCrit) t.Crits++;
            };
            var options = new ReplayOptions { FreeDiceSeed = firstSeed + i, StopOnFirstFailure = false, ForceCasts = true };
            var result = new ReplayDriver(hero, script, options).Run();
            totals[i] = result.EngineTotalDamage;
            forced[i] = result.ForcedCasts.Count;
            perRun[i] = tallies;
        });
        clock.Stop();
        double logDamage = script.Observations.Damage.Sum(d => d.TotalAmount);
        return new Result(script.Player.Name, script.FightName, logDamage, totals, forced.Average(), clock.Elapsed,
            Abilities(script, perRun));
    }

    /// <summary>Every damage source in the log or the engine, the biggest (by log damage, then engine) first.</summary>
    private static List<FreeDiceAbility> Abilities(ReplayScript script, Dictionary<int, Tally>[] perRun)
    {
        var log = script.Observations.Damage.GroupBy(d => d.AbilityId).ToDictionary(g => g.Key, g => g.ToList());
        var ids = log.Keys.Union(perRun.SelectMany(r => r.Keys)).ToList();
        int runs = perRun.Length;
        var rows = new List<FreeDiceAbility>();
        foreach (int id in ids)
        {
            var hits = log.GetValueOrDefault(id) ?? [];
            var engine = perRun.Select(r => r.GetValueOrDefault(id)).ToList();
            var damage = engine.Select(t => t?.Damage ?? 0.0).ToList();
            string name = hits.FirstOrDefault()?.AbilityName ?? engine.FirstOrDefault(t => t != null)?.Name ?? id.ToString();
            rows.Add(new FreeDiceAbility(id, name,
                hits.Sum(h => h.TotalAmount), hits.Count, hits.Count(h => h.Crit),
                damage.Average(), Percentile(damage, 0.05), Percentile(damage, 0.95),
                engine.Sum(t => t?.Hits ?? 0) / (double)runs, engine.Sum(t => t?.Crits ?? 0) / (double)runs));
        }
        return rows.OrderByDescending(r => Math.Max(r.LogDamage, r.EngineMean)).ToList();
    }

    private static double Percentile(IReadOnlyList<double> values, double p)
    {
        var sorted = values.OrderBy(t => t).ToList();
        return sorted[(int)Math.Clamp(Math.Round(p * (sorted.Count - 1)), 0, sorted.Count - 1)];
    }
}
