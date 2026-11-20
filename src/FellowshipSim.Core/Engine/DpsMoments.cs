namespace FellowshipSim.Core.Engine;

/// <summary>
/// Fights' DPS: their count, mean and sum of squared deviations from the mean (Welford's), and the margin of error of the
/// mean at 95% confidence. A run's chunks merge in chunk order (<see cref="Merge"/>), so the moments of a run are the same
/// to the last bit on any number of threads and in any process.
/// </summary>
public readonly record struct DpsMoments(long Count, double Mean, double M2)
{
    /// <summary>The two-sided normal quantile of 95% confidence, SimulationCraft's default <c>confidence</c>.</summary>
    public const double Z95 = 1.96;

    /// <summary>Fights before a run's margin of error ends it: SimulationCraft's <c>analyze_error_interval</c>.</summary>
    public const int TargetErrorMinimumFights = 100;

    /// <summary>The fights' DPS's standard deviation; 0 for no fights.</summary>
    public double StandardDeviation => Count > 0 ? Math.Sqrt(M2 / Count) : 0;

    /// <summary>The margin of error of the mean DPS at 95% confidence, in DPS.</summary>
    public double Margin => MarginOf(StandardDeviation, Count);

    /// <summary>The margin of error of the mean DPS at 95% confidence, in percent of the mean; infinite for a mean of 0.</summary>
    public double MarginPercent => MarginPercentOf(StandardDeviation, Count, Mean);

    /// <summary>The margin of error at 95% confidence of the mean of <paramref name="count"/> values, in their unit.</summary>
    public static double MarginOf(double standardDeviation, long count) => count > 0 ? Z95 * standardDeviation / Math.Sqrt(count) : 0;

    /// <summary><see cref="MarginOf"/> in percent of <paramref name="mean"/>; infinite for a mean of 0.</summary>
    public static double MarginPercentOf(double standardDeviation, long count, double mean) =>
        mean > 0 && count > 0 ? MarginOf(standardDeviation, count) / mean * 100 : double.PositiveInfinity;

    /// <summary>
    /// True when a run with <paramref name="targetError"/> (percent of the mean; 0 for none) ends at these fights: at
    /// least <see cref="TargetErrorMinimumFights"/>, with a margin of error below the target.
    /// </summary>
    public bool Reached(double targetError) => targetError > 0 && Count >= TargetErrorMinimumFights && MarginPercent < targetError;

    /// <summary>These fights and <paramref name="other"/>'s, after them (Chan's parallel formula).</summary>
    public DpsMoments Merge(DpsMoments other)
    {
        if (other.Count == 0) return this;
        long count = Count + other.Count;
        double delta = other.Mean - Mean;
        return new DpsMoments(count, Mean + delta * other.Count / count, M2 + other.M2 + delta * delta * ((double)Count * other.Count / count));
    }
}
