using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
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

    /// <summary>A sim's result from every one of its chunks, in chunk order, as JSON.</summary>
    [JSExport]
    public static string Combine(string settingsJson, string[] chunksJson) =>
        JsonSerializer.Serialize(SimChunks.Combine(Read<SimSettings>(settingsJson), chunksJson.Select(Read<MonteCarloChunk>).ToList()),
            SimChunks.Json);

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
