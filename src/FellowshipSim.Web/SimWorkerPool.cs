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

    private async Task<IJSObjectReference> PoolAsync() =>
        _pool ??= await js.InvokeAsync<IJSObjectReference>("import", "./sim/simPool.js");

    /// <summary>The workers a sim can use: the cores but the page's, up to the pool's most.</summary>
    public async Task<int> SizeAsync() => await (await PoolAsync()).InvokeAsync<int>("size");

    /// <summary>
    /// Runs a sim's <paramref name="chunkCount"/> chunks on up to <paramref name="workers"/> workers (all when null) and
    /// returns its result's JSON. <paramref name="chunkDone"/> hears the count of chunks done as each completes.
    /// </summary>
    public async Task<string> SimulateAsync(string profileJson, string settingsJson, int chunkCount, int? workers,
        Action<int> chunkDone, CancellationToken ct)
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

    /// <summary>Replays player <paramref name="playerId"/> of a raw fight on a worker; the worker's <see cref="ReviewReply"/> JSON.</summary>
    public async Task<string> ReviewAsync(string rawFight, int playerId, IProgress<string>? progress)
    {
        var pool = await PoolAsync();
        using var sink = DotNetObjectReference.Create(new Sink(null, progress));
        try
        {
            return await pool.InvokeAsync<string>("review", CancellationToken.None, rawFight, playerId, sink);
        }
        catch (JSException e)
        {
            throw new SimEngineException(Reason(e));
        }
    }

    /// <summary>
    /// Downloads a fight and replays player <paramref name="playerId"/> on a worker, with <paramref name="credentials"/>;
    /// the worker's <see cref="ReviewReply"/> JSON.
    /// </summary>
    public async Task<string> ReviewFightAsync(string reportCode, int fightId, int playerId, LogsCredentials credentials,
        IProgress<string>? progress)
    {
        var pool = await PoolAsync();
        using var sink = DotNetObjectReference.Create(new Sink(null, progress));
        try
        {
            return await pool.InvokeAsync<string>("reviewFight", CancellationToken.None, reportCode, fightId, playerId,
                credentials.ClientId, credentials.ClientSecret, sink);
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
    public sealed class Sink(Action<int>? chunkDone, IProgress<string>? progress)
    {
        [JSInvokable]
        public void OnChunk(int done) => chunkDone?.Invoke(done);

        [JSInvokable]
        public void OnProgress(string text) => progress?.Report(text);
    }
}
