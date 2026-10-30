namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// <see cref="Count"/> enemies of one kind in a pull, each with the values here: <see cref="Name"/>, the
/// <see cref="SpiritPoints"/> it is worth and <see cref="HitPoints"/> at <see cref="DungeonProfile.BaseDifficulty"/>. An enemy
/// with hit points dies from damage. An enemy without them has health falling over the pull's
/// <see cref="DungeonPull.PullDurationSeconds"/>. A <see cref="Boss"/> group is one boss. <see cref="Priority"/> marks
/// priority targets. <see cref="Cast"/> is the interruptible cast of each enemy.
/// </summary>
public sealed record EnemyGroup(
    int Count,
    string Name,
    double SpiritPoints = 0,
    bool Priority = false,
    double? HitPoints = null,
    bool Boss = false,
    EnemyCast? Cast = null)
{
    /// <summary>The problem with the group's values, or null when they are valid.</summary>
    public string? Problem() => this switch
    {
        _ when string.IsNullOrWhiteSpace(Name) => "the enemy has a name",
        { Boss: true, Count: not 1 } => "a boss line is one boss",
        { Count: < 1 } => "the count is at least 1",
        _ when !(SpiritPoints >= 0) => "the Spirit Points are 0 or more",
        { HitPoints: { } hp } when !(hp > 0) => "the hit points are more than 0",
        { Cast: { } cast } when cast.Problem() is { } problem => problem,
        _ => null,
    };
}
