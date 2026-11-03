using System.Collections.Concurrent;

namespace FellowshipSim.Core.Domain;

/// <summary>
/// A proc with bad luck protection: its chance is a step times the rolls since it last procced (the first roll after a
/// proc has one step), so it procs at the latest once the step reaches 100%. <see cref="Hero.RollBadLuckProc"/> rolls it.
/// Combat logs (Tariq's Schism, 17 fights, 5265 rolls): the rate after 0, 1 and 2 misses in a row is 16-19%, 31-35% and
/// 44-46%, one step of 15.8% each, the step whose average chance is the tooltip's 35%.
/// </summary>
public static class BadLuckProtection
{
    private static readonly ConcurrentDictionary<double, double> Steps = new();

    /// <summary>The step whose average chance per roll is <paramref name="averageChance"/> (0.35 gives 0.158).</summary>
    public static double StepFor(double averageChance) => Steps.GetOrAdd(averageChance, average =>
    {
        double low = 0.0, high = 1.0;
        for (int i = 0; i < 60; i++)
        {
            double mid = (low + high) / 2;
            if (AverageChance(mid) < average) low = mid;
            else high = mid;
        }
        return (low + high) / 2;
    });

    /// <summary>The average chance per roll of a proc whose chance is <paramref name="step"/> times the rolls since it procced.</summary>
    public static double AverageChance(double step)
    {
        double none = 1.0, rollsPerProc = 0.0;
        for (int n = 1; none > 0.0; n++)
        {
            double chance = Math.Min(1.0, n * step);
            rollsPerProc += n * none * chance;
            none *= 1.0 - chance;
        }
        return 1.0 / rollsPerProc;
    }
}
