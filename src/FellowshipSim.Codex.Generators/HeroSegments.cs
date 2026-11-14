using System.Text.Json;
using Microsoft.CodeAnalysis.Text;

namespace FellowshipSim.Codex.Generators;

/// <summary>
/// The tuning keys' first segments naming a hero. Each hero's name is one. An internal name (<c>Firemage</c>, <c>Bowguy</c>,
/// <c>Warmaster</c>) is another: a first segment equal, in any case, to a part of one hero's portrait alone between its
/// underscores (<c>hero_portrait_Firemage_default</c>). Its hero is the hero whose records (their <c>heroes</c> and hero
/// <c>origins</c>) have the most tuning keys starting with it, and a segment of a portrait part whose hero there is another
/// hero names no hero.
/// </summary>
internal static class HeroSegments
{
    /// <summary>The first segments naming a hero, from the game data's hero records and tuning keys.</summary>
    public static EquatableArray<HeroSegment> Of(SourceText? entities, CancellationToken ct)
    {
        var portraits = new Dictionary<string, string>(StringComparer.Ordinal);
        // The count of tuning keys starting with each first segment, by the hero of their records.
        var counts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        if (entities == null) return new([]);
        foreach (var line in entities.Lines)
        {
            ct.ThrowIfCancellationRequested();
            string text = line.ToString();
            bool hero = text.Contains("\"$type\":\"hero\"");
            if (!hero && !text.Contains("\"tuning\":{")) continue;
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(text);
            }
            catch (JsonException)
            {
                continue;
            }
            using (document)
            {
                var root = document.RootElement;
                if (hero && String(root, "name") is { Length: > 0 } name && String(root, "portrait") is { Length: > 0 } portrait)
                {
                    portraits[name] = portrait;
                }
                if (!root.TryGetProperty("tuning", out var tuning) || tuning.ValueKind != JsonValueKind.Object) continue;
                var heroes = HeroesOf(root);
                if (heroes.Count == 0) continue;
                foreach (var key in tuning.EnumerateObject())
                {
                    int dot = key.Name.IndexOf('.');
                    if (dot <= 0) continue;
                    string segment = key.Name.Substring(0, dot);
                    if (!counts.TryGetValue(segment, out var byHero)) counts[segment] = byHero = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (string h in heroes) byHero[h] = byHero.TryGetValue(h, out int n) ? n + 1 : 1;
                }
            }
        }

        // Each portrait part of one hero's portrait alone, and that hero.
        var parts = portraits
            .SelectMany(p => p.Value.Split('_').Distinct(StringComparer.OrdinalIgnoreCase).Select(part => (Part: part, Hero: p.Key)))
            .GroupBy(p => p.Part, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single().Hero, StringComparer.OrdinalIgnoreCase);
        var segments = portraits.Keys.Select(h => new HeroSegment(h, h)).ToList();
        foreach (var (segment, byHero) in counts)
        {
            if (portraits.ContainsKey(segment) || !parts.TryGetValue(segment, out var portraitHero)) continue;
            string most = byHero.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).First().Key;
            if (most == portraitHero) segments.Add(new HeroSegment(segment, most));
        }
        return new([.. segments.OrderBy(s => s.Segment, StringComparer.Ordinal)]);
    }

    /// <summary>The record's <c>heroes</c> and the names of its hero <c>origins</c>.</summary>
    private static HashSet<string> HeroesOf(JsonElement record)
    {
        var heroes = new HashSet<string>(StringComparer.Ordinal);
        if (record.TryGetProperty("heroes", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var h in list.EnumerateArray())
            {
                if (h.ValueKind == JsonValueKind.String && h.GetString() is { Length: > 0 } name) heroes.Add(name);
            }
        }
        if (record.TryGetProperty("origins", out var origins) && origins.ValueKind == JsonValueKind.Array)
        {
            foreach (var o in origins.EnumerateArray())
            {
                if (o.ValueKind == JsonValueKind.Object && String(o, "type") == "hero" && String(o, "name") is { Length: > 0 } name) heroes.Add(name);
            }
        }
        return heroes;
    }

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
}
