using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
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
    /// Replays player <paramref name="playerId"/> of a raw fight's UTF-8 JSON: a <see cref="ReviewReply"/> as bytes (a
    /// Uint8Array), with the review's files or the reason the engine refused it. Its progress goes to the page as call
    /// <paramref name="callId"/>'s.
    /// </summary>
    [JSExport]
    public static async Task<JSObject> Review(byte[] rawFight, int playerId, int callId)
    {
        ReviewReply reply;
        try
        {
            reply = new ReviewReply(await new LocalSimEngine().ReviewAsync(rawFight, playerId, new ProgressTo(callId)), null);
        }
        catch (SimEngineException e)
        {
            reply = new ReviewReply(null, e.Message);
        }
        return Bytes(reply.ToBytes());
    }

    /// <summary>
    /// Downloads fight <paramref name="fightId"/> of report <paramref name="reportCode"/> from FellowshipLogs with the
    /// signed-in user's token (<paramref name="accessToken"/>), else a developer's API client, and replays player
    /// <paramref name="playerId"/>: a <see cref="ReviewReply"/> as bytes (a Uint8Array). The fight
    /// is kept in the browser's storage for the next replay of it (sim/fights.js), never in the page's memory.
    /// </summary>
    [JSExport]
    public static async Task<JSObject> ReviewFight(string reportCode, int fightId, int playerId, string accessToken, string clientId,
        string clientSecret, int callId)
    {
        using var http = new HttpClient();
        var client = accessToken.Length > 0 ? FellowshipLogsClient.ForUser(http, accessToken) : new FellowshipLogsClient(http, clientId, clientSecret);
        return await ReviewFight(client, reportCode, fightId, playerId, new ProgressTo(callId));
    }

    /// <summary><see cref="ReviewFight(string, int, int, string, string, string, int)"/> with <paramref name="client"/>.</summary>
    internal static async Task<JSObject> ReviewFight(FellowshipLogsClient client, string reportCode, int fightId, int playerId,
        IProgress<string> progress)
    {
        ReviewReply reply;
        try
        {
            progress.Report("Downloading the fight from FellowshipLogs");
            var fight = new MemoryStream();
            await client.DownloadFightAsync(reportCode, fightId, fight, progress);
            var text = new ArraySegment<byte>(fight.GetBuffer(), 0, (int)fight.Length);
            await SaveFight(reportCode, fightId, text);
            reply = new ReviewReply(ReplayReview.Run(text, playerId, progress: progress), null);
        }
        catch (Exception e) when (e is FellowshipLogsException or HttpRequestException or NotSupportedException or ArgumentException)
        {
            reply = new ReviewReply(null, e.Message);
        }
        return Bytes(reply.ToBytes());
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

    /// <summary>
    /// Keeps a downloaded fight's UTF-8 JSON in the browser's storage (sim/fights.js), for the next replay of it. The
    /// script copies the bytes before it returns.
    /// </summary>
    [JSImport("saveFight", "simWorker")]
    private static partial Task SaveFight(string reportCode, int fightId, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> text);

    /// <summary>The bytes as a Uint8Array of the script's own.</summary>
    [JSImport("bytes", "simWorker")]
    private static partial JSObject Bytes(byte[] bytes);

    /// <summary>A replay's progress line, posted to the page (worker.js).</summary>
    [JSImport("progress", "simWorker")]
    private static partial void Progress(int callId, string text);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, SimChunks.Json)!;

    private sealed class ProgressTo(int callId) : IProgress<string>
    {
        public void Report(string value) => Progress(callId, value);
    }
}
