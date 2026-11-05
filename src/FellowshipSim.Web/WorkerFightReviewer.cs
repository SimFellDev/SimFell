using FellowshipSim.Engine;
using FellowshipSim.Engine.Jobs;
using FellowshipSim.Logs;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Web;

/// <summary>
/// The browser app's replays: a sim worker downloads the fight as the signed-in user (or with a developer's API client)
/// and replays it, and only the
/// review comes back. A fight is hundreds of MB once read, more than the page has room for beside the app.
/// </summary>
public sealed class WorkerFightReviewer(SimWorkerPool workers, LogsSettingsStore settings, LogsSignIn signIn) : IFightReviewer
{
    public async Task<ReviewArtifacts> ReviewAsync(string reportCode, int fightId, int playerId, SimJobContext context)
    {
        string? token = await signIn.AccessTokenAsync(context.CancellationToken);
        var credentials = token == null ? settings.Credentials : null;
        if (token == null && credentials == null) throw new FellowshipLogsException("Sign in with FellowshipLogs to download fights.");
        var reply = ReviewReply.FromBytes(await workers.ReviewFightAsync(reportCode, fightId, playerId, token, credentials, context));
        return reply.Review ?? throw new SimEngineException(reply.Error ?? "The replay gave no review.");
    }
}
