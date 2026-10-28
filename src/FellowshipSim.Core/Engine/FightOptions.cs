namespace FellowshipSim.Core.Engine;

/// <summary>
/// How each fight of a sim runs. <see cref="LengthVariance"/> spreads the fights' lengths evenly over the duration ± that
/// share (0.2: 240 to 360 s of a 300 s sim), the same for a seed in every run. <see cref="HealthFollowsTime"/> takes the
/// targets from full health at the start to none at the fight's end, whatever damage they take, so health-gated effects
/// come at their share of the fight. <see cref="StartingSpirit"/> is the hero's Spirit Points as the fight starts.
/// <see cref="Boss"/> is true for a fight against a boss: without one, the hero's <see cref="Domain.Hero.BossAuras"/> are off.
/// </summary>
public sealed record FightOptions(double LengthVariance = 0, bool HealthFollowsTime = false, double StartingSpirit = 0, bool Boss = true)
{
    public static readonly FightOptions Default = new();

    /// <summary>The length of the fight with seed <paramref name="seed"/> in a sim of <paramref name="duration"/>.</summary>
    public SimTime Length(SimTime duration, int seed)
    {
        if (LengthVariance <= 0) return duration;
        // SplitMix64 of the seed: a uniform share in [0, 1) that leaves the fight's own dice alone.
        ulong z = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        double u = (z >> 11) * (1.0 / (1UL << 53));
        return SimTime.FromSeconds(Math.Max(1.0, duration.TotalSeconds * (1 + LengthVariance * (2 * u - 1))));
    }

    /// <summary>The longest fight of a sim of <paramref name="duration"/>.</summary>
    public SimTime LongestLength(SimTime duration) =>
        LengthVariance <= 0 ? duration : SimTime.FromSeconds(duration.TotalSeconds * (1 + LengthVariance));
}
