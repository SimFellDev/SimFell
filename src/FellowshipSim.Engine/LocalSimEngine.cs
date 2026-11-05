using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Debugging;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Engine.Services;
using Superpower;

namespace FellowshipSim.Engine;

/// <summary>
/// The engine in this process. Replays and sims run on thread-pool threads; a sim's fights use up to
/// <see cref="SimSettings.DegreeOfParallelism"/> threads.
/// </summary>
public sealed class LocalSimEngine : ISimEngine
{
    private readonly Vocabularies _vocabularies = new();

    public Task<IReadOnlyList<string>> GetHeroesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Catalog.Heroes.Keys.Order().ToList());

    public Task<AplVocabulary?> GetVocabularyAsync(string hero, CancellationToken ct = default) =>
        Catalog.HasHero(hero) ? Task.Run<AplVocabulary?>(() => _vocabularies.For(hero), ct) : Task.FromResult<AplVocabulary?>(null);

    public Task<AplExpressionCheck?> CheckExpressionAsync(string hero, string text, CancellationToken ct = default) =>
        Catalog.HasHero(hero)
            ? Task.Run<AplExpressionCheck?>(() => _vocabularies.For(hero).Check(text), ct)
            : Task.FromResult<AplExpressionCheck?>(null);

    public Task<AplExpressionCheck?> CheckActionListAsync(string hero, string text, CancellationToken ct = default) =>
        Catalog.HasHero(hero)
            ? Task.Run<AplExpressionCheck?>(() => _vocabularies.For(hero).CheckList(text), ct)
            : Task.FromResult<AplExpressionCheck?>(null);

    public Task<AplDocumentActions> ParseActionListAsync(string text, CancellationToken ct = default)
    {
        try
        {
            return Task.FromResult(new AplDocumentActions(AplDocument.Parse(text)));
        }
        catch (ParseException e)
        {
            throw new SimEngineException(e.Message, e);
        }
    }

    public Task<AplDocumentText> WriteActionListAsync(IReadOnlyList<AplDocumentAction> actions, CancellationToken ct = default)
    {
        var (text, errors) = AplDocument.Write(actions);
        return Task.FromResult(new AplDocumentText(text, errors));
    }

    public async Task<ReviewArtifacts> ReviewAsync(Stream rawFight, int playerId, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var bytes = new MemoryStream(rawFight.CanSeek ? (int)(rawFight.Length - rawFight.Position) : 0);
        await rawFight.CopyToAsync(bytes, ct);
        return await ReviewAsync(bytes.GetBuffer().AsMemory(0, (int)bytes.Length), playerId, progress, ct);
    }

    /// <summary>
    /// The review of player <paramref name="playerId"/> in a raw fight's UTF-8 JSON, read in one pass over its bytes
    /// (<see cref="ReplayScriptBuilder"/>).
    /// </summary>
    public async Task<ReviewArtifacts> ReviewAsync(ReadOnlyMemory<byte> rawFight, int playerId, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(() => ReplayReview.Run(rawFight, playerId, progress: progress, cancellationToken: ct), ct);
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException)
        {
            throw new SimEngineException(e.Message, e);
        }
    }

    public Task<SimResult> SimulateAsync(SimcProfile profile, SimSettings settings, IProgress<SimProgress>? progress = null,
        CancellationToken ct = default) =>
        Task.Run(() =>
        {
            SimChunks.CheckDungeon(settings);
            var simulator = SimChunks.NewSimulator(profile);
            var result = simulator.Run(SimTime.FromSeconds(settings.DurationSeconds), settings.Iterations, settings.TargetCount,
                settings.FirstSeed, settings.DegreeOfParallelism, rollTypicalFight: true,
                progress: progress == null ? null : completed => progress.Report(new SimProgress(completed, settings.Iterations)),
                cancellationToken: ct, fight: settings.Fight);
            return SimResult.From(result);
        }, ct);

    public Task<AplFightTrace> TraceFightAsync(SimcProfile profile, SimSettings settings, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            SimChunks.CheckDungeon(settings);
            return SimChunks.NewSimulator(profile).TraceFight(0, SimTime.FromSeconds(settings.DurationSeconds), settings.TargetCount,
                settings.FirstSeed, settings.Fight);
        }, ct);
}
