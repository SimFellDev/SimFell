namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// Pull <see cref="Pull"/>'s enemies join the pull with this join, at one trigger at most:
/// <list type="bullet">
/// <item><see cref="OffsetMs"/> milliseconds after the pull starts;</item>
/// <item>as the pull's own enemies' average health falls to <see cref="OffsetPct"/> percent;</item>
/// <item>as <see cref="AfterDeaths"/> of the pull's own enemies are dead;</item>
/// <item>as <see cref="AfterDeathsTotal"/> of the enemies in combat for the pull are dead, pulls joined before included.</item>
/// </list>
/// With no trigger the enemies join as the pull starts. An untriggered join triggers as the pull's last enemy dies.
/// </summary>
public sealed record PullJoin(int Pull, double? OffsetMs = null, double? OffsetPct = null, DeathCount? AfterDeaths = null,
    DeathCount? AfterDeathsTotal = null)
{
    /// <summary>The number of triggers set.</summary>
    public int Triggers => (OffsetMs != null ? 1 : 0) + (OffsetPct != null ? 1 : 0) + (AfterDeaths != null ? 1 : 0) + (AfterDeathsTotal != null ? 1 : 0);

    /// <summary>The problem with the join's trigger, or null when it is valid.</summary>
    public string? Problem() => this switch
    {
        { Triggers: > 1 } => "pull_with has one trigger at most (offset_ms, offset_pct, after_deaths, after_deaths_total)",
        { OffsetMs: { } ms } when !(ms >= 0) => "offset_ms is 0 or more",
        { OffsetPct: { } pct } when !(pct > 0 && pct < 100) => "offset_pct is more than 0 and less than 100",
        { AfterDeaths: { } deaths } when deaths.Problem() is { } problem => $"after_deaths: {problem}",
        { AfterDeathsTotal: { } deaths } when deaths.Problem() is { } problem => $"after_deaths_total: {problem}",
        _ => null,
    };
}
