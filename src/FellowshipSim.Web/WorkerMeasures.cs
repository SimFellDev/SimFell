using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Engine;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;

namespace FellowshipSim.Web;

/// <summary>
/// A sim worker's measures for benchmarks/wasm/worker-memory.mjs, which runs a worker's runtime in Node: its managed heap,
/// its collections, a sim's chunk count, the default dungeon, a replay script built alone, and a first replay of a fight with FellowshipLogs' answers
/// from the script.
/// </summary>
public static partial class WorkerMeasures
{
    /// <summary>The managed heap's bytes after a full collection, and the collections so far, as JSON.</summary>
    [JSExport]
    public static string Memory()
    {
        long heap = GC.GetTotalMemory(forceFullCollection: true);
        return JsonSerializer.Serialize(new { heap, collections = GC.CollectionCount(0) });
    }

    /// <summary>The chunks of a sim with these settings (<see cref="SimChunks.Count"/>).</summary>
    [JSExport]
    public static int ChunkCount(string settingsJson) => SimChunks.Count(JsonSerializer.Deserialize<SimSettings>(settingsJson, SimChunks.Json)!);

    /// <summary>The default dungeon as JSON.</summary>
    [JSExport]
    public static string DefaultDungeon() => JsonSerializer.Serialize(DefaultDungeons.Default, SimChunks.Json);

    /// <summary>
    /// <see cref="SimWorker.ReviewFight(string, int, int, string, string, string, int)"/> with each FellowshipLogs answer
    /// from the script's <c>answer</c> (<see cref="Answer"/>).
    /// </summary>
    [JSExport]
    public static Task<JSObject> ReviewFight(string reportCode, int fightId, int playerId) =>
        SimWorker.ReviewFight(FellowshipLogsClient.ForUser(new HttpClient(new FromScript()), "token"), reportCode, fightId, playerId,
            new Progress<string>());

    /// <summary>The replay script of player <paramref name="playerId"/> in a raw fight's UTF-8 JSON, built alone: its length.</summary>
    [JSExport]
    public static int BuildScript(byte[] rawFight, int playerId) => ReplayScriptBuilder.Build(new RawFightHolder(rawFight), playerId).Length;

    /// <summary>The script's answer to a FellowshipLogs query (the request's JSON body).</summary>
    [JSImport("answer", "workerMeasures")]
    private static partial string Answer(string query);

    private sealed class FromScript : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(Answer(await request.Content!.ReadAsStringAsync(ct)), Encoding.UTF8, "application/json"),
            };
    }
}
