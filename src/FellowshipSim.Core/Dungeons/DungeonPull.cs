namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// Pull <see cref="Number"/> of a dungeon: its enemy <see cref="Groups"/>. It starts <see cref="DowntimeSeconds"/> after the
/// pull before it ends, or as pull <see cref="ChainWith"/> ends. <see cref="Join"/> takes another pull's enemies in.
/// <see cref="PullDurationSeconds"/> is the time over which the health of enemies without hit points falls.
/// <see cref="Final"/> marks the dungeon's final boss.
/// </summary>
public sealed record DungeonPull(
    int Number,
    IReadOnlyList<EnemyGroup> Groups,
    double? PullDurationSeconds = null,
    double DowntimeSeconds = 0,
    int? ChainWith = null,
    bool Final = false,
    PullJoin? Join = null)
{
    /// <summary>True when a group is a boss.</summary>
    public bool IsBoss => Groups.Any(g => g.Boss);

    /// <summary>The enemies of the pull.</summary>
    public int EnemyCount => Groups.Sum(g => g.Count);

    /// <summary>The problem with the pull's own values, or null when they are valid.</summary>
    public string? Problem()
    {
        if (Number < 1) return "the pull number is at least 1";
        if (Groups.Count == 0) return "the pull has at least one enemy group";
        foreach (var group in Groups)
        {
            if (group.Problem() is { } problem) return $"{group.Name}: {problem}";
        }
        if (PullDurationSeconds is { } duration && !(duration > 0)) return "the pull duration is more than 0 seconds";
        if (PullDurationSeconds == null && Groups.Any(g => g.HitPoints == null))
            return "a group without hit points needs the pull's pull_duration";
        if (!(DowntimeSeconds >= 0)) return "the downtime is 0 seconds or more";
        if (Final && !IsBoss) return "only a pull with a boss is the final boss";
        if (Join?.Problem() is { } joinProblem) return joinProblem;
        return null;
    }
}
