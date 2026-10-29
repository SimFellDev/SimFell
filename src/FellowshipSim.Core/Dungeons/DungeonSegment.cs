namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// One segment of a dungeon fight, <see cref="LengthSeconds"/> long: its enemies arrive as it starts, their health falls
/// with its time, and they die as it ends. A trash pull has <see cref="Enemies"/> enemies, <see cref="Priority"/> of them
/// priority targets. A boss segment has one boss and a council has <see cref="Enemies"/> bosses; <see cref="Final"/> marks
/// the final boss of the dungeon. Each enemy is a <see cref="Creature"/> when the segment names one, and gives that
/// creature's Spirit Points (<see cref="Core.Enemies.CreatureSpiritPoints"/>); otherwise it gives none, as a training mob.
/// </summary>
public sealed record DungeonSegment(
    DungeonSegmentKind Kind,
    double LengthSeconds,
    int Enemies = 1,
    int Priority = 0,
    bool Final = false,
    string? Creature = null)
{
    /// <summary>The number of bosses of a new council.</summary>
    public const int CouncilBosses = 3;

    /// <summary>True for a boss or council segment.</summary>
    public bool IsBoss => Kind != DungeonSegmentKind.Trash;

    /// <summary>The enemies in the segment: 1 for a boss.</summary>
    public int EnemyCount => Kind == DungeonSegmentKind.Boss ? 1 : Enemies;

    /// <summary>The problem with the segment's values, or null when they are valid.</summary>
    public string? Problem() => this switch
    {
        _ when !(LengthSeconds > 0) => "the length is more than 0 seconds",
        { Kind: not DungeonSegmentKind.Boss, Enemies: < 1 } => "the segment has at least 1 enemy",
        { Kind: DungeonSegmentKind.Trash } when Priority < 0 || Priority > Enemies => "the priority targets are 0 to the enemy count",
        { Kind: not DungeonSegmentKind.Trash, Priority: not 0 } => "only a trash pull has priority targets",
        { Kind: DungeonSegmentKind.Trash, Final: true } => "only a boss or a council is the final boss",
        _ => null,
    };
}
