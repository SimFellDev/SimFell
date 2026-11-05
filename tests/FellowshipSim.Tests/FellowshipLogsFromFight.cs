using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FellowshipSim.Logs;

namespace FellowshipSim.Tests;

/// <summary>
/// FellowshipLogs for one raw fight: its report and fight, and each events query's events from a time in pages of
/// <paramref name="pageSize"/>, each page's next timestamp that of the event after it (so events at that time are in both
/// pages, as FellowshipLogs has them). An events query's events are those FellowshipLogs returns for its variables: with a
/// <c>sourceID</c>, the events with that actor as source or as target; <c>Debuffs</c> or <c>Buffs</c> of
/// <c>Enemies</c>, those auras on enemies; <c>Deaths</c> of <c>Enemies</c>, enemies' deaths; <c>Casts</c> of
/// <c>Enemies</c>, enemies' casts and begincasts except their melee swings (the ability <c>Attack</c>); a <c>filterExpression</c> of
/// types (<c>type = "a" or type = "b"</c>), the events of those types; and otherwise every event.
/// </summary>
internal sealed class FellowshipLogsFromFight(byte[] fight, int pageSize = 10000) : HttpMessageHandler
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
                var variables = query.RootElement.GetProperty("variables");
                double from = variables.GetProperty("startTime").GetDouble();
                var events = root.GetProperty("events").EnumerateArray().Where(Selects(root, variables)).ToList();
                int first = events.FindIndex(e => e.GetProperty("timestamp").GetDouble() >= from);
                if (first < 0) first = events.Count;
                var page = events.Skip(first).Take(pageSize).Select(e => e.GetRawText());
                string next = first + pageSize < events.Count ? events[first + pageSize].GetProperty("timestamp").GetRawText() : "null";
                body = """{"data":{"reportData":{"report":{"events":{"data":[""" + string.Join(",", page) + """],"nextPageTimestamp":""" + next + "}}}}}";
            }
            else
            {
                body = """{"data":{"reportData":{"report":{"startTime":""" + root.GetProperty("report_start_time").GetRawText() + ""","fights":[""" +
                       root.GetProperty("fight").GetRawText() + """],"masterData":""" + root.GetProperty("master_data").GetRawText() + "}}}}";
            }
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    // The events of the fight an events query with these variables returns.
    private static Func<JsonElement, bool> Selects(JsonElement root, JsonElement variables)
    {
        var enemies = root.GetProperty("master_data").GetProperty("actors").EnumerateArray()
            .Where(a => a.GetProperty("type").GetString() == "NPC").Select(a => a.GetProperty("id").GetInt32()).ToHashSet();
        // FellowshipLogs' casts leave out the melee swings (T2qLPpmMkrAtJ9B1 fight 18: 1,845 enemy casts of 4,586).
        var attacks = root.GetProperty("master_data").GetProperty("abilities").EnumerateArray()
            .Where(a => a.GetProperty("name").GetString() == "Attack").Select(a => a.GetProperty("gameID").GetInt64()).ToHashSet();
        int source = variables.TryGetProperty("sourceID", out var s) ? s.GetInt32() : 0;
        string dataType = variables.TryGetProperty("dataType", out var d) ? d.GetString()! : "All";
        string hostility = variables.TryGetProperty("hostilityType", out var h) ? h.GetString()! : "Friendlies";
        string filter = variables.TryGetProperty("filterExpression", out var f) ? f.GetString()! : "";
        var types = Regex.Matches(filter, "type = \"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        string[] buffs = ["applybuff", "applybuffstack", "removebuff", "removebuffstack", "refreshbuff"];
        string[] debuffs = ["applydebuff", "applydebuffstack", "removedebuff", "removedebuffstack", "refreshdebuff"];
        return e =>
        {
            string type = e.GetProperty("type").GetString()!;
            int? from = Actor(e, "sourceID"), to = Actor(e, "targetID");
            bool onEnemy = to is { } t && enemies.Contains(t);
            if (source != 0) return from == source || to == source;
            if (types.Count > 0) return types.Contains(type);
            return (dataType, hostility) switch
            {
                ("Debuffs", "Enemies") => debuffs.Contains(type) && onEnemy,
                ("Buffs", "Enemies") => buffs.Contains(type) && onEnemy,
                ("Deaths", "Enemies") => type == "death" && onEnemy,
                ("Casts", "Enemies") => type is "cast" or "begincast" && from is { } f && enemies.Contains(f)
                    && !(e.TryGetProperty("abilityGameID", out var a) && attacks.Contains(a.GetInt64())),
                _ => true,
            };
        };
    }

    private static int? Actor(JsonElement e, string name) =>
        e.TryGetProperty(name, out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt32() : null;
}
