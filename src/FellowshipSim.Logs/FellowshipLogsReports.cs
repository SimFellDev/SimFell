using System.Text.Json;
using System.Text.Json.Nodes;

namespace FellowshipSim.Logs;

/// <summary>A report's fights and who played in each, and its combat-log format version (<see cref="GamePatch.LogVersion"/>).</summary>
public sealed record ReportSummary(string Code, string Title, long StartTime, IReadOnlyList<FightSummary> Fights, int? LogVersion = null);

public sealed record FightSummary(int Id, string Name, bool Kill, double DurationSeconds, IReadOnlyList<ReportPlayer> Players);

/// <summary>A player in a report: <see cref="Id"/> is the report's actor ID (the "source" in a FellowshipLogs link).</summary>
public sealed record ReportPlayer(int Id, string Name, string Hero);

/// <summary>Report queries: the fight list of a report, and the events of one fight that a player's replay reads.</summary>
public static class FellowshipLogsReports
{
    private const string SummaryQuery = """
        query($code: String!) {
          reportData {
            report(code: $code) {
              title startTime
              fights { id name kill startTime endTime friendlyPlayers }
              masterData { logVersion actors(type: "Player") { id name subType } }
            }
          }
        }
        """;

    private const string FightMetaQuery = """
        query($code: String!, $fightIDs: [Int]!) {
          reportData {
            report(code: $code) {
              startTime
              fights(fightIDs: $fightIDs) {
                id startTime endTime name friendlyPlayers enemyNPCs { id gameID instanceCount }
                dungeonPulls { id name startTime endTime enemyNPCs { id gameID minimumInstanceID maximumInstanceID } }
              }
              masterData {
                actors { id gameID name type subType petOwner }
                abilities { gameID name type icon }
              }
            }
          }
        }
        """;

    // sourceID 0 is every source, and an empty filterExpression is every event. With a player's sourceID, the events are
    // those with the player as source or as target.
    private const string EventsQuery = """
        query($code: String!, $fightIDs: [Int]!, $startTime: Float!, $endTime: Float!, $sourceID: Int!, $dataType: EventDataType!,
              $hostilityType: HostilityType!, $filterExpression: String!) {
          reportData {
            report(code: $code) {
              events(fightIDs: $fightIDs, startTime: $startTime, endTime: $endTime, sourceID: $sourceID, dataType: $dataType,
                     hostilityType: $hostilityType, filterExpression: $filterExpression, limit: 10000, includeResources: true) {
                data nextPageTimestamp
              }
            }
          }
        }
        """;

    // The events of a fight a player's replay reads besides the player's own: buffs and debuffs on enemies (external auras,
    // and each enemy's health and lifetime), enemies' deaths, enemies' casts (each enemy's arrival), the dungeon start
    // (the affixes) and every player's combatantinfo (a buff another player gives scales with the giver's ranks).
    private static readonly EventStream[] FightStreams =
    [
        new(0, "Debuffs", "Enemies", ""),
        new(0, "Buffs", "Enemies", ""),
        new(0, "Deaths", "Enemies", ""),
        new(0, "Casts", "Enemies", ""),
        new(0, "All", "Friendlies", "type = \"dungeonstart\" or type = \"combatantinfo\""),
    ];

    private const string FightTimesQuery = """
        query($code: String!, $fightIDs: [Int]!) {
          reportData { report(code: $code) { fights(fightIDs: $fightIDs) { startTime endTime } } }
        }
        """;

    private const string DungeonFightQuery = """
        query($code: String!, $fightIDs: [Int]!) {
          reportData {
            report(code: $code) {
              startTime
              masterData { logVersion }
              fights(fightIDs: $fightIDs) {
                id name startTime endTime difficultyLevel gameZone { id name } enemyNPCs { id gameID }
              }
              table(fightIDs: $fightIDs, dataType: Deaths, hostilityType: Enemies)
              playerDetails(fightIDs: $fightIDs)
            }
          }
        }
        """;

