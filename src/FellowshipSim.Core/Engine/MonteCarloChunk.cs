namespace FellowshipSim.Core.Engine;

/// <summary>
/// One chunk of a Monte Carlo run's fights (<see cref="MonteCarloSimulator.RunChunk"/>): its running totals, which
/// <see cref="MonteCarloSimulator.Combine"/> merges in chunk order to the run's result. Every member survives a JSON
/// round trip to the last bit (with named floating-point literals allowed: an empty chunk's Min and Max are infinite), so
/// a run's chunks can run in other processes, such as the browser app's workers, and still give the same result.
/// </summary>
public sealed class MonteCarloChunk
{
    public int Index { get; init; }

    /// <summary>Fights, and the running mean and sum of squared deviations of their DPS (Welford's).</summary>
    public long Count { get; init; }
    public double Mean { get; init; }
    public double M2 { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }

    /// <summary>The fights' damage and lengths in seconds, added up.</summary>
    public double DamageSum { get; init; }
    public double LengthSum { get; init; }

    public Dictionary<string, double> AbilityDamage { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each of the chunk's fights' damage, in fight order, when the run keeps it; else empty.</summary>
    public double[] FightDamage { get; init; } = [];

    /// <summary>The fights' report: damage over time, sources, auras and the sampled fights.</summary>
    public ReportTotals Report { get; init; } = new();

    public sealed class ReportTotals
    {
        public int Fights { get; init; }
        public double[] DamageSum { get; init; } = [];
        public List<BandFight> Band { get; init; } = [];
        public List<SampleFight> Samples { get; init; } = [];
        public double[] SpiritSum { get; init; } = [];
        public int[] SpiritFights { get; init; } = [];
        public double SpiritMax { get; init; }
        /// <summary>Each damage source, in the order the fights first met it.</summary>
        public List<SourceTotals> Sources { get; init; } = [];
        /// <summary>Each aura, in the order the fights first met it.</summary>
        public List<AuraTotals> Auras { get; init; } = [];
        public double PrimaryDamage { get; init; }
        public double BossDamage { get; init; }
        public double BossSeconds { get; init; }
        public double TargetSeconds { get; init; }
    }

    /// <summary>One of the first fights' damage each second, for the band of the damage over time.</summary>
    public sealed record BandFight(int Fight, float[] Damage);

    /// <summary>A sampled fight: its damage, aura spans, casts and Spirit Points, for the typical fight.</summary>
    public sealed record SampleFight(int Fight, double Damage, List<AuraSpans> Spans, List<CastReport> Casts, double[] Spirit);

    public sealed record AuraSpans(string Name, bool OnTarget, List<double> Spans);

    public sealed record SourceTotals(string Name, double Damage, double Crits, long Casts, long Hits, int Id);

    public sealed record AuraTotals(string Name, bool OnTarget, double UpSeconds, double StackSeconds, double AtMaxSeconds,
        double GapSeconds, long Applications, long Refreshes, long Gaps, int MaxStacks, int StackCap, int LogId);
}
