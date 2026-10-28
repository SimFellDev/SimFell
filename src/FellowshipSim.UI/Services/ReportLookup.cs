using FellowshipSim.Engine;
using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>A FellowshipLogs report's fights and players. Each player is marked by whether the engine can replay its hero.</summary>
public sealed class ReportLookup(ReplayLibrary library, LogsSettingsStore settings, ISimEngine engine)
{
    /// <summary>
    /// The report of a FellowshipLogs link or report code. Throws <see cref="ArgumentException"/> for text that is no link
    /// or code, and <see cref="FellowshipLogsException"/> when no credentials are set or FellowshipLogs refuses the request.
    /// </summary>
    public async Task<Report> FindAsync(string link, CancellationToken ct = default)
    {
        var parsed = ReportLink.Parse(link);
        var client = await settings.CreateClientAsync(ct)
                     ?? throw new FellowshipLogsException("Sign in with FellowshipLogs first.");
        var summary = await client.GetSummaryAsync(parsed.ReportCode, ct);
        var heroes = (await engine.GetHeroesAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var downloaded = library.ListDownloadedFights().Where(f => f.ReportCode == parsed.ReportCode).Select(f => f.FightId).ToHashSet();
        return new Report(
            summary.Code,
            summary.Title,
            DateTimeOffset.FromUnixTimeMilliseconds(summary.StartTime).UtcDateTime,
            parsed.FightId,
            parsed.PlayerId,
            summary.Fights.Select(f => new ReportFight(
                f.Id, f.Name, f.Kill, f.DurationSeconds, downloaded.Contains(f.Id),
                f.Players.Select(p => new FightPlayer(p.Id, p.Name, p.Hero, heroes.Contains(p.Hero))).ToList())).ToList(),
            summary.LogVersion);
    }
}
