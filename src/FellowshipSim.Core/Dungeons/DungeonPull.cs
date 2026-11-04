namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// Pull <see cref="Name"/> of a dungeon: its enemy <see cref="Groups"/>, whose health falls from full to none over the
/// pull's <see cref="DurationSeconds"/>. It starts <see cref="DowntimeSeconds"/> after the pull before it ends, or as pull
/// <see cref="ChainWith"/> ends. <see cref="Join"/> takes another pull's enemies in. <see cref="Final"/> marks the
/// dungeon's final boss. <see cref="Comment"/> is the '#' lines above the pull.
/// </summary>
public sealed record DungeonPull(
    string Name,
    IReadOnlyList<EnemyGroup> Groups,
    double DurationSeconds,
    double DowntimeSeconds = 0,
    string? ChainWith = null,
    bool Final = false,
    PullJoin? Join = null,
    string? Comment = null)
{
    /// <summary>True when <paramref name="name"/> is a pull name: letters, digits and '_'.</summary>
    public static bool IsName(string name) => name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    /// <summary>True when a group is a boss.</summary>
    public bool IsBoss => Groups.Any(g => g.Boss);

    /// <summary>The enemies of the pull.</summary>
    public int EnemyCount => Groups.Sum(g => g.Count);

    /// <summary>The problem with the pull's own values, or null when they are valid.</summary>
    public string? Problem()
    {
        if (!IsName(Name)) return "the pull name has letters, digits and '_' only";
        if (Groups.Count == 0) return "the pull has at least one enemy group";
        foreach (var group in Groups)
        {
            if (group.Problem() is { } problem) return $"{group.Name}: {problem}";
        }
        if (!(DurationSeconds > 0)) return "the duration is more than 0 seconds";
        if (!(DowntimeSeconds >= 0)) return "the downtime is 0 seconds or more";
        if (Final && !IsBoss) return "only a pull with a boss is the final boss";
        if (Join?.Problem() is { } joinProblem) return joinProblem;
        return null;
    }
}
