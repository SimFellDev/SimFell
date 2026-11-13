using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.Core.Engine;

/// <summary>
/// How each fight of a sim runs. A fight is a <see cref="Dungeon"/>'s pulls, or training dummies for the whole sim duration
/// when <see cref="Dungeon"/> is null. <see cref="LengthVariance"/> spreads a value over ± that share of it (0.2: 240 to
/// 360 s of a 300 s sim), the same for a seed in every run: the training dummies' fight length, or a dungeon pull's
/// <see cref="DungeonPull.DurationSeconds"/> and its groups' <see cref="EnemyGroup.FirstSeenSeconds"/> and
/// <see cref="EnemyGroup.DiesSeconds"/>. A dungeon fight lasts until its last pull ends, or to its time limit.
/// <see cref="HealthFollowsTime"/> takes the training dummies from full health at the start to none at the fight's end,
/// whatever damage they take, so health-gated effects come at their share of the fight. <see cref="StartingSpirit"/> is the
/// hero's Spirit Points as the fight starts. <see cref="Boss"/> is true for training dummies that are bosses: without one, the
/// hero's <see cref="Domain.Hero.BossAuras"/> are off. In a dungeon they are on while a boss is in combat.
/// With <see cref="ReactionDelays"/> the hero takes a player's
/// time to cast once the global cooldown, a cast or a channel lets them (<see cref="Engine.ReactionDelays.Measured"/>).
/// <see cref="ProcReactionSeconds"/> is the time a proc's gain takes to reach the action lists (<see cref="Domain.Aura.Seen"/>).
/// </summary>
public sealed record FightOptions(double LengthVariance = 0, bool HealthFollowsTime = false, double StartingSpirit = 0, bool Boss = true,
    DungeonProfile? Dungeon = null, bool ReactionDelays = false, double ProcReactionSeconds = 0)
{
    public static readonly FightOptions Default = new();

    /// <summary>
    /// The length of the fight with seed <paramref name="seed"/> in a sim of <paramref name="duration"/>; a dungeon fight's
    /// time limit.
    /// </summary>
    public SimTime Length(SimTime duration, int seed)
    {
        double seconds = BaseSeconds(duration);
        if (LengthVariance <= 0 || Dungeon != null) return SimTime.FromSeconds(seconds);
        return SimTime.FromSeconds(Math.Max(1.0, seconds * VarianceScale(seed)));
    }

    /// <summary>The share of its base value a varied value has in the fight with seed <paramref name="seed"/>: 1 ± <see cref="LengthVariance"/>.</summary>
    public double VarianceScale(int seed) => LengthVariance <= 0 ? 1.0 : 1 + LengthVariance * (2 * Uniform(seed) - 1);

    /// <summary>
    /// SplitMix64 of the seed: a uniform share in [0, 1) that leaves the fight's own dice alone.
    /// </summary>
    private static double Uniform(int seed)
    {
        ulong z = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        return (z >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>The longest fight of a sim of <paramref name="duration"/>: a dungeon's time limit.</summary>
    public SimTime LongestLength(SimTime duration) => Dungeon != null
        ? SimTime.FromSeconds(Dungeon.TimeLimitSeconds)
        : SimTime.FromSeconds(BaseSeconds(duration) * (1 + Math.Max(0, LengthVariance)));

    /// <summary>A fight's length before variance: the dungeon's time limit, or <paramref name="duration"/> for training dummies.</summary>
    public double BaseSeconds(SimTime duration) => Dungeon?.TimeLimitSeconds ?? duration.TotalSeconds;
}
