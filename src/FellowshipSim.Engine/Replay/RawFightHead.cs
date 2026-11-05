using System.Text.Json;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// A raw fight's top-level values other than its events (<c>report_code</c>, <c>report_start_time</c>, <c>fight</c> and
/// <c>master_data</c>), each a <see cref="JsonDocument"/> of its own, and the place of its <c>events</c> in the raw fight's
/// bytes. The documents are small and are not disposed (see <see cref="ReplayReview"/>).
/// </summary>
internal sealed class RawFightHead
{
    private static readonly string[] Names = ["report_code", "report_start_time", "fight", "master_data"];

    private readonly JsonElement[] _values = new JsonElement[Names.Length];

    /// <summary>The start of the <c>events</c> array in the raw fight's bytes; -1 for a raw fight without one.</summary>
    public int EventsStart { get; private set; } = -1;

    /// <summary>The top-level value named <paramref name="name"/>, read as <see cref="RawFightValue"/>.</summary>
    public RawFightValue this[string name] => new(_values[Array.IndexOf(Names, name)]);

    /// <summary>
    /// The head of a raw fight's UTF-8 JSON. The events are read past only when a head value comes after them. A raw fight
    /// that is not a JSON object throws <see cref="ArgumentException"/>.
    /// </summary>
    public static RawFightHead Read(ReadOnlySpan<byte> rawFight)
    {
        var head = new RawFightHead();
        var reader = new Utf8JsonReader(rawFight);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new ArgumentException("The raw fight is empty.");
        int seen = 0;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("events"u8))
            {
                reader.Read();
                head.EventsStart = reader.TokenType == JsonTokenType.StartArray ? (int)reader.TokenStartIndex : -1;
                if (seen == Names.Length) break;
                reader.Skip();
                continue;
            }
            int i = Names.Length - 1;
            while (i >= 0 && !reader.ValueTextEquals(Names[i])) i--;
            reader.Read();
            if (i < 0)
            {
                reader.Skip();
                continue;
            }
            if (head._values[i].ValueKind == JsonValueKind.Undefined) seen++;
            head._values[i] = JsonDocument.ParseValue(ref reader).RootElement;
        }
        return head;
    }
}
