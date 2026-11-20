using System.Text.Json;
using Fellowship.SDK.Documents;

namespace FellowshipSim.Codex.Generators;

/// <summary>
/// The tuning keys with the value of a record's scalar field. A field of kind <c>ConstantsRow</c> in a record's
/// <c>fieldSources</c> has the value of its one row, and the keys in its <c>tuning</c> member are that row in the record's
/// tuning. A field's constant is that value, so those keys have no constant in <c>Constants</c>. A key with a value by rank
/// keeps its constant, since the field has the first rank alone.
/// </summary>
internal static class FieldTuning
{
    private const string ConstantsRow = "ConstantsRow";

    /// <summary>
    /// The tuning keys of <paramref name="tuned"/> with the value of each of <paramref name="fields"/> of
    /// <paramref name="record"/>, by field. The field and the key have one row: the row of the field in the record's
    /// <c>fieldSources</c>, and the row of a field of <paramref name="tuned"/> naming the key. A field with no key is left out.
    /// </summary>
    public static Dictionary<string, List<string>> KeysOf(EntityDocument record, IEnumerable<string> fields, EntityDocument tuned)
    {
        var keysByRow = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var source in (tuned.FieldSources ?? new Dictionary<string, Fellowship.SDK.Models.FieldSource>()).Values)
        {
            if (source.Kind != ConstantsRow || source.Rows.Count != 1 || source.Tuning is not { Count: > 0 } keys) continue;
            var single = keys.Where(k => tuned.Tuning != null && tuned.Tuning.TryGetValue(k, out var t)
                && t.Value is JsonElement { ValueKind: not JsonValueKind.Array }).ToList();
            if (single.Count == 0) continue;
            if (!keysByRow.TryGetValue(source.Rows[0], out var row)) keysByRow[source.Rows[0]] = row = [];
            row.AddRange(single.Where(k => !row.Contains(k)));
        }

        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string field in fields)
        {
            if (record.FieldSources?.GetValueOrDefault(field) is { Kind: ConstantsRow, Rows.Count: 1 } source
                && keysByRow.TryGetValue(source.Rows[0], out var keys))
            {
                result[field] = keys;
            }
        }
        return result;
    }

    /// <summary>
    /// A field constant's summary, ending in a full stop, with the tuning keys of the field's value after it, and the record
    /// with those keys where it is another record.
    /// </summary>
    public static string Summary(string summary, IReadOnlyList<string>? keys, string? tuned = null) =>
        keys is { Count: > 0 }
            ? summary.Substring(0, summary.Length - 1) + "; tuning " + string.Join(", ", keys.Select(k => $"<c>{k}</c>")) + (tuned != null ? " of " + tuned : "") + "."
            : summary;
}
