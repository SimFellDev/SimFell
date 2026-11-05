using System.Text.Json;

namespace FellowshipSim.Logs;

/// <summary>
/// One page of a FellowshipLogs events query, read in place in the answer's UTF-8 JSON: the place of its
/// <c>reportData.report.events.data</c> array, and its <c>nextPageTimestamp</c>. Each event is copied as the answer has
/// it, with no event parsed.
/// </summary>
internal readonly record struct EventsPage(int DataStart, int DataLength, double? NextPageTimestamp)
{
    /// <summary>The page of an events query's answer; null for an answer without an events array.</summary>
    public static EventsPage? Read(ReadOnlySpan<byte> answer)
    {
        var reader = new Utf8JsonReader(answer);
        reader.Read();
        if (!Into(ref reader, "data"u8) || !Into(ref reader, "reportData"u8) || !Into(ref reader, "report"u8) || !Into(ref reader, "events"u8))
        {
            return null;
        }
        int dataStart = -1, dataLength = 0;
        double? next = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool data = reader.ValueTextEquals("data"u8), nextPage = reader.ValueTextEquals("nextPageTimestamp"u8);
            reader.Read();
            if (data && reader.TokenType == JsonTokenType.StartArray)
            {
                dataStart = (int)reader.TokenStartIndex;
                reader.Skip();
                dataLength = (int)reader.BytesConsumed - dataStart;
            }
            else if (data) dataStart = -1;
            else if (nextPage) next = reader.TokenType == JsonTokenType.Number ? reader.GetDouble() : null;
            reader.Skip();
        }
        return dataStart < 0 ? null : new EventsPage(dataStart, dataLength, next);
    }

    /// <summary>Each event of the page in the answer's order: its place in the answer, time, source and target.</summary>
    public List<PageEvent> Events(ReadOnlySpan<byte> answer)
    {
        var data = answer.Slice(DataStart, DataLength);
        var reader = new Utf8JsonReader(data);
        reader.Read();
        var events = new List<PageEvent>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            int start = (int)reader.TokenStartIndex;
            reader.Skip();
            int length = (int)reader.BytesConsumed - start;
            var (timestamp, source, target) = Actors(data.Slice(start, length));
            events.Add(new PageEvent(DataStart + start, length, timestamp, source, target));
        }
        return events;
    }

    /// <summary>
    /// Writes one event to <paramref name="writer"/> as the answer has it. An event with line breaks between its tokens
    /// (an answer formatted for reading) is written compact.
    /// </summary>
    public static void WriteEvent(ReadOnlySpan<byte> value, Utf8JsonWriter writer)
    {
        // JSON strings have no raw line breaks, so a line break in the event is between tokens.
        if (value.IndexOfAny((byte)'\n', (byte)'\r') < 0) writer.WriteRawValue(value, skipInputValidation: true);
        else
        {
            using var document = JsonDocument.Parse(value.ToArray());
            document.RootElement.WriteTo(writer);
        }
    }

    // An event's timestamp, sourceID and targetID; null for each one the event lacks.
    private static (double? Timestamp, int? Source, int? Target) Actors(ReadOnlySpan<byte> value)
    {
        var reader = new Utf8JsonReader(value);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return (null, null, null);
        double? timestamp = null;
        int? source = null, target = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isTimestamp = reader.ValueTextEquals("timestamp"u8), isSource = reader.ValueTextEquals("sourceID"u8),
                isTarget = reader.ValueTextEquals("targetID"u8);
            reader.Read();
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (isTimestamp) timestamp = reader.GetDouble();
                else if (isSource) source = reader.GetInt32();
                else if (isTarget) target = reader.GetInt32();
            }
            reader.Skip();
        }
        return (timestamp, source, target);
    }

    // Moves the reader from an object's start to the start of its property's value; false when the property is missing
    // or its value is no object.
    private static bool Into(ref Utf8JsonReader reader, ReadOnlySpan<byte> name)
    {
        if (reader.TokenType != JsonTokenType.StartObject) return false;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool found = reader.ValueTextEquals(name);
            reader.Read();
            if (found) return reader.TokenType == JsonTokenType.StartObject;
            reader.Skip();
        }
        return false;
    }
}
