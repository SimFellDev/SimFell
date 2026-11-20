namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// Pull <see cref="Name"/> of a dungeon: its enemy <see cref="Groups"/>. Each group's enemies enter combat at the pull's
/// start or the group's <see cref="EnemyGroup.FirstSeenSeconds"/> after it, and their health falls from full to none by the
/// pull's <see cref="DurationSeconds"/> or the group's <see cref="EnemyGroup.DiesSeconds"/>. The pull starts
/// <see cref="DowntimeSeconds"/> after the pull before it ends, or as pull <see cref="ChainWith"/> ends. <see cref="Join"/>
/// takes another pull's enemies in. <see cref="Final"/> marks the dungeon's final boss. <see cref="Comment"/> is the '#'
/// lines above the pull.
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

    /// <summary>
    /// The seconds after the pull's start at which <paramref name="group"/>'s enemies die: its
    /// <see cref="EnemyGroup.DiesSeconds"/>, or <see cref="DurationSeconds"/>.
    /// </summary>
    public double DiesSeconds(EnemyGroup group) => group.DiesSeconds ?? DurationSeconds;

    /// <summary>The seconds from the pull's start to its last enemy's death.</summary>
    public double EndSeconds => Groups.Count > 0 ? Groups.Max(DiesSeconds) : DurationSeconds;

    /// <summary>The seconds the pull's enemies are in combat, summed over the enemies.</summary>
    public double EnemySeconds => Groups.Sum(g => g.Count * (DiesSeconds(g) - g.FirstSeenSeconds));

    /// <summary>
    /// The share of <see cref="DurationSeconds"/> after which the pull's enemies' average health is
    /// <paramref name="percent"/> of full. An enemy still to enter combat is at full health.
    /// </summary>
    public double DurationShareAtHealth(double percent)
    {
        double target = percent / 100.0;
        // The average health falls in a straight line between two shares at which a group enters combat or dies.
        var shares = Groups.SelectMany(g => new[] { g.FirstSeenSeconds / DurationSeconds, DiesSeconds(g) / DurationSeconds })
            .Distinct().Order().ToList();
        double from = shares[0], health = AverageHealth(from);
        foreach (double to in shares.Skip(1))
        {
            double next = AverageHealth(to);
            if (next <= target) return from + (to - from) * (health - target) / (health - next);
            (from, health) = (to, next);
        }
        return shares[^1];
    }

    // The enemies' average health, 1 for full, at a share of the duration.
    private double AverageHealth(double share)
    {
        double health = 0;
        foreach (var group in Groups)
        {
            double enters = group.FirstSeenSeconds / DurationSeconds, dies = DiesSeconds(group) / DurationSeconds;
            health += group.Count * Math.Clamp(1 - (share - enters) / (dies - enters), 0, 1);
        }
        return health / EnemyCount;
    }

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
        foreach (var group in Groups)
        {
            if (group.DiesSeconds > DurationSeconds) return $"{group.Name}: dies is at most the pull's duration";
            if (group.DiesSeconds == null && !(group.FirstSeenSeconds < DurationSeconds))
                return $"{group.Name}: first_seen is less than the pull's duration";
        }
        if (!(DowntimeSeconds >= 0)) return "the downtime is 0 seconds or more";
        if (Final && !IsBoss) return "only a pull with a boss is the final boss";
        if (Join?.Problem() is { } joinProblem) return joinProblem;
        return null;
    }
}
