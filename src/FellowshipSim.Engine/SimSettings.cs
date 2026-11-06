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
/// proc reaches the action lists 0.5 s after its gain unless told otherwise, as the Sim page has them.
/// <see cref="RealPpmFirstProcFloor"/> is the fights' <see cref="FightOptions.RealPpmFirstProcFloor"/>; null is on for a
/// dungeon and off for training dummies, whose fixed-length fights the floor would favour.
/// </summary>
public sealed record SimSettings(int Iterations, double DurationSeconds, int TargetCount = 1, int FirstSeed = 1, int? DegreeOfParallelism = null,
    double LengthVariance = 0, bool HealthFollowsTime = true, double StartingSpirit = 0, bool Boss = true, DungeonProfile? Dungeon = null,
    bool ReactionDelays = true, double ProcReactionSeconds = SimSettings.DefaultProcReactionSeconds, bool? RealPpmFirstProcFloor = null)
{
    /// <summary>SimulationCraft's player reaction_mean: 0.3 s.</summary>
    public const double DefaultProcReactionSeconds = 0.3;

    [System.Text.Json.Serialization.JsonIgnore]
    public FightOptions Fight => new(LengthVariance, HealthFollowsTime, StartingSpirit, Boss, Dungeon, ReactionDelays, ProcReactionSeconds,
        FirstProcFloor);

    /// <summary><see cref="RealPpmFirstProcFloor"/>, or its default: on for a dungeon, off for training dummies.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool FirstProcFloor => RealPpmFirstProcFloor ?? Dungeon != null;
}
