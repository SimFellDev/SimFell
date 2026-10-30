using FellowshipSim.Core.Dungeons;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;

namespace FellowshipSim.UI.Services;

/// <summary>
/// The fight downloaded to the data folder (<see cref="ReplayLibrary"/>, a minute or more the first time; kept for its
/// replays), then read by <see cref="DungeonReader"/>. A fight downloaded without its pulls gets them from FellowshipLogs.
/// </summary>
public sealed class DiskDungeonImport(ReplayLibrary library, LogsSettingsStore settings) : IDungeonImport
{
    public async Task<DungeonProfile> ReadAsync(string reportCode, int fightId, int playerId, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var client = library.HasRawFight(reportCode, fightId) ? null : await settings.CreateClientAsync(ct);
        var raw = await library.GetRawFightAsync(client, reportCode, fightId, progress, ct);
        if (!DungeonReader.HasPulls(raw))
        {
            client ??= await settings.CreateClientAsync(ct)
                ?? throw new FellowshipLogsException("Sign in with FellowshipLogs to read the fight's pulls.");
            progress?.Report("Downloading the fight's pulls from FellowshipLogs");
            await client.AddDungeonPullsAsync(raw, ct);
            await library.SaveRawFightAsync(reportCode, fightId, raw, ct);
        }
        progress?.Report("Reading the dungeon");
        return DungeonReader.FromRawFight(raw, playerId);
    }
}
