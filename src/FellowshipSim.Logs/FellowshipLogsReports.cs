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
              fights(fightIDs: $fightIDs) { id startTime endTime name friendlyPlayers enemyNPCs { id gameID instanceCount } }
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
        var fight = (await client.QueryAsync(FightTimesQuery, new { code = reportCode, fightIDs = new[] { fightId } }, ct))
            ["reportData"]?["report"]?["fights"]?.AsArray().FirstOrDefault()
            ?? throw new FellowshipLogsException($"Fight {fightId} is not in report '{reportCode}'.");
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
    /// Downloads every event of one fight, with the report's actors and abilities, as a raw fight
    /// (the input of the engine's <c>ReplayScriptBuilder</c>).
    /// </summary>
    public static async Task<JsonObject> DownloadFightAsync(this FellowshipLogsClient client, string reportCode, int fightId,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var meta = await client.QueryAsync(FightMetaQuery, new { code = reportCode, fightIDs = new[] { fightId } }, ct);
        var report = meta["reportData"]?["report"] ?? throw new FellowshipLogsException($"Report '{reportCode}' was not found.");
        var fight = report["fights"]?.AsArray().FirstOrDefault() ?? throw new FellowshipLogsException($"Fight {fightId} is not in report '{reportCode}'.");
        double start = fight["startTime"]!.GetValue<double>(), end = fight["endTime"]!.GetValue<double>();

        var events = new JsonArray();
        double? cursor = start;
        while (cursor is { } from && from < end)
        {
            var page = (await client.QueryAsync(EventsQuery, new { code = reportCode, fightIDs = new[] { fightId }, startTime = from, endTime = end }, ct))
                ["reportData"]!["report"]!["events"]!;
            foreach (var e in page["data"]?.AsArray() ?? []) events.Add(e?.DeepClone());
            progress?.Report($"Downloaded {events.Count:N0} events");
            double? next = page["nextPageTimestamp"]?.GetValue<double?>();
            if (next == null || next <= from) break;
            cursor = next;
        }

        return new JsonObject
        {
            ["report_code"] = reportCode,
            ["report_start_time"] = report["startTime"]?.DeepClone(),
            ["fight"] = fight.DeepClone(),
            ["master_data"] = report["masterData"]?.DeepClone(),
            ["character"] = null,
            ["events"] = events,
        };
    }
}
