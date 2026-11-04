using System.Text.Json;
using System.Text.Json.Nodes;

namespace FellowshipSim.Logs;

/// <summary>A report's fights and who played in each, and its combat-log format version (<see cref="GamePatch.LogVersion"/>).</summary>
public sealed record ReportSummary(string Code, string Title, long StartTime, IReadOnlyList<FightSummary> Fights, int? LogVersion = null);

public sealed record FightSummary(int Id, string Name, bool Kill, double DurationSeconds, IReadOnlyList<ReportPlayer> Players);

/// <summary>A player in a report: <see cref="Id"/> is the report's actor ID (the "source" in a FellowshipLogs link).</summary>
public sealed record ReportPlayer(int Id, string Name, string Hero);

/// <summary>Report queries: the fight list of a report, and every event of one fight.</summary>
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

    private const string EventsQuery = """
        query($code: String!, $fightIDs: [Int]!, $startTime: Float!, $endTime: Float!) {
          reportData {
            report(code: $code) {
              events(fightIDs: $fightIDs, dataType: All, startTime: $startTime, endTime: $endTime, limit: 10000, includeResources: true) {
                data nextPageTimestamp
              }
            }
          }
        }
        """;

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
                id name startTime endTime difficulty gameZone { name } enemyNPCs { id }
                dungeonPulls { id name startTime endTime enemyNPCs { id gameID minimumInstanceID maximumInstanceID } }
              }
              table(fightIDs: $fightIDs, dataType: Deaths, hostilityType: Enemies)
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
    /// One dungeon fight without its events: <c>report_code</c>; <c>report_start_time</c> and <c>log_version</c>, the
    /// report's start and combat-log format version (null when FellowshipLogs returns none); <c>fight</c> (id, name, startTime, endTime, difficulty,
    /// gameZone, enemyNPCs and dungeonPulls, each pull's enemyNPCs with their instance ranges); and <c>enemy_deaths</c>, each enemy death's
    /// actor <c>id</c>, <c>instance</c>, <c>gameID</c>, <c>name</c> and <c>timestamp</c>.
    /// </summary>
    public static async Task<JsonObject> GetDungeonFightAsync(this FellowshipLogsClient client, string reportCode, int fightId,
        CancellationToken ct = default)
    {
        var report = await ReportPartAsync(client, DungeonFightQuery, new { code = reportCode, fightIDs = new[] { fightId } },
            r => r["fights"]?.AsArray().FirstOrDefault() is { } f && f["dungeonPulls"] is JsonArray ? r : null,
            $"pulls for fight {fightId} of report '{reportCode}'", ct);
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
        return new JsonObject
        {
            ["report_code"] = reportCode,
            ["report_start_time"] = report["startTime"]?.DeepClone(),
            ["log_version"] = report["masterData"]?["logVersion"]?.DeepClone(),
            ["fight"] = report["fights"]!.AsArray()[0]!.DeepClone(),
            ["enemy_deaths"] = deaths,
        };
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
    /// Downloads every event of one fight, with the report's actors and abilities, as a raw fight (the input of the
    /// engine's <c>ReplayScriptBuilder</c>), and writes it to <paramref name="output"/> as UTF-8 JSON. Each page of events
    /// is written as it arrives: a long fight's events as one tree are over a GB, more than the browser app's worker has.
    /// </summary>
    public static async Task DownloadFightAsync(this FellowshipLogsClient client, string reportCode, int fightId, Stream output,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var report = await ReportPartAsync(client, FightMetaQuery, new { code = reportCode, fightIDs = new[] { fightId } },
            r => r["fights"]?.AsArray().FirstOrDefault() is { } f && f["startTime"] is JsonValue && f["endTime"] is JsonValue ? r : null,
            $"fight {fightId} of report '{reportCode}'", ct);
        var fight = report["fights"]!.AsArray()[0]!;
        double start = fight["startTime"]!.GetValue<double>(), end = fight["endTime"]!.GetValue<double>();

        await using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();
        writer.WriteString("report_code", reportCode);
        Write(writer, "report_start_time", report["startTime"]);
        Write(writer, "fight", fight);
        Write(writer, "master_data", report["masterData"]);
        writer.WriteNull("character");
        writer.WriteStartArray("events");
        int events = 0;
        double? cursor = start;
        while (cursor is { } from && from < end)
        {
            var page = await ReportPartAsync(client, EventsQuery, new { code = reportCode, fightIDs = new[] { fightId }, startTime = from, endTime = end },
                r => r["events"] is { } p && p["data"] is JsonArray ? p : null,
                $"events for fight {fightId} of report '{reportCode}' from {(from - start) / 1000:F0}s", ct);
            foreach (var e in page["data"]!.AsArray())
            {
                if (e == null) writer.WriteNullValue();
                else e.WriteTo(writer);
            }
            events += page["data"]!.AsArray().Count;
            await writer.FlushAsync(ct);
            progress?.Report($"Downloaded {events:N0} events");
            double? next = page["nextPageTimestamp"]?.GetValue<double?>();
            if (next == null || next <= from) break;
            cursor = next;
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(ct);
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
