// Prepares one logged player's fight for building an action list: the raw fight (data/fights, downloaded once), the
// dungeon of the run (DungeonReader over FellowshipLogs' dungeon fight), and the player's character. Run once per fight;
// each download spends FellowshipLogs points.
//
//     dotnet run .claude/skills/apl-from-logs/prepare.cs -- <report link> [<player id or name>] <out dir>
//
// The link names the fight (?fight=N) and may name the player (&source=N). Writes to <out dir>:
//   fight.json        the report, fight, player, hero and the raw fight's path
//   dungeon-fight.json FellowshipLogs' dungeon fight: enemy deaths and the first tank's contacts with each enemy
//   dungeon.simc      the dungeon's lines
//   character.simc    the character's lines, with no action list
// Credentials are FELLOWSHIPLOGS_CLIENT_ID and FELLOWSHIPLOGS_CLIENT_SECRET, from the environment or the simfell user secrets.
#:project ../../../src/FellowshipSim.Engine/FellowshipSim.Engine.csproj
#:package Microsoft.Extensions.Configuration.EnvironmentVariables@10.0.13
#:package Microsoft.Extensions.Configuration.UserSecrets@10.0.13
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipSim.Core;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Microsoft.Extensions.Configuration;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: dotnet run .claude/skills/apl-from-logs/prepare.cs -- <report link> [<player id or name>] <out dir>");
    return 1;
}
var link = ReportLink.Parse(args[0]);
string? playerArg = args.Length == 3 ? args[1] : link.PlayerId?.ToString();
string outDir = Path.GetFullPath(args[^1]);
if (link.FightId is not { } fightId)
{
    Console.Error.WriteLine("The link names no fight: open the fight on FellowshipLogs and copy its link.");
    return 1;
}
Directory.CreateDirectory(outDir);

var configuration = new ConfigurationBuilder().AddUserSecrets("simfell").AddEnvironmentVariables().Build();
string? id = configuration["FELLOWSHIPLOGS_CLIENT_ID"], secret = configuration["FELLOWSHIPLOGS_CLIENT_SECRET"];
if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
{
    Console.Error.WriteLine("No FellowshipLogs credentials: set FELLOWSHIPLOGS_CLIENT_ID and FELLOWSHIPLOGS_CLIENT_SECRET " +
                            "in the environment or with dotnet user-secrets set <key> <value> --id simfell.");
    return 1;
}
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
var client = new FellowshipLogsClient(http, id, secret);

var summary = await client.GetSummaryAsync(link.ReportCode);
var fight = summary.Fights.FirstOrDefault(f => f.Id == fightId);
if (fight == null)
{
    Console.Error.WriteLine($"Report {link.ReportCode} has no fight {fightId}.");
    return 1;
}
var player = playerArg == null ? null
    : int.TryParse(playerArg, out int playerId) ? fight.Players.FirstOrDefault(p => p.Id == playerId)
    : fight.Players.FirstOrDefault(p => p.Name.Trim().Equals(playerArg.Trim(), StringComparison.OrdinalIgnoreCase));
if (player == null)
{
    Console.Error.WriteLine($"Name the player: {string.Join(", ", fight.Players.Select(p => $"{p.Id} {p.Name} ({p.Hero})"))}.");
    return 1;
}
string? hero = Catalog.Heroes.Keys.FirstOrDefault(h => h.Equals(player.Hero, StringComparison.OrdinalIgnoreCase));
if (hero == null)
{
    Console.Error.WriteLine($"{player.Name} plays {player.Hero}, and the engine has no such hero (it has {string.Join(", ", Catalog.Heroes.Keys)}).");
    return 1;
}
bool currentPatch = GamePatch.Current.Covers(DateTimeOffset.FromUnixTimeMilliseconds(summary.StartTime).UtcDateTime, summary.LogVersion);

string root = AppContext.GetData("EntryPointFileDirectoryPath") is string scriptDir ? scriptDir : Environment.CurrentDirectory;
while (!Directory.Exists(Path.Combine(root, "src", "FellowshipSim.Core"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("No SimFell checkout above the script.");
var library = new ReplayLibrary(Path.Combine(root, "data"));
Directory.CreateDirectory(library.FightsDirectory);
string rawFight = await library.GetRawFightFileAsync(client, link.ReportCode, fightId, player.Id, new Progress<string>(Console.Error.WriteLine));

var dungeonFight = await client.GetDungeonFightAsync(link.ReportCode, fightId);
File.WriteAllText(Path.Combine(outDir, "dungeon-fight.json"), dungeonFight.ToJsonString());
using (var dungeonDocument = JsonSerializer.SerializeToDocument(dungeonFight))
    File.WriteAllText(Path.Combine(outDir, "dungeon.simc"), DungeonReader.FromFight(dungeonDocument.RootElement).Format());

JsonObject? combatant = null;
using (var raw = JsonDocument.Parse(File.ReadAllBytes(rawFight)))
{
    foreach (var e in raw.RootElement.GetProperty("events").EnumerateArray())
    {
        if (e.GetProperty("type").GetString() != "combatantinfo" || !e.TryGetProperty("sourceID", out var s) || s.GetInt32() != player.Id) continue;
        combatant = JsonNode.Parse(e.GetRawText())!.AsObject();
        break;
    }
}
if (combatant == null)
{
    Console.Error.WriteLine($"The raw fight has no combatantinfo of player {player.Id}.");
    return 1;
}
var build = CharacterReader.FromCombatantInfo(combatant, player.Name, hero);
File.WriteAllText(Path.Combine(outDir, "character.simc"), new SimcProfile(build, "").Format());

File.WriteAllText(Path.Combine(outDir, "fight.json"), new JsonObject
{
    ["report"] = link.ReportCode,
    ["fight"] = fightId,
    ["fightName"] = fight.Name,
    ["durationSeconds"] = fight.DurationSeconds,
    ["player"] = player.Id,
    ["playerName"] = player.Name,
    ["hero"] = hero,
    ["currentPatch"] = currentPatch,
    ["rawFight"] = rawFight,
}.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"{fight.Name}, {fight.DurationSeconds:F0} s: {player.Name} ({player.Id}), {hero}.{(currentPatch ? "" : $" The report is from before patch {GamePatch.Current.Name}.")}");
Console.WriteLine($"Raw fight: {rawFight}");
Console.WriteLine($"Prepared: {outDir}");
return 0;
