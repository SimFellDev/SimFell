using System.Text.Json;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// A raw fight's value as the log has it: its UTF-8 text's start and length in the raw fight's bytes, and its first token.
/// A missing value and JSON null have <see cref="JsonTokenType.None"/>.
/// </summary>
internal readonly record struct RawSlice(int Start, int Length, JsonTokenType Token)
{
    public bool IsNone => Token == JsonTokenType.None;

    public ReadOnlySpan<byte> Of(ReadOnlySpan<byte> raw) => raw.Slice(Start, Length);

    /// <summary>
    /// The reader's current value, at <paramref name="offset"/> in the raw fight's bytes. An object or an array is read to its
    /// end.
    /// </summary>
    public static RawSlice Read(ref Utf8JsonReader reader, int offset)
    {
        var token = reader.TokenType;
        if (token == JsonTokenType.Null) return default;
        int start = (int)reader.TokenStartIndex;
        if (token is JsonTokenType.StartObject or JsonTokenType.StartArray) reader.Skip();
        return new RawSlice(offset + start, (int)reader.BytesConsumed - start, token);
    }

    /// <summary>
    /// Writes the value as the replay script has a copied value: a number or a literal as its text, a string, an object or
    /// an array escaped as the writer escapes, and JSON null for none.
    /// </summary>
    public void WriteTo(Utf8JsonWriter writer, ReadOnlySpan<byte> raw)
    {
        switch (Token)
        {
            case JsonTokenType.None:
                writer.WriteNullValue();
                break;
            case JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False:
                writer.WriteRawValue(Of(raw), skipInputValidation: true);
                break;
            case JsonTokenType.String:
                WriteString(writer, raw);
                break;
            default:
                using (var value = JsonDocument.Parse(Of(raw).ToArray())) value.RootElement.WriteTo(writer);
                break;
        }
    }

    /// <summary>Writes a string value unescaped, then escaped as the writer escapes.</summary>
    public void WriteString(Utf8JsonWriter writer, ReadOnlySpan<byte> raw)
    {
        var text = Of(raw)[1..^1];
        if (text.IndexOf((byte)'\\') < 0)
        {
            writer.WriteStringValue(text);
            return;
        }
        var reader = new Utf8JsonReader(Of(raw));
        reader.Read();
        writer.WriteStringValue(reader.GetString());
    }
}
