using System.Text.Json;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Engine;
using FellowshipSim.Logs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Web;

/// <summary>
/// The browser app's dungeon imports: a sim worker downloads the fight as the signed-in user (or with a developer's API
/// client), or takes the one the browser keeps, and reads its dungeon, and only the dungeon text comes back.
/// </summary>
public sealed class WorkerDungeonImport(SimWorkerPool workers, LogsSettingsStore settings, LogsSignIn signIn) : IDungeonImport
{
    public async Task<DungeonProfile> ReadAsync(string reportCode, int fightId, int playerId, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        string? token = await signIn.AccessTokenAsync(ct);
        var credentials = token == null ? settings.Credentials : null;
        string json = await workers.DungeonFightAsync(reportCode, fightId, playerId, token, credentials, progress);
        var reply = JsonSerializer.Deserialize<DungeonReply>(json, SimChunks.Json)!;
        return reply.Text is { } text ? DungeonProfile.Parse(text) : throw new ArgumentException(reply.Error ?? "The fight gave no dungeon.");
    }
}
