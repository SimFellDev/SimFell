using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;

namespace FellowshipSim.Web;

/// <summary>
/// What a sim worker (sim/worker.js) runs: each worker is a .NET runtime of its own, started from this app's assemblies
/// without the page. A sim's chunks (<see cref="SimChunks"/>) and a replay review run here, off the page's one thread.
/// </summary>
public static partial class SimWorker
{
    // The sims this worker has chunks of, by the page's run ID.
    private static readonly Dictionary<int, SimChunks> Runs = new();

    /// <summary>Readies the worker for chunks of run <paramref name="runId"/>.</summary>
    [JSExport]
    public static void Start(int runId, string profileJson, string settingsJson) =>
        Runs[runId] = new SimChunks(Read<SimcProfile>(profileJson), Read<SimSettings>(settingsJson));

    /// <summary>Fights chunk <paramref name="chunk"/> of run <paramref name="runId"/>; its totals as JSON.</summary>
    [JSExport]
    public static string Chunk(int runId, int chunk) => JsonSerializer.Serialize(Runs[runId].Run(chunk), SimChunks.Json);

    [JSExport]
    public static void Finish(int runId) => Runs.Remove(runId);

    /// <summary>A sim's result from every one of its chunks, in chunk order, with its typical fight's hits, as JSON.</summary>
    [JSExport]
    public static string Combine(string profileJson, string settingsJson, string[] chunksJson) =>
        JsonSerializer.Serialize(new SimChunks(Read<SimcProfile>(profileJson), Read<SimSettings>(settingsJson))
            .Combine(chunksJson.Select(Read<MonteCarloChunk>).ToList()), SimChunks.Json);

