using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.Engine;

/// <summary>
/// A Monte Carlo sim's fights: <see cref="Iterations"/> fights of <see cref="Dungeon"/>'s segments, or, when it is null,
/// of <see cref="DurationSeconds"/> against <see cref="TargetCount"/> training mobs. Fight i has seed
/// <see cref="FirstSeed"/> + i. <see cref="DegreeOfParallelism"/> is the most threads the fights use; null is
/// <see cref="MonteCarloSimulator.DefaultDegreeOfParallelism"/>. <see cref="LengthVariance"/>,
/// <see cref="HealthFollowsTime"/>, <see cref="StartingSpirit"/>, <see cref="Boss"/> and <see cref="Dungeon"/> are each
/// fight's <see cref="FightOptions"/>; targets lose their health over each fight unless told otherwise, as the Sim page has
/// them.
/// </summary>
public sealed record SimSettings(int Iterations, double DurationSeconds, int TargetCount = 1, int FirstSeed = 1, int? DegreeOfParallelism = null,
    double LengthVariance = 0, bool HealthFollowsTime = true, double StartingSpirit = 0, bool Boss = true, DungeonProfile? Dungeon = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public FightOptions Fight => new(LengthVariance, HealthFollowsTime, StartingSpirit, Boss, Dungeon);
}
