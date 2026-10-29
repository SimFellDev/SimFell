using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.Core.Engine;

/// <summary>
/// How each fight of a sim runs. A fight is a <see cref="Dungeon"/>'s segments in turn, or training mobs for the whole
/// sim duration when <see cref="Dungeon"/> is null. <see cref="LengthVariance"/> spreads the fights' lengths evenly over
/// the duration (a dungeon's length) ± that share (0.2: 240 to 360 s of a 300 s sim), the same for a seed in every run; a
/// dungeon's segments each take their share of the fight's length. <see cref="HealthFollowsTime"/> takes the training
/// mobs from full health at the start to none at the fight's end, whatever damage they take, so health-gated effects come
/// at their share of the fight; a dungeon's enemies always follow their segment's time. <see cref="StartingSpirit"/> is
/// the hero's Spirit Points as the fight starts. <see cref="Boss"/> is true for training mobs that are bosses: without
/// one, the hero's <see cref="Domain.Hero.BossAuras"/> are off. In a dungeon they are on in boss and council segments.
/// </summary>
public sealed record FightOptions(double LengthVariance = 0, bool HealthFollowsTime = false, double StartingSpirit = 0, bool Boss = true,
    DungeonProfile? Dungeon = null)
{
    public static readonly FightOptions Default = new();

    /// <summary>The length of the fight with seed <paramref name="seed"/> in a sim of <paramref name="duration"/>.</summary>
    public SimTime Length(SimTime duration, int seed)
    {
        double seconds = BaseSeconds(duration);
        if (LengthVariance <= 0) return SimTime.FromSeconds(seconds);
        // SplitMix64 of the seed: a uniform share in [0, 1) that leaves the fight's own dice alone.
        ulong z = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        double u = (z >> 11) * (1.0 / (1UL << 53));
        return SimTime.FromSeconds(Math.Max(1.0, seconds * (1 + LengthVariance * (2 * u - 1))));
    }

    /// <summary>The longest fight of a sim of <paramref name="duration"/>.</summary>
    public SimTime LongestLength(SimTime duration) => SimTime.FromSeconds(BaseSeconds(duration) * (1 + Math.Max(0, LengthVariance)));

    /// <summary>A fight's length before variance: the dungeon's, or <paramref name="duration"/> for training mobs.</summary>
    public double BaseSeconds(SimTime duration) => Dungeon?.LengthSeconds ?? duration.TotalSeconds;
}
