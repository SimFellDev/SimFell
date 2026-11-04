using System.Net;
using System.Text;
using System.Text.Json;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// The first downloaded fight in data/fights downloaded again from FellowshipLogs' answers in pages of 10,000 events, each
/// page's next timestamp that of the event after it (so events at that time are in both pages, as FellowshipLogs has them):
/// every player's replay script from it equals the script from the fight as it was. Without fights it does nothing.
/// </summary>
public class FightRedownloadTests(ITestOutputHelper output)
{
    private const int PageSize = 10000;

    [Fact]
    public async Task A_Fight_Downloaded_Again_Builds_The_Same_Scripts()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        if (library.ListDownloadedFights() is not [var (reportCode, fightId), ..])
        {
            output.WriteLine($"No fights in {library.FightsDirectory}.");
            return;
        }
        byte[] fight = await library.GetRawFightBytesAsync(null, reportCode, fightId);
        var again = new MemoryStream();
        await new FellowshipLogsClient(new HttpClient(new FromFight(fight)), "id", "secret").DownloadFightAsync(reportCode, fightId, again);

        var players = ReplayScriptBuilder.Players(fight);
        Assert.NotEmpty(players);
        foreach (var player in players)
        {
            byte[] expected, actual;
            try
            {
                expected = ReplayScriptBuilder.Build(fight, player.Id);
            }
            catch (ArgumentException)
            {
                continue;
            }
            actual = ReplayScriptBuilder.Build(again.ToArray(), player.Id);
            Assert.True(expected.AsSpan().SequenceEqual(actual), $"Player {player.Id}'s script differs.");
        }
    }

    // FellowshipLogs for one raw fight: its report and fight, and its events from a time in pages.
    private sealed class FromFight(byte[] fight) : HttpMessageHandler
    {
        private readonly JsonDocument _fight = JsonDocument.Parse(fight);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var root = _fight.RootElement;
            string body;
            if (request.RequestUri!.ToString() == FellowshipLogsClient.TokenUrl) body = """{ "access_token": "token", "expires_in": 3600 }""";
            else
            {
                using var query = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                if (query.RootElement.GetProperty("query").GetString()!.Contains("events("))
                {
                    double from = query.RootElement.GetProperty("variables").GetProperty("startTime").GetDouble();
                    var events = root.GetProperty("events").EnumerateArray().ToList();
                    int first = events.FindIndex(e => e.GetProperty("timestamp").GetDouble() >= from);
                    if (first < 0) first = events.Count;
                    var page = events.Skip(first).Take(PageSize).Select(e => e.GetRawText());
                    string next = first + PageSize < events.Count ? events[first + PageSize].GetProperty("timestamp").GetRawText() : "null";
                    body = """{"data":{"reportData":{"report":{"events":{"data":[""" + string.Join(",", page) + """],"nextPageTimestamp":""" + next + "}}}}}";
                }
                else
                {
                    body = """{"data":{"reportData":{"report":{"startTime":""" + root.GetProperty("report_start_time").GetRawText() + ""","fights":[""" + root.GetProperty("fight").GetRawText() + """],"masterData":""" + root.GetProperty("master_data").GetRawText() + "}}}}";
                }
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
