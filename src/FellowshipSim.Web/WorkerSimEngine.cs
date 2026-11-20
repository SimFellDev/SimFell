using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Debugging;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.Logs;

namespace FellowshipSim.Web;

/// <summary>
/// The browser app's engine. Heroes, the action-list grammar and checks run on the page (<see cref="LocalSimEngine"/>);
/// sims and replays run on the sim workers (<see cref="SimWorkerPool"/>), so the page stays free while they work. A sim
/// gives the same result as <see cref="LocalSimEngine"/>'s, to the last bit.
/// </summary>
public sealed class WorkerSimEngine(SimWorkerPool workers) : ISimEngine
{
    private readonly LocalSimEngine _local = new();

    public Task<IReadOnlyList<string>> GetHeroesAsync(CancellationToken ct = default) => _local.GetHeroesAsync(ct);

    public Task<AplVocabulary?> GetVocabularyAsync(string hero, CancellationToken ct = default) => _local.GetVocabularyAsync(hero, ct);

    public Task<AplExpressionCheck?> CheckExpressionAsync(string hero, string text, CancellationToken ct = default) =>
        _local.CheckExpressionAsync(hero, text, ct);

    public Task<AplExpressionCheck?> CheckActionListAsync(string hero, string text, CancellationToken ct = default) =>
        _local.CheckActionListAsync(hero, text, ct);

    public Task<AplDocumentActions> ParseActionListAsync(string text, CancellationToken ct = default) => _local.ParseActionListAsync(text, ct);

    public Task<AplDocumentText> WriteActionListAsync(IReadOnlyList<AplDocumentAction> actions, CancellationToken ct = default) =>
        _local.WriteActionListAsync(actions, ct);

    public async Task<ReviewArtifacts> ReviewAsync(Stream rawFight, int playerId, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var raw = new MemoryStream();
        await rawFight.CopyToAsync(raw, ct);
        var reply = ReviewReply.FromBytes(await workers.ReviewAsync(raw.ToArray(), playerId, progress));
        return reply.Review ?? throw new SimEngineException(reply.Error ?? "The replay gave no review.");
    }

    public async Task<SimResult> SimulateAsync(SimcProfile profile, SimSettings settings, IProgress<SimProgress>? progress = null,
        CancellationToken ct = default)
    {
        // An action list the engine cannot run is refused here, with its reason, before any worker starts.
        SimChunks.NewSimulator(profile);
        var chunks = new SimChunkProgress(settings);
        string json = await workers.SimulateAsync(JsonSerializer.Serialize(profile, SimChunks.Json),
            JsonSerializer.Serialize(settings, SimChunks.Json), chunks.ResultChunks, settings.DegreeOfParallelism,
            (chunk, fights) =>
            {
                int resultChunks = chunks.Add(chunk, fights);
                progress?.Report(chunks.Progress);
                return resultChunks;
            },
            ct);
        var result = JsonSerializer.Deserialize<SimResult>(json, SimChunks.Json)!;
        progress?.Report(SimChunks.Progress(settings, result.Iterations, settings.TargetError > 0 ? result.MarginPercent : null, done: true));
        return result;
    }

    public async Task<AplFightTrace> TraceFightAsync(SimcProfile profile, SimSettings settings, CancellationToken ct = default)
    {
        // An action list the engine cannot run is refused here, with its reason, before a worker starts.
        SimChunks.CheckDungeon(settings);
        SimChunks.NewSimulator(profile);
        string json = await workers.TraceFightAsync(JsonSerializer.Serialize(profile, SimChunks.Json),
            JsonSerializer.Serialize(settings, SimChunks.Json));
        var reply = JsonSerializer.Deserialize<TraceReply>(json, SimChunks.Json)!;
        return reply.Trace ?? throw new SimEngineException(reply.Error ?? "The fight gave no trace.");
    }
}
