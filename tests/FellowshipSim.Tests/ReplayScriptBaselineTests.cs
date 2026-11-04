using System.Security.Cryptography;
using System.Text.Json;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Every downloaded fight (data/fights) against the replay scripts built from it at commit 080bdfb (the JsonNode builder):
/// data/script-baseline/scripts.tsv has, for each player of each fight, the report code, fight ID and player ID, then the
/// script's UTF-8 length and SHA-256, or <c>error</c> and the exception's type and message. Without fights or the file
/// the tests do nothing.
/// </summary>
public class ReplayScriptBaselineTests(ITestOutputHelper output)
{
    private static readonly string Baseline = Path.Combine(TestPaths.RepositoryRoot, "data", "script-baseline", "scripts.tsv");

    [Fact]
    public async Task Every_Players_Script_Equals_The_Baseline()
    {
        if (!File.Exists(Baseline))
        {
            output.WriteLine($"No baseline at {Baseline}.");
            return;
        }
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        var expected = File.ReadAllLines(Baseline).Select(l => l.Split('\t'))
            .ToDictionary(p => (p[0], int.Parse(p[1]), int.Parse(p[2])), p => string.Join('\t', p[3..]));
        var differences = new List<string>();
        int compared = 0;
        foreach (var (reportCode, fightId) in library.ListDownloadedFights())
        {
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId);
            foreach (var player in ReplayScriptBuilder.Players(raw))
            {
                if (!expected.Remove((reportCode, fightId, player.Id), out var want)) continue;
                string got;
                try
                {
                    byte[] script = ReplayScriptBuilder.Build(raw, player.Id);
                    got = $"{script.Length}\t{Convert.ToHexString(SHA256.HashData(script))}";
                }
                catch (Exception e)
                {
                    got = $"error\t{e.GetType().Name}: {e.Message}";
                }
                compared++;
                if (got != want) differences.Add($"{reportCode} fight {fightId} player {player.Id}: {got} (baseline {want})");
            }
        }
        output.WriteLine($"{compared} scripts compared.");
        Assert.Empty(differences);
        // A baseline player of a downloaded fight that Players no longer finds.
        var fights = library.ListDownloadedFights().ToHashSet();
        Assert.Empty(expected.Keys.Where(k => fights.Contains((k.Item1, k.Item2))));
    }

    [Fact]
    public async Task Every_Downloaded_Fight_Is_In_Time_Order()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        var outOfOrder = new List<string>();
        foreach (var (reportCode, fightId) in library.ListDownloadedFights())
        {
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId);
            if (FirstOutOfOrder(raw) is { } timestamp) outOfOrder.Add($"{reportCode} fight {fightId} at {timestamp}");
        }
        Assert.Empty(outOfOrder);
    }

    // The timestamp of the first event earlier than the one before it.
    private static double? FirstOutOfOrder(byte[] raw)
    {
        var reader = new Utf8JsonReader(raw);
        reader.Read();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool events = reader.ValueTextEquals("events"u8);
            reader.Read();
            if (!events || reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                continue;
            }
            double last = double.NegativeInfinity;
            while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                double timestamp = 0;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    bool isTimestamp = reader.ValueTextEquals("timestamp"u8);
                    reader.Read();
                    if (isTimestamp && reader.TokenType == JsonTokenType.Number) timestamp = reader.GetDouble();
                    reader.Skip();
                }
                if (timestamp < last) return timestamp;
                last = timestamp;
            }
        }
        return null;
    }
}
