namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// <see cref="Count"/> enemies of one kind in a pull, each with the values here: <see cref="Name"/>, the
/// <see cref="SpiritPoints"/> it is worth and <see cref="HitPoints"/>, its health before the difficulty's scale. Each is the
/// game data value of the one creature named <see cref="Name"/> when null (<see cref="Spirit"/> and <see cref="Health"/>).
/// The health falls over the pull's <see cref="DungeonPull.DurationSeconds"/>. A <see cref="Boss"/> group is one boss.
/// <see cref="Priority"/> marks priority targets. <see cref="Cast"/> is the interruptible cast of each enemy.
/// <see cref="Comment"/> is the '#' lines above the group's line.
/// </summary>
public sealed record EnemyGroup(
    int Count,
    string Name,
    double? SpiritPoints = null,
    bool Priority = false,
    double? HitPoints = null,
    bool Boss = false,
    EnemyCast? Cast = null,
    string? Comment = null)
{
    /// <summary>
    /// Each enemy's health before the difficulty's scale: <see cref="HitPoints"/>, or the <see cref="CodexCreature.BaseHealth"/>
    /// of the one creature named <see cref="Name"/>. Null when neither has one.
    /// </summary>
    public double? Health => HitPoints ?? GameCreatures.ByName(Name)?.BaseHealth;

    /// <summary>
    /// The Spirit Points each enemy is worth: <see cref="SpiritPoints"/>, or the <see cref="CodexCreature.SpiritPoints"/> of the
    /// one creature named <see cref="Name"/>, or 0.
    /// </summary>
    public double Spirit => SpiritPoints ?? GameCreatures.ByName(Name)?.SpiritPoints ?? 0;

    /// <summary>The problem with the group's values, or null when they are valid.</summary>
    public string? Problem() => this switch
    {
        _ when string.IsNullOrWhiteSpace(Name) => "the enemy has a name",
        { Boss: true, Count: not 1 } => "a boss line is one boss",
        { Count: < 1 } => "the count is at least 1",
        { SpiritPoints: { } spirit } when !(spirit >= 0) => "the Spirit Points are 0 or more",
        { HitPoints: { } hp } when !(hp > 0) => "the hit points are more than 0",
        { Health: null } => "the enemy has hit_points, or the name of one creature of the game data",
        { Cast: { } cast } when cast.Problem() is { } problem => problem,
        _ => null,
    };
}
