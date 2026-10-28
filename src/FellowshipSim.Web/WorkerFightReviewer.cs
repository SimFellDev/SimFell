using System.Text.Json;
using FellowshipSim.Engine;
using FellowshipSim.Engine.Jobs;
using FellowshipSim.Logs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Web;

/// <summary>
/// The browser app's replays: a sim worker downloads the fight with the visitor's API client and replays it, and only the
/// review comes back. A fight is hundreds of MB once read, more than the page has room for beside the app.
/// </summary>
public sealed class WorkerFightReviewer(SimWorkerPool workers, LogsSettingsStore settings) : IFightReviewer
{
    public async Task<ReviewArtifacts> ReviewAsync(string reportCode, int fightId, int playerId, SimJobContext context)
    {
        var credentials = settings.Credentials
            ?? throw new FellowshipLogsException("Set your FellowshipLogs API client ID and secret to download fights.");
        string json = await workers.ReviewFightAsync(reportCode, fightId, playerId, credentials, context);
        var reply = JsonSerializer.Deserialize<ReviewReply>(json, SimChunks.Json)!;
        return reply.Review ?? throw new SimEngineException(reply.Error ?? "The replay gave no review.");
    }
}
