using System.Text;
using System.Text.Json.Nodes;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The Language Server Protocol's base protocol over two streams: each message is a <c>Content-Length</c> header, a blank
/// line and a UTF-8 JSON-RPC body.
/// </summary>
public sealed class LspConnection(Stream input, Stream output)
{
    private readonly Lock _writing = new();

    /// <summary>The next message, or null at the end of the input.</summary>
    public async Task<JsonObject?> ReadAsync(CancellationToken ct = default)
    {
        int length = -1;
        while (await ReadHeaderLineAsync(ct) is { } line)
        {
            if (line.Length == 0)
            {
                if (length < 0) continue;
                var body = new byte[length];
                await input.ReadExactlyAsync(body, ct);
                return JsonNode.Parse(body)?.AsObject();
            }
            int colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line[(colon + 1)..].Trim());
        }
        return null;
    }

    public void Write(JsonObject message)
    {
        byte[] body = Encoding.UTF8.GetBytes(message.ToJsonString());
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        lock (_writing)
        {
            output.Write(header);
            output.Write(body);
            output.Flush();
        }
    }

    private async Task<string?> ReadHeaderLineAsync(CancellationToken ct)
    {
        var line = new StringBuilder();
        var one = new byte[1];
        while (true)
        {
            if (await input.ReadAsync(one, ct) == 0) return null;
            if (one[0] == '\n') return line.ToString().TrimEnd('\r');
            line.Append((char)one[0]);
        }
    }
}
