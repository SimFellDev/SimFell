// Checks an action list against the hero's vocabulary, sims it with the logged character on the logged run's dungeon,
// and prints the sim against the log: hero DPS, then each damage ID's DPS, hits, average hit and crit, and each
// ability's casts a minute. Reads the out dir of prepare.cs and log-tables.cs.
//
//     dotnet run .claude/skills/apl-from-logs/compare.cs -- <out dir> <action list file> [<iterations>=200] [<threads>=8]
//
// Writes <out dir>/runs/<list name>.simc (the whole profile, for the CLI or the app), .json (the SimResult) and .txt (the
// table). The fights have no length variance.
#:project ../../../src/FellowshipSim.Engine/FellowshipSim.Engine.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property ServerGarbageCollection=true
#:property ConcurrentGarbageCollection=false
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipSim.Engine;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length is < 2 or > 4)
{
    Console.Error.WriteLine("Usage: dotnet run .claude/skills/apl-from-logs/compare.cs -- <out dir> <action list file> [<iterations>=200] [<threads>=8]");
    return 1;
}
string outDir = Path.GetFullPath(args[0]), listPath = Path.GetFullPath(args[1]);
int iterations = args.Length > 2 ? int.Parse(args[2]) : 200, threads = args.Length > 3 ? int.Parse(args[3]) : 8;
var meta = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "fight.json")))!;
var log = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "log.json")))!;
string hero = (string)meta["hero"]!;
string list = File.ReadAllText(listPath);
var engine = new LocalSimEngine();

var check = await engine.CheckActionListAsync(hero, list);
var problems = (check?.Tokens ?? []).Where(t => t.Problem != null).Select(t => $"'{list.Substring(t.Start, t.Length)}' at {t.Start}: {t.Problem}").ToList();
if (check?.Error != null || problems.Count > 0)
{
    Console.WriteLine($"The action list does not check: {check?.Error}");
    foreach (var p in problems) Console.WriteLine($"  {p}");
    return 1;
}

string text = string.Join('\n',
    File.ReadAllText(Path.Combine(outDir, "character.simc")).TrimEnd(),
    $"{SimProfile.Iterations}={iterations}",
    $"{SimProfile.Threads}={threads}",
    $"{SimProfile.Seed}=1",
    $"{SimProfile.LengthVariance}=0",
    File.ReadAllText(Path.Combine(outDir, "dungeon.simc")).TrimEnd(),
    list);
string runs = Path.Combine(outDir, "runs");
Directory.CreateDirectory(runs);
string label = Path.GetFileNameWithoutExtension(listPath);
File.WriteAllText(Path.Combine(runs, $"{label}.simc"), text);
var profile = SimProfile.Parse(text);
var result = await engine.SimulateAsync(profile.Profile, profile.Settings);
File.WriteAllText(Path.Combine(runs, $"{label}.json"), JsonSerializer.Serialize(result));

double logSeconds = (double)log["fightSeconds"]!, simSeconds = result.DurationSeconds;
var sb = new StringBuilder();
sb.AppendLine($"{label}: {iterations} iterations, {simSeconds:F0} s fights against the log's {logSeconds:F0} s.");
// A sim with more enemies alive than the run has more hits of every area ability, whatever the action list.
double simAlive = (profile.Settings.Dungeon?.Pulls.Sum(p => p.EnemySeconds) ?? 0) / simSeconds;
sb.AppendLine($"Mean enemies alive: sim {simAlive:F1}, log {(double)log["meanAlive"]!:F1}.");
sb.AppendLine($"Hero DPS: sim {result.MeanDps:N0} ± {result.Margin:N0}, log {(double)log["heroDamage"]! / logSeconds:N0} ({result.MeanDps / ((double)log["heroDamage"]! / logSeconds) - 1:+0.0%;-0.0%}).");
sb.AppendLine();

