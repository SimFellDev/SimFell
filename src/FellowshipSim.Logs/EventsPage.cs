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

    /// <summary>
    /// Writes each event of the page to <paramref name="writer"/> as the answer has it, and returns their count. A page
    /// with line breaks between its tokens (an answer formatted for reading) has each event written compact.
    /// </summary>
    public int WriteEvents(ReadOnlySpan<byte> answer, Utf8JsonWriter writer)
    {
        var data = answer.Slice(DataStart, DataLength);
        // JSON strings have no raw line breaks, so a line break in the array is between tokens.
        bool compact = data.IndexOfAny((byte)'\n', (byte)'\r') < 0;
        var reader = new Utf8JsonReader(data);
        reader.Read();
        int count = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            count++;
            int start = (int)reader.TokenStartIndex;
            reader.Skip();
            var value = data[start..(int)reader.BytesConsumed];
            if (compact) writer.WriteRawValue(value, skipInputValidation: true);
            else
            {
                using var document = JsonDocument.Parse(value.ToArray());
                document.RootElement.WriteTo(writer);
            }
        }
        return count;
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
