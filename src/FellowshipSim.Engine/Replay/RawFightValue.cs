using System.Text.Json;
using System.Text.Json.Nodes;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// A value of a raw fight's <see cref="JsonDocument"/>, read in place. A missing property and JSON null are both none,
/// and a property of none is none. A long fight's events as a <see cref="JsonNode"/> tree are over a GB, more than the
/// browser app's worker has; the document is its text and an index.
/// </summary>
internal readonly record struct RawFightValue(JsonElement Element)
{
    public RawFightValue this[string name] =>
        Element.ValueKind == JsonValueKind.Object && Element.TryGetProperty(name, out var value) ? new RawFightValue(value) : default;

    public bool IsNull => Element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;

    public bool IsArray => Element.ValueKind == JsonValueKind.Array;

    /// <summary>The array's items other than JSON null; none for a value that is no array.</summary>
    public IEnumerable<RawFightValue> Items => IsArray
        ? Element.EnumerateArray().Where(i => i.ValueKind != JsonValueKind.Null).Select(i => new RawFightValue(i))
        : [];

    public static string Str(RawFightValue v) => v.Element.ValueKind == JsonValueKind.String ? v.Element.GetString()! : "";

    public static int? Int(RawFightValue v) =>
        v.Element.ValueKind is JsonValueKind.Number or JsonValueKind.String or JsonValueKind.True or JsonValueKind.False ? (int)Num(v) : null;

    public static double Num(RawFightValue v) => v.Element.ValueKind == JsonValueKind.Number && v.Element.TryGetDouble(out var d) ? d : 0;

    public static bool True(RawFightValue v) => v.Element.ValueKind == JsonValueKind.True;

    /// <summary>The value as a node of its own, with its text as the log has it; null for none.</summary>
    public static JsonNode? Copy(RawFightValue v) => v.IsNull ? null : JsonNode.Parse(v.Element.GetRawText());
}