// Damage by damage ID: the sim's sources of one ID (a Stealth cast, a clone's hits) are one row, as in the log.
var logDamage = log["damage"]!.AsArray().ToDictionary(d => (int)d!["id"]!, d => d!);
var simDamage = (result.Abilities ?? []).GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.ToList());
sb.AppendLine($"{"Damage",-34}{"ID",9}{"sim DPS",10}{"log DPS",10}{"sim hits/m",12}{"log hits/m",12}{"sim avg",10}{"log avg",10}{"sim crit",10}{"log crit",10}");
foreach (int id in simDamage.Keys.Union(logDamage.Keys)
             .OrderByDescending(id => (simDamage.TryGetValue(id, out var s) ? s.Sum(a => a.Dps) : 0) + (logDamage.TryGetValue(id, out var l) ? (double)l["damage"]! / logSeconds : 0)))
{
    var s = simDamage.GetValueOrDefault(id) ?? [];
    var l = logDamage.GetValueOrDefault(id);
    double simHits = s.Sum(a => a.Hits), logHits = l == null ? 0 : (double)l["hits"]!;
    string name = l != null ? (string)l["name"]! : string.Join(" + ", s.Select(a => a.Name).Distinct());
    sb.AppendLine($"{name,-34}{id,9}{s.Sum(a => a.Dps),10:N0}{(l == null ? 0 : (double)l["damage"]! / logSeconds),10:N0}" +
                  $"{simHits / simSeconds * 60,12:F1}{logHits / logSeconds * 60,12:F1}" +
                  $"{(simHits > 0 ? s.Sum(a => a.Damage) / simHits : 0),10:N0}{(logHits > 0 ? (double)l!["damage"]! / logHits : 0),10:N0}" +
                  $"{(simHits > 0 ? s.Sum(a => a.CritChance * a.Hits) / simHits : 0),10:P0}{(logHits > 0 ? (double)l!["crits"]! / logHits : 0),10:P0}");
}
// A name with damage under more than one ID (one in the sim, two in the log): its total, sim against log.
var byName = simDamage.Values.SelectMany(s => s).Select(a => (logDamage.TryGetValue(a.Id, out var l) ? (string)l["name"]! : a.Name, a.Id, Sim: a.Dps, Log: 0.0))
    .Concat(logDamage.Values.Select(l => ((string)l["name"]!, (int)l["id"]!, Sim: 0.0, Log: (double)l["damage"]! / logSeconds)))
    .GroupBy(r => r.Item1).Where(g => g.Select(r => r.Item2).Distinct().Count() > 1).ToList();
if (byName.Count > 0)
{
    sb.AppendLine("Damage names under more than one ID, in total:");
    foreach (var g in byName)
        sb.AppendLine($"{g.Key,-34}{string.Join("+", g.Select(r => r.Item2).Distinct()),18}{g.Sum(r => r.Sim),10:N0}{g.Sum(r => r.Log),10:N0}");
}
sb.AppendLine();

// Casts a minute: the sim's from its typical fight's cast list (every cast, with or without damage), the log's from the player's casts.
var logCasts = log["casts"]!.AsArray().ToDictionary(c => (int)c!["id"]!, c => c!);
var simCasts = (result.Casts ?? []).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.ToList());
sb.AppendLine($"Casts a minute (the sim's typical fight, {result.Casts?.Count ?? 0} casts):");
sb.AppendLine($"{"Ability",-34}{"ID",9}{"sim",9}{"log",9}");
foreach (int id in simCasts.Keys.Union(logCasts.Keys).OrderByDescending(id => logCasts.TryGetValue(id, out var c) ? (int)c["count"]! : simCasts[id].Count))
{
    string name = logCasts.TryGetValue(id, out var c) ? (string)c["name"]! : string.Join(" + ", simCasts[id].Select(x => x.Name).Distinct());
    sb.AppendLine($"{name,-34}{id,9}{(simCasts.TryGetValue(id, out var s) ? s.Count : 0) / simSeconds * 60,9:F2}{(c == null ? 0 : (int)c["count"]!) / logSeconds * 60,9:F2}");
}
File.WriteAllText(Path.Combine(runs, $"{label}.txt"), sb.ToString());
Console.Write(sb.ToString());
return 0;