    private const string PlayerDamageQuery = """
        query($code: String!, $fightIDs: [Int]!, $sourceID: Int!, $dataType: EventDataType!, $startTime: Float!, $endTime: Float!) {
          reportData {
            report(code: $code) {
              events(fightIDs: $fightIDs, dataType: $dataType, sourceID: $sourceID, startTime: $startTime, endTime: $endTime,
                limit: 10000, includeResources: true) {
                data nextPageTimestamp
              }
            }
          }
        }
        """;

    private const string CombatantInfoQuery = """
        query($code: String!, $fightIDs: [Int]!, $sourceID: Int!, $startTime: Float!, $endTime: Float!) {
          reportData {
            report(code: $code) {
              events(fightIDs: $fightIDs, dataType: CombatantInfo, sourceID: $sourceID, startTime: $startTime, endTime: $endTime, limit: 10) {
                data
              }
            }
          }
        }
        """;

    /// <summary>
    /// The <c>combatantinfo</c> event of player <paramref name="playerId"/> in one fight (stats, talents and gear), or null
    /// when the fight has none for the player: one small query instead of the fight's every event.
    /// </summary>
    public static async Task<JsonObject?> GetCombatantInfoAsync(this FellowshipLogsClient client, string reportCode, int fightId,
        int playerId, CancellationToken ct = default)
    {
        var fight = await ReportPartAsync(client, FightTimesQuery, new { code = reportCode, fightIDs = new[] { fightId } },
            r => r["fights"]?.AsArray().FirstOrDefault() is { } f && f["startTime"] is JsonValue && f["endTime"] is JsonValue ? f : null,
            $"fight {fightId} of report '{reportCode}'", ct);
        var data = (await client.QueryAsync(CombatantInfoQuery, new
        {
            code = reportCode,
            fightIDs = new[] { fightId },
            sourceID = playerId,
            startTime = fight["startTime"]!.GetValue<double>(),
            endTime = fight["endTime"]!.GetValue<double>(),
        }, ct))["reportData"]?["report"]?["events"]?["data"]?.AsArray();
        return data?.FirstOrDefault(e => e?["type"]?.GetValue<string>() == "combatantinfo" && e["sourceID"]?.GetValue<int>() == playerId)
            ?.DeepClone().AsObject();
    }

    /// <summary>
    /// One dungeon fight, with one player's damage events in place of its every event: <c>report_code</c>;
    /// <c>report_start_time</c> and <c>log_version</c>, the report's start and combat-log format version (null when
    /// FellowshipLogs returns none); <c>fight</c> (id, name, startTime, endTime, difficultyLevel, gameZone with its id and
    /// name, and enemyNPCs with their id and gameID); <c>enemy_deaths</c>, each enemy death's actor <c>id</c>,
    /// <c>instance</c>, <c>gameID</c>, <c>name</c> and <c>timestamp</c>; <c>contact_player</c>, the player whose damage
    /// events are read (<see cref="ContactPlayer"/>); and <c>enemy_contacts</c>, each enemy instance that player hit or was
    /// hit by: actor <c>id</c>, <c>instance</c>, the <c>first</c> such event's timestamp with the enemy's <c>x</c> and
    /// <c>y</c> there, and the <c>last</c> event's timestamp.
    /// </summary>
    public static async Task<JsonObject> GetDungeonFightAsync(this FellowshipLogsClient client, string reportCode, int fightId,
        CancellationToken ct = default)
    {
        var report = await ReportPartAsync(client, DungeonFightQuery, new { code = reportCode, fightIDs = new[] { fightId } },
            r => r["fights"]?.AsArray().FirstOrDefault() is { } f && f["startTime"] is JsonValue && f["endTime"] is JsonValue ? r : null,
            $"fight {fightId} of report '{reportCode}'", ct);
        var fight = report["fights"]!.AsArray()[0]!;
        var entries = report["table"]?["data"]?["entries"] as JsonArray
            ?? throw new FellowshipLogsException($"FellowshipLogs returned no enemy deaths for fight {fightId} of report '{reportCode}'.");
        // Each death's actor, instance, creature, name and time alone: an entry also has the damage and events before the death.
        var deaths = new JsonArray(entries
            .Where(e => e?["id"] is JsonValue)
            .Select(e => (JsonNode)new JsonObject
            {
                ["id"] = e!["id"]!.GetValue<int>(),
                ["instance"] = e["instance"]?.GetValue<int?>() ?? 1,
                ["gameID"] = e["guid"]?.GetValue<int?>() ?? 0,
                ["name"] = e["name"]?.GetValue<string>() ?? "",
                ["timestamp"] = e["timestamp"]?.GetValue<double>() ?? 0,
            })
            .ToArray());
        var player = ContactPlayer(report["playerDetails"]?["data"]?["playerDetails"])
            ?? throw new FellowshipLogsException($"FellowshipLogs returned no players for fight {fightId} of report '{reportCode}'.");
        var enemies = (fight["enemyNPCs"]?.AsArray() ?? []).Select(n => n?["id"]?.GetValue<int>() ?? -1).ToHashSet();
        var contacts = await EnemyContactsAsync(client, reportCode, fightId, player.Id, fight["startTime"]!.GetValue<double>(),
            fight["endTime"]!.GetValue<double>(), enemies, ct);
        return new JsonObject
        {
            ["report_code"] = reportCode,
            ["report_start_time"] = report["startTime"]?.DeepClone(),
            ["log_version"] = report["masterData"]?["logVersion"]?.DeepClone(),
            ["fight"] = fight.DeepClone(),
            ["enemy_deaths"] = deaths,
            ["contact_player"] = new JsonObject { ["id"] = player.Id, ["name"] = player.Name, ["hero"] = player.Hero },
            ["enemy_contacts"] = contacts,
        };
    }

