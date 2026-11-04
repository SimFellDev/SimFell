using System.Net;
using System.Text;
using FellowshipSim.Logs;

namespace FellowshipSim.Tests;

/// <summary>
/// A fight's download when FellowshipLogs answers an events query with no events, as it answered report rf7qpV3tATCx86hN
/// fight 6 once: one query, and an error naming what is missing.
/// </summary>
public class FellowshipLogsDownloadTests
{
    private const string Meta = """
        { "data": { "reportData": { "report": { "startTime": 1000,
          "fights": [ { "id": 6, "startTime": 100, "endTime": 200, "name": "Wraithtide Vault", "friendlyPlayers": [1], "enemyNPCs": [], "dungeonPulls": [] } ],
          "masterData": { "actors": [], "abilities": [] } } } } }
        """;

    private const string NoEvents = """{ "data": { "reportData": { "report": { "events": null } } } }""";
    private const string NoReport = """{ "data": { "reportData": { "report": null } } }""";
    private const string Events = """
        { "data": { "reportData": { "report": { "events": { "data": [ { "timestamp": 100, "type": "dungeonstart" }, { "timestamp": 150, "type": "death" } ], "nextPageTimestamp": null } } } } }
        """;

    // FellowshipLogs: the token, the fight's actors and pulls, and then each answer to an events query in turn.
    private sealed class FellowshipLogs(params string[] eventsAnswers) : HttpMessageHandler
    {
        private readonly Queue<string> _events = new(eventsAnswers);
        public int EventsQueries;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string body;
            if (request.RequestUri!.ToString() == FellowshipLogsClient.TokenUrl) body = """{ "access_token": "token", "expires_in": 3600 }""";
            else if ((await request.Content!.ReadAsStringAsync(ct)).Contains("events("))
            {
                EventsQueries++;
                body = _events.Count > 1 ? _events.Dequeue() : _events.Peek();
            }
            else body = Meta;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task A_Page_Without_Events_Is_A_FellowshipLogs_Error()
    {
        foreach (string answer in new[] { NoEvents, NoReport })
        {
            var logs = new FellowshipLogs(answer, Events);
            var client = new FellowshipLogsClient(new HttpClient(logs), "id", "secret");

            var e = await Assert.ThrowsAsync<FellowshipLogsException>(() => client.DownloadFightAsync("rf7qpV3tATCx86hN", 6));
            Assert.Equal("FellowshipLogs returned no events for fight 6 of report 'rf7qpV3tATCx86hN' from 0s.", e.Message);
            Assert.Equal(1, logs.EventsQueries);
        }
    }

    [Fact]
    public async Task A_Page_With_Events_Downloads()
    {
        var logs = new FellowshipLogs(Events);
        var raw = await new FellowshipLogsClient(new HttpClient(logs), "id", "secret").DownloadFightAsync("rf7qpV3tATCx86hN", 6);

        Assert.Equal(1, logs.EventsQueries);
        Assert.Equal(2, raw["events"]!.AsArray().Count);
    }

    [Fact]
    public async Task A_Dungeon_Fight_Without_Its_Deaths_Table_Is_A_FellowshipLogs_Error()
    {
        // The fight's answer has its pulls and no table.
        var client = new FellowshipLogsClient(new HttpClient(new FellowshipLogs(Events)), "id", "secret");

        var e = await Assert.ThrowsAsync<FellowshipLogsException>(() => client.GetDungeonFightAsync("rf7qpV3tATCx86hN", 6));
        Assert.Equal("FellowshipLogs returned no enemy deaths for fight 6 of report 'rf7qpV3tATCx86hN'.", e.Message);
    }
}
