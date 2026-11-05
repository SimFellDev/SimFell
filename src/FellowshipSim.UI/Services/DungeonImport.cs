using System.Text.Json;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// The dungeon of a logged run: the fight's enemy deaths and one player's damage events, without the fight's other events
/// (<see cref="FellowshipLogsReports.GetDungeonFightAsync"/>), read by <see cref="DungeonReader"/>.
/// </summary>
public sealed class DungeonImport(LogsSettingsStore settings)
{
    /// <summary>
    /// The dungeon of the fight a FellowshipLogs link names (<c>?fight=N</c>), and whether its report is from the patch the
    /// engine models (<see cref="GamePatch.Current"/>; true when FellowshipLogs returns no report start). Throws
    /// <see cref="ArgumentException"/> for a link with no fight or a fight with no dungeon to read, and
    /// <see cref="FellowshipLogsException"/> when FellowshipLogs cannot be asked or returns no fight.
    /// </summary>
    public async Task<(DungeonProfile Dungeon, bool CurrentPatch)> ReadAsync(string link, CancellationToken ct = default)
    {
        var parsed = ReportLink.Parse(link);
        int fightId = parsed.FightId ?? throw new ArgumentException("The link names no fight: open the fight on FellowshipLogs and copy its link.");
        var client = await settings.CreateClientAsync(ct) ?? throw new FellowshipLogsException("Sign in with FellowshipLogs first.");
        var fight = await client.GetDungeonFightAsync(parsed.ReportCode, fightId, ct);
        bool current = fight["report_start_time"]?.GetValue<double?>() is not { } start
            || GamePatch.Current.Covers(DateTimeOffset.FromUnixTimeMilliseconds((long)start).UtcDateTime, fight["log_version"]?.GetValue<int?>());
        using var document = JsonSerializer.SerializeToDocument(fight);
        return (DungeonReader.FromFight(document.RootElement), current);
    }
}
