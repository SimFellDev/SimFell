using System.Net;
using System.Text;
using System.Text.Json;
using FellowshipSim.Logs;

namespace FellowshipSim.Tests;

/// <summary>
/// A player's fight download: its six events queries and their merge in time order, a page formatted for reading, and
/// FellowshipLogs answering an events query with no events, as it answered report rf7qpV3tATCx86hN fight 6 once (one
/// query, and an error naming what is missing).
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

    // An events answer of one page with these events.
    private static string Page(string events) => """{"data":{"reportData":{"report":{"events":{"data":""" + events + ""","nextPageTimestamp":null}}}}}""";

    // True for the events query of the player's own events.
    private static bool PlayerStream(JsonElement variables) => variables.GetProperty("sourceID").GetInt32() != 0;

    // FellowshipLogs: the token, the fight's actors and pulls, and each events query's answer from its variables.
    private sealed class FellowshipLogs(Func<JsonElement, string> events) : HttpMessageHandler
    {
        public readonly List<JsonElement> EventsQueries = [];

        // Each answer to an events query in turn, the last one again for any query after it.
        public FellowshipLogs(params string[] eventsAnswers) : this(InTurn(eventsAnswers)) { }

        private static Func<JsonElement, string> InTurn(string[] answers)
        {
            var queue = new Queue<string>(answers);
            return _ => queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        }

        /// <summary>The answer to every query that is no events query.</summary>
        public string Answer = Meta;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string body;
            string content = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            if (request.RequestUri!.ToString() == FellowshipLogsClient.TokenUrl) body = """{ "access_token": "token", "expires_in": 3600 }""";
            else if (content.Contains("events("))
            {
                var variables = JsonDocument.Parse(content).RootElement.GetProperty("variables").Clone();
                EventsQueries.Add(variables);
                body = events(variables);
            }
            else body = Answer;
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

            var e = await Assert.ThrowsAsync<FellowshipLogsException>(() => client.DownloadFightAsync("rf7qpV3tATCx86hN", 6, 1, new MemoryStream()));
            Assert.Equal("FellowshipLogs returned no events for fight 6 of report 'rf7qpV3tATCx86hN' from 0s.", e.Message);
            Assert.Single(logs.EventsQueries);
        }
    }

    [Fact]
    public async Task A_Download_Merges_The_Players_Events_With_The_Fights_Other_Events_In_Time_Order()
    {
        var logs = new FellowshipLogs(v => PlayerStream(v)
            ? Page("""[{"timestamp":120,"type":"cast","sourceID":1},{"timestamp":160,"type":"damage","sourceID":9,"targetID":1}]""")
            : (v.GetProperty("dataType").GetString(), v.GetProperty("filterExpression").GetString()) switch
            {
                ("Debuffs", _) => Page("""[{"timestamp":110,"type":"applydebuff","sourceID":2,"targetID":9},{"timestamp":120,"type":"applydebuff","sourceID":2,"targetID":9},{"timestamp":130,"type":"applydebuff","sourceID":1,"targetID":9}]"""),
                ("Deaths", _) => Page("""[{"timestamp":150,"type":"death","sourceID":2,"targetID":9}]"""),
                ("All", "type = \"dungeonstart\" or type = \"combatantinfo\"") => Page("""[{"timestamp":100,"type":"dungeonstart"}]"""),
                _ => Page("[]"),
            });
        var raw = new MemoryStream();
        await new FellowshipLogsClient(new HttpClient(logs), "id", "secret").DownloadFightAsync("rf7qpV3tATCx86hN", 6, 1, raw);

        Assert.Equal(
            [("Debuffs", "Enemies", 0, ""), ("Buffs", "Enemies", 0, ""), ("Deaths", "Enemies", 0, ""), ("Casts", "Enemies", 0, ""),
             ("All", "Friendlies", 0, "type = \"dungeonstart\" or type = \"combatantinfo\""),
             ("All", "Friendlies", 1, "")],
            logs.EventsQueries.Select(v => (v.GetProperty("dataType").GetString(), v.GetProperty("hostilityType").GetString(),
                v.GetProperty("sourceID").GetInt32(), v.GetProperty("filterExpression").GetString())));
        var fight = JsonDocument.Parse(raw.ToArray()).RootElement;
        Assert.Equal("rf7qpV3tATCx86hN", fight.GetProperty("report_code").GetString());
        Assert.Equal(1000, fight.GetProperty("report_start_time").GetInt32());
        Assert.Equal(6, fight.GetProperty("fight").GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Array, fight.GetProperty("master_data").GetProperty("actors").ValueKind);
        Assert.Equal(JsonValueKind.Null, fight.GetProperty("character").ValueKind);
        // The player's events come first at one time, and the player's debuff comes from the player's events alone.
        Assert.Equal(
            [(100, "dungeonstart", 0), (110, "applydebuff", 2), (120, "cast", 1), (120, "applydebuff", 2), (150, "death", 2), (160, "damage", 9)],
            fight.GetProperty("events").EnumerateArray().Select(e => (e.GetProperty("timestamp").GetInt32(), e.GetProperty("type").GetString(),
                e.TryGetProperty("sourceID", out var s) ? s.GetInt32() : 0)));
    }

    [Fact]
    public async Task A_Dungeon_Fight_Without_Its_Deaths_Table_Is_A_FellowshipLogs_Error()
    {
        // The fight's answer has its pulls and no table.
        var client = new FellowshipLogsClient(new HttpClient(new FellowshipLogs(Events)), "id", "secret");

        var e = await Assert.ThrowsAsync<FellowshipLogsException>(() => client.GetDungeonFightAsync("rf7qpV3tATCx86hN", 6));
        Assert.Equal("FellowshipLogs returned no enemy deaths for fight 6 of report 'rf7qpV3tATCx86hN'.", e.Message);
    }

    // A dungeon fight with enemy 5 and its deaths table; Arck (3) is its tank and sbt (2) a damage dealer.
    private const string DungeonFight = """
        { "data": { "reportData": { "report": { "startTime": 1000, "masterData": { "logVersion": 4 },
          "fights": [ { "id": 6, "startTime": 100, "endTime": 900, "name": "Cithrel's Fall", "difficultyLevel": 50,
                        "gameZone": { "id": 7, "name": "Cithrel's Fall" }, "enemyNPCs": [ { "id": 5, "gameID": 164 } ] } ],
          "table": { "data": { "entries": [ { "id": 5, "instance": 1, "guid": 164, "name": "Icewyrm Sentinel", "timestamp": 800 } ] } },
          "playerDetails": { "data": { "playerDetails": {
            "dps": [ { "id": 2, "name": "sbt", "type": "Mara" } ], "tanks": [ { "id": 3, "name": "Arck", "type": "Xavian" } ] } } } } } } }
        """;

    // The tank's damage done: enemy 5 hit at 300 and 500; its damage taken: enemy 5 hits it at 200, and party member 4 at 250.
    private const string TankDamageDone = """
        { "data": { "reportData": { "report": { "events": { "data": [
          { "timestamp": 300, "type": "damage", "sourceID": 3, "targetID": 5, "targetInstance": 1, "targetResources": { "x": 10, "y": 20 } },
          { "timestamp": 500, "type": "damage", "sourceID": 3, "targetID": 5, "targetInstance": 1, "targetResources": { "x": 90, "y": 90 } } ],
          "nextPageTimestamp": null } } } } }
        """;

    private const string TankDamageTaken = """
        { "data": { "reportData": { "report": { "events": { "data": [
          { "timestamp": 200, "type": "damage", "sourceID": 5, "sourceInstance": 1, "targetID": 3, "sourceResources": { "x": -7, "y": -8 } },
          { "timestamp": 250, "type": "damage", "sourceID": 4, "targetID": 3, "sourceResources": { "x": 1, "y": 1 } } ],
          "nextPageTimestamp": null } } } } }
        """;

    // The fight's dungeonstart event, with its affixes.
    private const string DungeonStart = """
        { "data": { "reportData": { "report": { "events": { "data": [
          { "timestamp": 100, "type": "dungeonstart", "affixes": [4, 6, 24, 25, 22] } ],
          "nextPageTimestamp": null } } } } }
        """;

    [Fact]
    public async Task A_Dungeon_Fight_Has_Its_Tanks_First_And_Last_Contact_With_Each_Enemy()
    {
        var logs = new FellowshipLogs(TankDamageDone, TankDamageTaken, DungeonStart) { Answer = DungeonFight };
        var fight = await new FellowshipLogsClient(new HttpClient(logs), "id", "secret").GetDungeonFightAsync("T2qLPpmMkrAtJ9B1", 6);

        Assert.Equal(3, logs.EventsQueries.Count);
        Assert.Equal("type = \"dungeonstart\"", logs.EventsQueries[2].GetProperty("filterExpression").GetString());
        Assert.Equal([4, 6, 24, 25, 22], fight["affixes"]!.AsArray().Select(a => a!.GetValue<int>()));
        Assert.Equal(3, fight["contact_player"]!["id"]!.GetValue<int>());
        var contact = Assert.Single(fight["enemy_contacts"]!.AsArray())!;
        // The first contact is enemy 5 hitting the tank at 200, where it stood at (-7, -8); the last is the tank's hit at 500.
        Assert.Equal((5, 1, 200.0, -7.0, -8.0, 500.0), (contact["id"]!.GetValue<int>(), contact["instance"]!.GetValue<int>(),
            contact["first"]!.GetValue<double>(), contact["x"]!.GetValue<double>(), contact["y"]!.GetValue<double>(), contact["last"]!.GetValue<double>()));
        Assert.Equal(800, Assert.Single(fight["enemy_deaths"]!.AsArray())!["timestamp"]!.GetValue<double>());
    }

    [Fact]
    public async Task A_Page_Formatted_For_Reading_Downloads_Compact()
    {
        const string formatted = "{ \"data\": { \"reportData\": { \"report\": { \"events\": {\n  \"data\": [\n    {\n      \"timestamp\": 100,\n      \"type\": \"dungeonstart\"\n    }\n  ],\n  \"nextPageTimestamp\": null } } } } }";
        var raw = new MemoryStream();
        await new FellowshipLogsClient(new HttpClient(new FellowshipLogs(v => PlayerStream(v) ? formatted : Page("[]"))), "id", "secret")
            .DownloadFightAsync("rf7qpV3tATCx86hN", 6, 1, raw);

        Assert.EndsWith("\"events\":[{\"timestamp\":100,\"type\":\"dungeonstart\"}]}", Encoding.UTF8.GetString(raw.ToArray()));
    }

    [Fact]
    public async Task A_Compact_Page_Downloads_As_FellowshipLogs_Has_It()
    {
        const string events = """[{"timestamp":100,"type":"dungeonstart","name":"Cithrel's Fall"},null,{"timestamp":150,"type":"death"}]""";
        var raw = new MemoryStream();
        await new FellowshipLogsClient(new HttpClient(new FellowshipLogs(v => Page(PlayerStream(v) ? events : "[]"))), "id", "secret")
            .DownloadFightAsync("rf7qpV3tATCx86hN", 6, 1, raw);

        Assert.EndsWith($"\"events\":{events}}}", Encoding.UTF8.GetString(raw.ToArray()));
    }

    [Theory]
    [InlineData("""{ "errors": [ { "message": "Too many points" } ], "data": null }""", "FellowshipLogs API error: [{\"message\":\"Too many points\"}]")]
    [InlineData("""{ "data": null }""", "FellowshipLogs API response has no data.")]
    [InlineData("null", "FellowshipLogs API returned an empty response.")]
    public async Task An_Answer_With_Errors_Or_No_Data_Is_A_FellowshipLogs_Error(string answer, string message)
    {
        var client = new FellowshipLogsClient(new HttpClient(new FellowshipLogs(answer)), "id", "secret");

        var download = await Assert.ThrowsAsync<FellowshipLogsException>(() => client.DownloadFightAsync("rf7qpV3tATCx86hN", 6, 1, new MemoryStream()));
        var query = await Assert.ThrowsAsync<FellowshipLogsException>(() => client.QueryAsync("events(", null));
        Assert.Equal(message, download.Message);
        Assert.Equal(message, query.Message);
    }
}
