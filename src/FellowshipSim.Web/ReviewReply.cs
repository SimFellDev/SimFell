using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using FellowshipSim.Engine;
using FellowshipSim.Logs;

namespace FellowshipSim.Web;

/// <summary>
/// A replay's answer from a worker: its review, or the reason the engine refused it. It crosses from the worker to the page
/// as bytes (<see cref="ToBytes"/>): the review's script, page, summary text and hits are tens of MB, and as JSON string
/// values each one's escaping rents buffers several times its size. The shared array pool keeps those buffers for the
/// worker's life.
/// </summary>
public sealed record ReviewReply(ReviewArtifacts? Review, string? Error)
{
    // The head: the summary or the error, as JSON.
    private sealed record Head(ReviewSummary? Summary, string? Error);

    /// <summary>
    /// The reply as bytes: the head's UTF-8 JSON, then the review's script, page, summary text and hits as UTF-8, each after
    /// its length as a 32-bit little-endian integer.
    /// </summary>
    public byte[] ToBytes()
    {
        byte[] head = JsonSerializer.SerializeToUtf8Bytes(new Head(Review?.Summary, Error), SimChunks.Json);
        string[] texts = Review is { } r ? [r.ScriptJson, r.Html, r.SummaryText, r.HitsCsv] : [];
        var bytes = new byte[4 + head.Length + texts.Sum(t => 4 + Encoding.UTF8.GetByteCount(t))];
        var rest = bytes.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(rest, head.Length);
        head.CopyTo(rest[4..]);
        rest = rest[(4 + head.Length)..];
        foreach (string text in texts)
        {
            int length = Encoding.UTF8.GetBytes(text, rest[4..]);
            BinaryPrimitives.WriteInt32LittleEndian(rest, length);
            rest = rest[(4 + length)..];
        }
        return bytes;
    }

    /// <summary>The reply that <see cref="ToBytes"/> wrote.</summary>
    public static ReviewReply FromBytes(ReadOnlySpan<byte> bytes)
    {
        var head = JsonSerializer.Deserialize<Head>(Next(ref bytes), SimChunks.Json)!;
        if (head.Summary is not { } summary) return new ReviewReply(null, head.Error);
        string scriptJson = Encoding.UTF8.GetString(Next(ref bytes)), html = Encoding.UTF8.GetString(Next(ref bytes));
        string summaryText = Encoding.UTF8.GetString(Next(ref bytes)), hitsCsv = Encoding.UTF8.GetString(Next(ref bytes));
        return new ReviewReply(new ReviewArtifacts(summary, scriptJson, html, summaryText, hitsCsv), head.Error);
    }

    // The next length-prefixed part, and the bytes after it.
    private static ReadOnlySpan<byte> Next(ref ReadOnlySpan<byte> bytes)
    {
        int length = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var part = bytes.Slice(4, length);
        bytes = bytes[(4 + length)..];
        return part;
    }
}
