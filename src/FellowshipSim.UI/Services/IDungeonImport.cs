using FellowshipSim.Core.Dungeons;

namespace FellowshipSim.UI.Services;

/// <summary>
/// Reads the dungeon of a logged run, with one player's hero share (<c>DungeonReader</c>). <see cref="DiskDungeonImport"/>
/// reads the fight downloaded to the data folder; the browser app downloads and reads it on a sim worker, so the fight never
/// sits in the page's memory.
/// </summary>
public interface IDungeonImport
{
    /// <summary>
    /// The dungeon of fight <paramref name="fightId"/> of report <paramref name="reportCode"/> with player
    /// <paramref name="playerId"/>'s hero share. Throws <see cref="FellowshipSim.Logs.FellowshipLogsException"/> when
    /// FellowshipLogs cannot be asked, and <see cref="ArgumentException"/> when the fight has no dungeon to read.
    /// </summary>
    Task<DungeonProfile> ReadAsync(string reportCode, int fightId, int playerId, IProgress<string>? progress = null,
        CancellationToken ct = default);
}