    /// <summary>
    /// Replays player <paramref name="playerId"/> of a raw fight: a <see cref="ReviewReply"/> as JSON, with the review's files
    /// or the reason the engine refused it. Its progress goes to the page as call <paramref name="callId"/>'s.
    /// </summary>
    [JSExport]
    public static async Task<string> Review(string rawFight, int playerId, int callId)
    {
        ReviewReply reply;
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawFight));
            var review = await new LocalSimEngine().ReviewAsync(stream, playerId, new ProgressTo(callId));
            reply = new ReviewReply(review, null);
        }
        catch (SimEngineException e)
        {
            reply = new ReviewReply(null, e.Message);
        }
        return JsonSerializer.Serialize(reply, SimChunks.Json);
    }

    /// <summary>
    /// Downloads fight <paramref name="fightId"/> of report <paramref name="reportCode"/> from FellowshipLogs with the
    /// signed-in user's token (<paramref name="accessToken"/>), else a developer's API client, and replays player
    /// <paramref name="playerId"/>: a <see cref="ReviewReply"/> as JSON. The fight
    /// is kept in the browser's storage for the next replay of it (sim/fights.js), never in the page's memory.
    /// </summary>
    [JSExport]
    public static async Task<string> ReviewFight(string reportCode, int fightId, int playerId, string accessToken, string clientId,
        string clientSecret, int callId)
    {
        var progress = new ProgressTo(callId);
        ReviewReply reply;
        try
        {
            using var http = new HttpClient();
            progress.Report("Downloading the fight from FellowshipLogs");
            var client = accessToken.Length > 0 ? FellowshipLogsClient.ForUser(http, accessToken) : new FellowshipLogsClient(http, clientId, clientSecret);
            // The fight is replayed from its text's JsonDocument; a long fight's events as a tree are more than a worker has.
            string text = (await client.DownloadFightAsync(reportCode, fightId, progress)).ToJsonString();
            await SaveFight(reportCode, fightId, text);
            progress.Report("Replaying the fight");
            using var fight = JsonDocument.Parse(text);
            reply = new ReviewReply(ReplayReview.Run(fight.RootElement, playerId, progress: progress), null);
        }
        catch (Exception e) when (e is FellowshipLogsException or HttpRequestException or NotSupportedException or ArgumentException)
        {
            reply = new ReviewReply(null, e.Message);
        }
        return JsonSerializer.Serialize(reply, SimChunks.Json);
    }

    /// <summary>
    /// Reads the dungeon of fight <paramref name="fightId"/> of report <paramref name="reportCode"/> with player
    /// <paramref name="playerId"/>'s hero share: a <see cref="DungeonReply"/> as JSON. The fight is
    /// <paramref name="keptFight"/>, the one the browser keeps, or else downloaded as <see cref="ReviewFight"/> does and kept.
    /// A fight kept without its pulls gets them from FellowshipLogs and is kept again.
    /// </summary>
    [JSExport]
    public static async Task<string> ReadDungeonFight(string reportCode, int fightId, int playerId, string keptFight, string accessToken,
        string clientId, string clientSecret, int callId)
    {
        var progress = new ProgressTo(callId);
        DungeonReply reply;
        try
        {
            using var http = new HttpClient();
            FellowshipLogsClient Client() =>
                accessToken.Length > 0 ? FellowshipLogsClient.ForUser(http, accessToken)
                : clientId.Length > 0 ? new FellowshipLogsClient(http, clientId, clientSecret)
                : throw new FellowshipLogsException("Sign in with FellowshipLogs to download fights.");
            // The fight is read as its text's JsonDocument; a long fight's events as a tree are more than a worker has.
            string text = keptFight;
            if (text.Length == 0)
            {
                var client = Client();
                progress.Report("Downloading the fight from FellowshipLogs");
                text = (await client.DownloadFightAsync(reportCode, fightId, progress)).ToJsonString();
                await SaveFight(reportCode, fightId, text);
            }
            var fight = JsonDocument.Parse(text);
            if (!DungeonReader.HasPulls(fight.RootElement))
            {
                fight.Dispose();
                var client = Client();
                progress.Report("Downloading the fight's pulls from FellowshipLogs");
                var raw = JsonNode.Parse(text)!.AsObject();
                await client.AddDungeonPullsAsync(raw);
                text = raw.ToJsonString();
                await SaveFight(reportCode, fightId, text);
                fight = JsonDocument.Parse(text);
            }
            using (fight)
            {
                progress.Report("Reading the dungeon");
                reply = new DungeonReply(DungeonReader.FromRawFight(fight.RootElement, playerId).Format(), null);
            }
        }
        catch (Exception e) when (e is FellowshipLogsException or HttpRequestException or NotSupportedException or ArgumentException)
        {
            reply = new DungeonReply(null, e.Message);
        }
        return JsonSerializer.Serialize(reply, SimChunks.Json);
    }

    /// <summary>
    /// The first fight of a sim with every decision of its action list: a <see cref="TraceReply"/> as JSON, with the trace
    /// or the reason the engine refused it.
    /// </summary>
    [JSExport]
    public static async Task<string> TraceFight(string profileJson, string settingsJson)
    {
        TraceReply reply;
        try
        {
            reply = new TraceReply(await new LocalSimEngine().TraceFightAsync(Read<SimcProfile>(profileJson), Read<SimSettings>(settingsJson)), null);
        }
        catch (SimEngineException e)
        {
            reply = new TraceReply(null, e.Message);
        }
        return JsonSerializer.Serialize(reply, SimChunks.Json);
    }



    /// <summary>Keeps a downloaded fight in the browser's storage (sim/fights.js), for the next replay of it.</summary>
    [JSImport("saveFight", "simWorker")]
    private static partial Task SaveFight(string reportCode, int fightId, string text);

    /// <summary>A replay's progress line, posted to the page (worker.js).</summary>
    [JSImport("progress", "simWorker")]
    private static partial void Progress(int callId, string text);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, SimChunks.Json)!;

    private sealed class ProgressTo(int callId) : IProgress<string>
    {
        public void Report(string value) => Progress(callId, value);
    }
}

/// <summary>A replay's answer from a worker: its review, or the reason the engine refused it.</summary>
public sealed record ReviewReply(ReviewArtifacts? Review, string? Error);
