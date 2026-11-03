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
        var fight = await library.GetRawFightDocumentAsync(client, reportCode, fightId, progress, ct);
        if (!DungeonReader.HasPulls(fight.RootElement))
        {
            fight.Dispose();
            client ??= await settings.CreateClientAsync(ct)
                ?? throw new FellowshipLogsException("Sign in with FellowshipLogs to read the fight's pulls.");
            progress?.Report("Downloading the fight's pulls from FellowshipLogs");
            // The pulls go into the fight's tree, which reads no event, and the fight is saved with them.
            var raw = await library.GetRawFightAsync(null, reportCode, fightId, progress, ct);
            await client.AddDungeonPullsAsync(raw, ct);
            await library.SaveRawFightAsync(reportCode, fightId, raw, ct);
            fight = await library.GetRawFightDocumentAsync(null, reportCode, fightId, progress, ct);
        }
        using (fight)
        {
            progress?.Report("Reading the dungeon");
            return DungeonReader.FromRawFight(fight.RootElement, playerId);
        }
    }
}
