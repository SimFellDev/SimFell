using System.Text.Json;

namespace FellowshipSim.UI.Services;

/// <summary>
/// Finished sims, one JSON file each at the visitor's <c>results/&lt;job id&gt;.json</c> in the <see cref="IBlobStore"/>,
/// so they outlast a restart, with its <see cref="SavedSimSummary"/> beside it at <c>&lt;job id&gt;.summary.json</c>: a
/// sim's file is several MB, so a list reads the summaries alone. A visitor reads only their own. Past
/// <see cref="Keep"/>, the oldest go as new ones come, all but the favorites.
/// </summary>
public sealed class SimResultLibrary(IBlobStore store)
{
    private const string SummarySuffix = ".summary.json";

    /// <summary>The sims other than favorites a visitor keeps by default.</summary>
    public const int DefaultKeep = 20;

    /// <summary>The most sims other than favorites a visitor keeps; null keeps every one.</summary>
    public int? Keep { get; init; } = DefaultKeep;

    public async Task SaveAsync(Visitor visitor, SavedSim sim, CancellationToken ct = default)
    {
        await store.WriteAsync(KeyOf(visitor, sim.Id), JsonSerializer.Serialize(sim, JsonSerializerOptions.Web), ct);
        await WriteSummaryAsync(visitor, SavedSimSummary.Of(sim), ct);
        if (Keep is not { } keep) return;
        foreach (var old in (await ListAsync(visitor, ct)).Where(s => !s.Favorite).Skip(keep)) await RemoveAsync(visitor, old.Id, ct);
    }

    /// <summary>Takes the visitor's saved sim's new title or favorite (<paramref name="summary"/>); one they do not have is left alone.</summary>
    public async Task UpdateAsync(Visitor visitor, SavedSimSummary summary, CancellationToken ct = default)
    {
        if (await HasAsync(visitor, summary.Id, ct)) await WriteSummaryAsync(visitor, summary, ct);
    }

    /// <summary>Deletes the visitor's saved sim <paramref name="id"/>; one they do not have is left alone.</summary>
    public async Task DeleteAsync(Visitor visitor, string id, CancellationToken ct = default)
    {
        if (await HasAsync(visitor, id, ct)) await RemoveAsync(visitor, id, ct);
    }

    /// <summary>The visitor's saved sim <paramref name="id"/> with its summary's title and favorite, or null when they have none.</summary>
    public async Task<SavedSim?> GetAsync(Visitor visitor, string id, CancellationToken ct = default)
    {
        if (!DiskBlobStore.IsSegment(id) || await store.ReadAsync(KeyOf(visitor, id), ct) is not { } json || Parse<SavedSim>(json) is not { } sim)
        {
            return null;
        }
        return await ReadSummaryAsync(visitor, id, ct) is { } summary ? sim with { Title = summary.Title, Favorite = summary.Favorite } : sim;
    }

    /// <summary>
    /// The visitor's saved sims, newest first. A sim saved before summaries has its summary written now; a file that does
    /// not read is left out.
    /// </summary>
    public async Task<IReadOnlyList<SavedSimSummary>> ListAsync(Visitor visitor, CancellationToken ct = default)
    {
        var sims = new List<SavedSimSummary>();
        foreach (string key in await store.ListAsync(visitor.Key("results"), ct))
        {
            if (!key.EndsWith(".json", StringComparison.Ordinal) || key.EndsWith(SummarySuffix, StringComparison.Ordinal)) continue;
            string id = Path.GetFileNameWithoutExtension(key);
            if (await ReadSummaryAsync(visitor, id, ct) is { } summary)
            {
                sims.Add(summary);
            }
            else if (await store.ReadAsync(key, ct) is { } json && Parse<SummarySource>(json) is { Result: { } result } source)
            {
                summary = new SavedSimSummary(source.Id, source.Title, source.Hero, result.MeanDps, source.CreatedAt, source.Favorite);
                await WriteSummaryAsync(visitor, summary, ct);
                sims.Add(summary);
            }
        }
        return sims.OrderByDescending(s => s.CreatedAt).ToList();
    }

    private async Task<bool> HasAsync(Visitor visitor, string id, CancellationToken ct) =>
        DiskBlobStore.IsSegment(id) && (await store.ListAsync(visitor.Key("results"), ct)).Contains(KeyOf(visitor, id));

    private async Task RemoveAsync(Visitor visitor, string id, CancellationToken ct)
    {
        await store.DeleteAsync(KeyOf(visitor, id), ct);
        await store.DeleteAsync(SummaryKeyOf(visitor, id), ct);
    }

    private async Task<SavedSimSummary?> ReadSummaryAsync(Visitor visitor, string id, CancellationToken ct) =>
        await store.ReadAsync(SummaryKeyOf(visitor, id), ct) is { } json ? Parse<SavedSimSummary>(json) : null;

    private Task WriteSummaryAsync(Visitor visitor, SavedSimSummary summary, CancellationToken ct) =>
        store.WriteAsync(SummaryKeyOf(visitor, summary.Id), JsonSerializer.Serialize(summary, JsonSerializerOptions.Web), ct);

    private static string KeyOf(Visitor visitor, string id) => visitor.Key($"results/{id}.json");

    private static string SummaryKeyOf(Visitor visitor, string id) => visitor.Key($"results/{id}{SummarySuffix}");

    private static T? Parse<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The fields of a saved sim's file its summary takes; the reader skips the rest.</summary>
    private sealed record SummarySource(string Id, string Title, string Hero, DateTime CreatedAt, SourceResult Result, bool Favorite = false);

    private sealed record SourceResult(double MeanDps);
}
