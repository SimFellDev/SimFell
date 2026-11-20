namespace FellowshipSim.Core.Engine;

/// <summary>
/// A sim's fights by DPS: <see cref="Counts"/> are the fights in each of equal bins from <see cref="Min"/> to
/// <see cref="Max"/>, the lowest and highest fight's DPS. The last bin includes <see cref="Max"/>.
/// </summary>
public sealed record DpsDistribution(double Min, double Max, IReadOnlyList<int> Counts)
{
    /// <summary>Bins of a sim's distribution.</summary>
    public const int Bins = 50;

    /// <summary>
    /// The distribution of <paramref name="fightDps"/>, each fight's DPS; null for no fights. Fights of one DPS fill one bin.
    /// </summary>
    public static DpsDistribution? Of(IReadOnlyList<double> fightDps)
    {
        if (fightDps.Count == 0) return null;
        double min = fightDps.Min(), max = fightDps.Max();
        int bins = max > min ? Bins : 1;
        var counts = new int[bins];
        foreach (double dps in fightDps) counts[Bin(dps, min, max, bins)]++;
        return new DpsDistribution(min, max, counts);
    }

    /// <summary>The bin of <paramref name="dps"/>.</summary>
    public int BinOf(double dps) => Bin(dps, Min, Max, Counts.Count);

    private static int Bin(double dps, double min, double max, int bins) =>
        max > min ? Math.Clamp((int)((dps - min) / (max - min) * bins), 0, bins - 1) : 0;
}
