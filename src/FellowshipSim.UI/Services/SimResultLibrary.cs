using System.Text.Json;

namespace FellowshipSim.UI.Services;

/// <summary>
/// Finished sims, one JSON file each at the visitor's <c>results/&lt;job id&gt;.json</c> in the <see cref="IBlobStore"/>,
/// so they outlast a restart. A visitor reads only their own. With <see cref="Keep"/> set, the oldest go as new ones come.
/// </summary>
public sealed class SimResultLibrary(IBlobStore store)
{
    /// <summary>The most sims a visitor keeps (the browser app's storage); null keeps every one.</summary>
    public int? Keep { get; init; }

    public async Task SaveAsync(Visitor visitor, SavedSim sim, CancellationToken ct = default)
    {
        await store.WriteAsync(KeyOf(visitor, sim.Id), JsonSerializer.Serialize(sim, JsonSerializerOptions.Web), ct);
        if (Keep is not { } keep) return;
        foreach (var old in (await ListAsync(visitor, ct)).Skip(keep)) await store.DeleteAsync(KeyOf(visitor, old.Id), ct);
    }

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
