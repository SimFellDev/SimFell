using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.Engine;

/// <summary>
/// A Monte Carlo sim's fights: <see cref="Iterations"/> fights of <see cref="Dungeon"/>'s pulls, or, when it is null,
/// of <see cref="DurationSeconds"/> against <see cref="TargetCount"/> training dummies. Fight i has seed
/// <see cref="FirstSeed"/> + i. <see cref="DegreeOfParallelism"/> is the most threads the fights use; null is
/// <see cref="MonteCarloSimulator.DefaultDegreeOfParallelism"/>. <see cref="LengthVariance"/>,
/// <see cref="HealthFollowsTime"/>, <see cref="StartingSpirit"/>, <see cref="Boss"/>, <see cref="Dungeon"/>,
/// <see cref="ReactionDelays"/> and <see cref="ProcReactionSeconds"/> are each fight's
/// <see cref="FightOptions"/>; targets lose their health over each fight, the hero takes a player's reaction time, and a
/// proc reaches the action lists <see cref="ProcReactionSeconds"/> after its gain unless told otherwise, as the Sim page has them.
/// </summary>
public sealed record SimSettings(int Iterations, double DurationSeconds, int TargetCount = 1, int FirstSeed = 1, int? DegreeOfParallelism = null,
    double LengthVariance = 0, bool HealthFollowsTime = true, double StartingSpirit = 0, bool Boss = true, DungeonProfile? Dungeon = null,
    bool ReactionDelays = true, double ProcReactionSeconds = SimSettings.DefaultProcReactionSeconds)
{
    /// <summary>SimulationCraft's player reaction_mean.</summary>
    public const double DefaultProcReactionSeconds = 0.3;

    [System.Text.Json.Serialization.JsonIgnore]
    public FightOptions Fight => new(LengthVariance, HealthFollowsTime, StartingSpirit, Boss, Dungeon, ReactionDelays, ProcReactionSeconds);
}
