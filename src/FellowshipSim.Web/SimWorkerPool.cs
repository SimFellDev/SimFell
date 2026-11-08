using System.Reflection;
using FellowshipSim.Core.Engine;
using FellowshipSim.Engine;
using FellowshipSim.UI.Services;
using Microsoft.JSInterop;

namespace FellowshipSim.Web;

/// <summary>
/// The page's side of the sim workers (sim/simPool.js): a sim's chunks spread over them, and a replay on one of them. A
/// worker's error comes back as a <see cref="SimEngineException"/>; a cancelled sim as an
/// <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class SimWorkerPool(IJSRuntime js)
{
    private IJSObjectReference? _pool;
    private int _nextRun;

    /// <summary>
    /// The build's stamp (the project's BuildStamp). The sim scripts' addresses carry it, so a browser never runs a
    /// script it kept from an older build (GitHub Pages lets it keep one for 10 minutes) with this build's app.
    /// </summary>
    private static readonly string BuildStamp = typeof(SimWorkerPool).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "BuildStamp")?.Value ?? "0";

    private async Task<IJSObjectReference> PoolAsync() =>
        _pool ??= await js.InvokeAsync<IJSObjectReference>("import", $"./sim/simPool.js?v={BuildStamp}");

    /// <summary>The workers a sim can use: the cores but the page's, up to the pool's most.</summary>
    public async Task<int> SizeAsync() => await (await PoolAsync()).InvokeAsync<int>("size");

    /// <summary>The browser's logical processors.</summary>
    public async Task<int> CoresAsync() => await (await PoolAsync()).InvokeAsync<int>("cores");

    /// <summary>
    /// Runs a sim's <paramref name="chunkCount"/> chunks on up to <paramref name="workers"/> workers (all when null) and
    /// returns its result's JSON. <paramref name="chunkDone"/> hears each chunk's index and fights as it completes, and
    /// returns the chunks of the result: fewer ends the sim at them.
    /// </summary>
    public async Task<string> SimulateAsync(string profileJson, string settingsJson, int chunkCount, int? workers,
        Func<int, DpsMoments, int> chunkDone, CancellationToken ct)
    {
        var pool = await PoolAsync();
        int run = Interlocked.Increment(ref _nextRun);
        using var sink = DotNetObjectReference.Create(new Sink(chunkDone, null));
        await using var stop = ct.Register(() => _ = pool.InvokeVoidAsync("cancel", run).AsTask());
        try
        {
            return await pool.InvokeAsync<string>("simulate", CancellationToken.None, run, profileJson, settingsJson, chunkCount, workers, sink);
        }
        catch (JSException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (JSException e)
        {
            throw new SimEngineException(Reason(e));
        }
    }

    /// <summary>
    /// Replays player <paramref name="playerId"/> of a raw fight's UTF-8 JSON on a worker; the worker's
    /// <see cref="ReviewReply"/> bytes.
    /// </summary>
    public async Task<byte[]> ReviewAsync(byte[] rawFight, int playerId, IProgress<string>? progress)
    {
        var pool = await PoolAsync();
        using var sink = DotNetObjectReference.Create(new Sink(null, progress));
        try
        {
            return await pool.InvokeAsync<byte[]>("review", CancellationToken.None, rawFight, playerId, sink);
        }
        catch (JSException e)
        {
            throw new SimEngineException(Reason(e));
        }
    }

    /// <summary>
    /// Downloads a fight and replays player <paramref name="playerId"/> on a worker, as the signed-in user
    /// (<paramref name="accessToken"/>) or else with a developer's <paramref name="credentials"/>; the worker's
    /// <see cref="ReviewReply"/> bytes.
    /// </summary>
    public async Task<byte[]> ReviewFightAsync(string reportCode, int fightId, int playerId, string? accessToken, LogsCredentials? credentials,
        IProgress<string>? progress)
    {
        var pool = await PoolAsync();
        using var sink = DotNetObjectReference.Create(new Sink(null, progress));
        try
        {
            return await pool.InvokeAsync<byte[]>("reviewFight", CancellationToken.None, reportCode, fightId, playerId,
                accessToken ?? "", credentials?.ClientId ?? "", credentials?.ClientSecret ?? "", sink);
        }
        catch (JSException e)
        {
            throw new SimEngineException(Reason(e));
        }
    }

    /// <summary>The first fight of a sim with every decision of its action list, on a worker; the worker's <see cref="TraceReply"/> JSON.</summary>
    public async Task<string> TraceFightAsync(string profileJson, string settingsJson)
    {
        var pool = await PoolAsync();
        try
        {
            return await pool.InvokeAsync<string>("traceFight", CancellationToken.None, profileJson, settingsJson);
        }
        catch (JSException e)
        {
            throw new SimEngineException(Reason(e));
        }
    }


    // The first line of a worker's error, without the JS error's own prefix.
    private static string Reason(JSException e)
    {
        string line = e.Message.Split('\n')[0].Trim();
        return line.StartsWith("Error: ", StringComparison.Ordinal) ? line["Error: ".Length..] : line;
    }

    /// <summary>What simPool.js tells the page as a run goes: chunks done, or a replay's progress lines.</summary>
    public sealed class Sink(Func<int, DpsMoments, int>? chunkDone, IProgress<string>? progress)
    {
        /// <summary>Chunk <paramref name="chunk"/> is done, with its fights' DPS moments; returns the chunks of the result.</summary>
        [JSInvokable]
        public int OnChunk(int chunk, long count, double mean, double m2) => chunkDone?.Invoke(chunk, new DpsMoments(count, mean, m2)) ?? -1;

        [JSInvokable]
        public void OnProgress(string text) => progress?.Report(text);
    }
}
