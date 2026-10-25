using System.Text.Json;

namespace FellowshipSim.UI.Services;

/// <summary>
/// Finished sims, one JSON file each at the visitor's <c>results/&lt;job id&gt;.json</c> in the <see cref="IBlobStore"/>,
/// so they outlast a restart. A visitor reads only their own.
/// </summary>
public sealed class SimResultLibrary(IBlobStore store)
{
    public Task SaveAsync(Visitor visitor, SavedSim sim, CancellationToken ct = default) =>
        store.WriteAsync(KeyOf(visitor, sim.Id), JsonSerializer.Serialize(sim, JsonSerializerOptions.Web), ct);

    /// <summary>The visitor's saved sim <paramref name="id"/>, or null when they have none.</summary>
    public async Task<SavedSim?> GetAsync(Visitor visitor, string id, CancellationToken ct = default) =>
        DiskBlobStore.IsSegment(id) && await store.ReadAsync(KeyOf(visitor, id), ct) is { } json ? Parse(json) : null;

    /// <summary>The visitor's saved sims, newest first. A file that does not read is left out.</summary>
    public async Task<IReadOnlyList<SavedSim>> ListAsync(Visitor visitor, CancellationToken ct = default)
    {
        var sims = new List<SavedSim>();
        foreach (string key in await store.ListAsync(visitor.Key("results"), ct))
        {
            if (key.EndsWith(".json", StringComparison.Ordinal) && await store.ReadAsync(key, ct) is { } json && Parse(json) is { } sim)
            {
                sims.Add(sim);
            }
        }
        return sims.OrderByDescending(s => s.CreatedAt).ToList();
    }

    private static string KeyOf(Visitor visitor, string id) => visitor.Key($"results/{id}.json");

    private static SavedSim? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SavedSim>(json, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
