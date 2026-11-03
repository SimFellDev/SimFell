namespace FellowshipSim.Logs;

public static class FellowshipLogsCharacters
{
    /// <summary>How many of a character's recent reports <see cref="GetCharacterAsync"/> reads.</summary>
    public const int RecentReports = 5;

    private const string CharacterQuery = """
        query($id: Int!, $limit: Int!) {
          characterData {
            character(id: $id) {
              id name
              recentReports(limit: $limit) {
                data {
                  code startTime
                  fights { id name kill startTime endTime friendlyPlayers }
                  masterData { logVersion actors(type: "Player") { id name subType } }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// The character <paramref name="characterId"/> and every fight of its <see cref="RecentReports"/> latest reports that
    /// it is in, the latest first. A report's player is the character when it has the character's name and is in the
    /// fight. Throws <see cref="FellowshipLogsException"/> when FellowshipLogs has no such character.
    /// </summary>
    public static async Task<LoggedCharacter> GetCharacterAsync(this FellowshipLogsClient client, int characterId, CancellationToken ct = default)
    {
        var character = (await client.QueryAsync(CharacterQuery, new { id = characterId, limit = RecentReports }, ct))
            ["characterData"]?["character"]
            ?? throw new FellowshipLogsException($"FellowshipLogs has no character {characterId}.");
        string name = character["name"]?.GetValue<string>() ?? "";
        var fights = new List<CharacterFight>();
        foreach (var report in character["recentReports"]?["data"]?.AsArray() ?? [])
        {
            if (report == null) continue;
            var players = (report["masterData"]?["actors"]?.AsArray() ?? [])
                .Where(a => a?["name"]?.GetValue<string>() == name)
                .ToDictionary(a => a!["id"]!.GetValue<int>(), a => new ReportPlayer(a!["id"]!.GetValue<int>(), name, a["subType"]?.GetValue<string>() ?? ""));
            foreach (var f in report["fights"]?.AsArray() ?? [])
            {
                if (f == null) continue;
                // The same name logs as an "Unknown" actor too; the player in the fight is the character.
                var player = (f["friendlyPlayers"]?.AsArray() ?? []).Select(x => x!.GetValue<int>())
                    .Where(players.ContainsKey).Select(id => players[id]).FirstOrDefault();
                if (player == null) continue;
                double start = f["startTime"]!.GetValue<double>(), end = f["endTime"]!.GetValue<double>();
                fights.Add(new CharacterFight(
                    report["code"]!.GetValue<string>(), report["startTime"]?.GetValue<long>() ?? 0,
                    report["masterData"]?["logVersion"]?.GetValue<int?>(), f["id"]!.GetValue<int>(), f["name"]?.GetValue<string>() ?? "",
                    f["kill"]?.GetValue<bool?>() ?? false, (end - start) / 1000.0, end, player));
            }
        }
        // The latest fight first, by its end: the report's start plus the fight's end in the report.
        return new LoggedCharacter(character["id"]?.GetValue<int>() ?? characterId, name,
            fights.OrderByDescending(f => f.ReportStartTime + f.EndTime).ToList());
    }
}