    /// <summary>
    /// The player whose damage events show each enemy's first contact: the fight's first tank, who reaches each pack first;
    /// with no tank, its first damage dealer, then its first healer. <paramref name="details"/> is FellowshipLogs'
    /// <c>playerDetails</c> (<c>tanks</c>, <c>healers</c> and <c>dps</c>). Null for a fight without players.
    /// </summary>
    public static ReportPlayer? ContactPlayer(JsonNode? details) =>
        new[] { "tanks", "dps", "healers" }
            .SelectMany(role => details?[role]?.AsArray() ?? [])
            .Where(p => p?["id"] is JsonValue)
            .Select(p => new ReportPlayer(p!["id"]!.GetValue<int>(), p["name"]?.GetValue<string>() ?? "?", p["type"]?.GetValue<string>() ?? ""))
            .FirstOrDefault();

    // Each enemy instance's first and last damage event with player playerId, dealt or taken, and the enemy's position at
    // the first.
    private static async Task<JsonArray> EnemyContactsAsync(FellowshipLogsClient client, string reportCode, int fightId, int playerId,
        double start, double end, HashSet<int> enemies, CancellationToken ct)
    {
        var contacts = new Dictionary<(int Id, int Instance), (double First, double X, double Y, double Last)>();
        foreach (var (dataType, enemySide) in new[] { ("DamageDone", "target"), ("DamageTaken", "source") })
        {
            double? cursor = start;
            while (cursor is { } from && from < end)
            {
                byte[] answer = await client.QueryUtf8Async(PlayerDamageQuery, new
                {
                    code = reportCode,
                    fightIDs = new[] { fightId },
                    sourceID = playerId,
                    dataType,
                    startTime = from,
                    endTime = end,
                }, ct);
                using var document = JsonDocument.Parse(answer);
                if (!(document.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("reportData", out var reportData)
                    && reportData.TryGetProperty("report", out var report) && report.TryGetProperty("events", out var events)
                    && events.ValueKind == JsonValueKind.Object))
                {
                    throw new FellowshipLogsException($"FellowshipLogs returned no damage events of player {playerId} in fight {fightId} of report '{reportCode}'.");
                }
                foreach (var e in events.GetProperty("data").EnumerateArray())
                {
                    if (!e.TryGetProperty($"{enemySide}ID", out var idValue) || !enemies.Contains(idValue.GetInt32())) continue;
                    int instance = e.TryGetProperty($"{enemySide}Instance", out var i) ? i.GetInt32() : 1;
                    double t = e.GetProperty("timestamp").GetDouble();
                    var key = (idValue.GetInt32(), instance);
                    if (contacts.TryGetValue(key, out var known))
                    {
                        if (t >= known.First)
                        {
                            contacts[key] = known with { Last = Math.Max(known.Last, t) };
                            continue;
                        }
                    }
                    if (!e.TryGetProperty($"{enemySide}Resources", out var resources) || !resources.TryGetProperty("x", out var x)) continue;
                    contacts[key] = (t, x.GetDouble(), resources.GetProperty("y").GetDouble(), Math.Max(known.Last, t));
                }
                double? next = events.TryGetProperty("nextPageTimestamp", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetDouble() : null;
                if (next is not { } after || after <= from) break;
                cursor = after;
            }
        }
        return new JsonArray(contacts
            .OrderBy(c => c.Value.First)
            .Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Key.Id,
                ["instance"] = c.Key.Instance,
                ["first"] = c.Value.First,
                ["x"] = c.Value.X,
                ["y"] = c.Value.Y,
                ["last"] = c.Value.Last,
            })
            .ToArray());
    }

    public static async Task<ReportSummary> GetSummaryAsync(this FellowshipLogsClient client, string reportCode, CancellationToken ct = default)
    {
        var data = await client.QueryAsync(SummaryQuery, new { code = reportCode }, ct);
        var report = data["reportData"]?["report"] ?? throw new FellowshipLogsException($"Report '{reportCode}' was not found.");
        var players = (report["masterData"]?["actors"]?.AsArray() ?? [])
            .Where(a => a != null)
            .ToDictionary(a => a!["id"]!.GetValue<int>(), a => new ReportPlayer(
                a!["id"]!.GetValue<int>(), a["name"]?.GetValue<string>() ?? "?", a["subType"]?.GetValue<string>() ?? ""));
        var fights = new List<FightSummary>();
        foreach (var f in report["fights"]?.AsArray() ?? [])
        {
            if (f == null) continue;
            var ids = f["friendlyPlayers"]?.AsArray().Select(x => x!.GetValue<int>()) ?? [];
            fights.Add(new FightSummary(
                f["id"]!.GetValue<int>(),
                f["name"]?.GetValue<string>() ?? "",
                f["kill"]?.GetValue<bool?>() ?? false,
                (f["endTime"]!.GetValue<double>() - f["startTime"]!.GetValue<double>()) / 1000.0,
                ids.Where(players.ContainsKey).Select(id => players[id]).ToList()));
        }
        return new ReportSummary(reportCode, report["title"]?.GetValue<string>() ?? reportCode,
            report["startTime"]?.GetValue<long>() ?? 0, fights, report["masterData"]?["logVersion"]?.GetValue<int?>());
    }

    /// <summary>
    /// Downloads the events of one fight that player <paramref name="playerId"/>'s replay reads, with the report's actors
    /// and abilities, as a raw fight (the input of the engine's <c>ReplayScriptBuilder</c>), and writes it to
    /// <paramref name="output"/> as UTF-8 JSON. The events are the player's (as source or as target), then every buff and
    /// debuff on an enemy, every enemy's death and cast, the dungeon start and every player's combatantinfo, in time order;
    /// the player's events come first at one time. Each page of the player's events is written as it arrives: a long fight's events as one tree are over a GB,
    /// more than the browser app's worker has.
    /// </summary>
    public static async Task DownloadFightAsync(this FellowshipLogsClient client, string reportCode, int fightId, int playerId,
        Stream output, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var report = await ReportPartAsync(client, FightMetaQuery, new { code = reportCode, fightIDs = new[] { fightId } },
            r => r["fights"]?.AsArray().FirstOrDefault() is { } f && f["startTime"] is JsonValue && f["endTime"] is JsonValue ? r : null,
            $"fight {fightId} of report '{reportCode}'", ct);
        var fight = report["fights"]!.AsArray()[0]!;
        double start = fight["startTime"]!.GetValue<double>(), end = fight["endTime"]!.GetValue<double>();

        // The other streams' events without the player's, which the player's stream has: held until the player's events
        // reach their time.
        var held = new List<(double Timestamp, byte[] Value)>();
        foreach (var stream in FightStreams)
        {
            await ReadEventsAsync(client, reportCode, fightId, stream, start, end, (answer, events) =>
            {
                foreach (var e in events)
                {
                    if (e.SourceId == playerId || e.TargetId == playerId) continue;
                    held.Add((e.Timestamp ?? start, answer.AsSpan(e.Start, e.Length).ToArray()));
                }
                return Task.CompletedTask;
            }, ct);
        }
        held = held.OrderBy(h => h.Timestamp).ToList();

        await using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();
        writer.WriteString("report_code", reportCode);
        Write(writer, "report_start_time", report["startTime"]);
        Write(writer, "fight", fight);
        Write(writer, "master_data", report["masterData"]);
        writer.WriteNull("character");
        writer.WriteStartArray("events");
        int written = 0, next = 0;
        await ReadEventsAsync(client, reportCode, fightId, new EventStream(playerId, "All", "Friendlies", ""), start, end,
            async (answer, events) =>
            {
                foreach (var e in events)
                {
                    while (e.Timestamp is { } t && next < held.Count && held[next].Timestamp < t)
                    {
                        EventsPage.WriteEvent(held[next++].Value, writer);
                        written++;
                    }
                    EventsPage.WriteEvent(answer.AsSpan(e.Start, e.Length), writer);
                    written++;
                }
                await writer.FlushAsync(ct);
                progress?.Report($"Downloaded {written:N0} events");
            }, ct);
        for (; next < held.Count; next++, written++) EventsPage.WriteEvent(held[next].Value, writer);
        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(ct);
    }

    // Each page of a stream's events from the fight's start to its end, in turn.
    private static async Task ReadEventsAsync(FellowshipLogsClient client, string reportCode, int fightId, EventStream stream,
        double start, double end, Func<byte[], List<PageEvent>, Task> page, CancellationToken ct)
    {
        double? cursor = start;
        while (cursor is { } from && from < end)
        {
            byte[] answer = await client.QueryUtf8Async(EventsQuery, new
            {
                code = reportCode,
                fightIDs = new[] { fightId },
                startTime = from,
                endTime = end,
                sourceID = stream.SourceId,
                dataType = stream.DataType,
                hostilityType = stream.HostilityType,
                filterExpression = stream.FilterExpression,
            }, ct);
            var read = EventsPage.Read(answer)
                ?? throw new FellowshipLogsException($"FellowshipLogs returned no events for fight {fightId} of report '{reportCode}' from {(from - start) / 1000:F0}s.");
            await page(answer, read.Events(answer));
            if (read.NextPageTimestamp is not { } next || next <= from) break;
            cursor = next;
        }
    }

    // A property with a node's value, or JSON null for none.
    private static void Write(Utf8JsonWriter writer, string name, JsonNode? value)
    {
        writer.WritePropertyName(name);
        if (value == null) writer.WriteNullValue();
        else value.WriteTo(writer);
    }

    /// <summary>
    /// The part of a report query's <c>report</c> that <paramref name="part"/> picks. An answer with no report, or a report
    /// without the part, throws <see cref="FellowshipLogsException"/>: "FellowshipLogs returned no <paramref name="what"/>."
    /// </summary>
    private static async Task<JsonNode> ReportPartAsync(FellowshipLogsClient client, string query, object variables,
        Func<JsonNode, JsonNode?> part, string what, CancellationToken ct)
    {
        var report = (await client.QueryAsync(query, variables, ct))["reportData"]?["report"];
        return report != null && part(report) is { } found ? found : throw new FellowshipLogsException($"FellowshipLogs returned no {what}.");
    }
}
