// The logged player's play in one prepared fight, per ability: casts a minute, the hero's resources at each cast, enemies
// alive and enemies hit, boss or trash, target health, the player's buffs at the cast against their uptime, the player's
// debuffs on the target, and the cast that follows. Reads the out dir of prepare.cs.
//
//     dotnet run .claude/skills/apl-from-logs/log-tables.cs -- <out dir>
//
// Writes to <out dir>:
//   log-tables.txt  the tables
//   log-casts.txt   every cast in order, with its resources, enemies alive and hit, target health and target
//   log.json        each damage ID's damage, hits and crits, and each ability's casts, for compare.cs
#:project ../../../src/FellowshipSim.Engine/FellowshipSim.Engine.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Engine;
using FellowshipSim.Engine.Replay;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run .claude/skills/apl-from-logs/log-tables.cs -- <out dir>");
    return 1;
}
string outDir = Path.GetFullPath(args[0]);
var meta = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "fight.json")))!;
int player = (int)meta["player"]!;
string hero = (string)meta["hero"]!;
var vocabulary = (await new LocalSimEngine().GetVocabularyAsync(hero))!;
var auraSlugs = vocabulary.Auras.Select(a => a.Slug).ToHashSet();
var resourceSlugs = vocabulary.Resources.Select(r => r.Slug).ToHashSet();
// The hero's resources in the log: each LogResources entry whose resource is in the hero's vocabulary.
var resources = LogResources.All.Where(r => resourceSlugs.Contains(r.Resource.AplName)).ToList();
// Abilities off the global cooldown (auto attacks): the next-cast table passes over them.
var heroModel = Catalog.CreateHero(hero);
heroModel.Initialize();
var offGcd = heroModel.Abilities.Where(a => a.IgnoresGcd).Select(a => a.Id).ToHashSet();

using var raw = JsonDocument.Parse(File.ReadAllBytes((string)meta["rawFight"]!));
var root = raw.RootElement;
var fight = root.GetProperty("fight");
double start = fight.GetProperty("startTime").GetDouble(), fightSeconds = (fight.GetProperty("endTime").GetDouble() - start) / 1000;
double Seconds(double timestamp) => (timestamp - start) / 1000;
var creatureOf = fight.GetProperty("enemyNPCs").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt32(), x => x.GetProperty("gameID").GetInt32());
bool IsBoss(int actor) => creatureOf.TryGetValue(actor, out var g) && GameCreatures.ById(g)?.Rank == CreatureRank.Boss;
string CreatureName(int actor) => creatureOf.TryGetValue(actor, out var g) ? GameCreatures.ById(g)?.Name ?? $"creature {g}" : $"actor {actor}";
var abilityName = new Dictionary<int, string>();
foreach (var a in root.GetProperty("master_data").GetProperty("abilities").EnumerateArray())
    abilityName.TryAdd(a.GetProperty("gameID").GetInt32(), a.GetProperty("name").GetString() ?? "");
string Name(int abilityId) => abilityName.GetValueOrDefault(abilityId, abilityId.ToString());
var petOwner = new Dictionary<int, int>();
foreach (var a in root.GetProperty("master_data").GetProperty("actors").EnumerateArray())
    if (a.TryGetProperty("petOwner", out var o) && o.ValueKind == JsonValueKind.Number) petOwner[a.GetProperty("id").GetInt32()] = o.GetInt32();
bool Mine(int source) => source == player || petOwner.GetValueOrDefault(source) == player;

// Each enemy instance is in combat from its first contact with the run's first tank or the player to its death, or to its
// last contact without one.
var dungeonFight = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "dungeon-fight.json")))!;
var spans = new Dictionary<(int, int), (double From, double To)>();
foreach (var c in dungeonFight["enemy_contacts"]!.AsArray())
    spans[((int)c!["id"]!, (int)c["instance"]!)] = (Seconds((double)c["first"]!), Seconds((double)c["last"]!));
var deaths = new Dictionary<(int, int), double>();
foreach (var d in dungeonFight["enemy_deaths"]!.AsArray())
    deaths.TryAdd(((int)d!["id"]!, (int?)d["instance"] ?? 1), Seconds((double)d["timestamp"]!));

var events = root.GetProperty("events").EnumerateArray().ToList();
int Int(JsonElement e, string name, int fallback) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;
foreach (var e in events)
{
    if (e.GetProperty("type").GetString() != "damage" || !Mine(Int(e, "sourceID", -1))) continue;
    var key = (Int(e, "targetID", -1), Int(e, "targetInstance", 1));
    if (!creatureOf.ContainsKey(key.Item1)) continue;
    double t = Seconds(e.GetProperty("timestamp").GetDouble());
    spans[key] = spans.TryGetValue(key, out var s) ? (Math.Min(s.From, t), Math.Max(s.To, t)) : (t, t);
}
var combat = spans.Select(kv => (Key: kv.Key, From: kv.Value.From, To: deaths.TryGetValue(kv.Key, out var d) ? Math.Max(d, kv.Value.From) : kv.Value.To, Boss: IsBoss(kv.Key.Item1))).ToList();
int Alive(double t) => combat.Count(c => c.From <= t && c.To > t);
bool BossUp(double t) => combat.Any(c => c.Boss && c.From <= t && c.To > t);

// One pass: the player's buffs and their uptime, enemy health, the player's hits and casts.
var buffs = new Dictionary<int, double>();
var uptime = new Dictionary<int, double>();
var health = new Dictionary<(int, int), double>();
// The player's debuffs on each enemy instance.
var debuffs = new HashSet<(int Target, int Instance, int Ability)>();
var casts = new List<Cast>();
var hits = new List<(double T, int Ability, int Target, int Instance, double Amount, bool Crit)>();
foreach (var e in events)
{
    string type = e.GetProperty("type").GetString()!;
    double t = Seconds(e.GetProperty("timestamp").GetDouble());
    int source = Int(e, "sourceID", -1), target = Int(e, "targetID", -1), ability = Int(e, "abilityGameID", 0);
    int targetInstance = Int(e, "targetInstance", 1);
    if (creatureOf.ContainsKey(target) && e.TryGetProperty("targetResources", out var tr) && tr.TryGetProperty("maxHitPoints", out var mh) && mh.GetDouble() > 0)
        health[(target, targetInstance)] = 100 * tr.GetProperty("hitPoints").GetDouble() / mh.GetDouble();
    if (target == player && type == "applybuff") buffs.TryAdd(ability, t);
    if (target == player && type == "removebuff" && buffs.Remove(ability, out var since)) uptime[ability] = uptime.GetValueOrDefault(ability) + t - since;
    if (Mine(source) && creatureOf.ContainsKey(target))
    {
        var debuff = (target, targetInstance, ability);
        if (type == "applydebuff") debuffs.Add(debuff);
        if (type == "removedebuff") debuffs.Remove(debuff);
    }
    if (type == "damage" && Mine(source) && creatureOf.ContainsKey(target))
        hits.Add((t, ability, target, targetInstance, e.GetProperty("amount").GetDouble(),
            Int(e, "hitType", 0) is ReplayScriptBuilder.HitTypeCrit or ReplayScriptBuilder.HitTypeGrievousCrit));
    // The log repeats some casts with "fake": true.
    if (type != "cast" || source != player || e.TryGetProperty("fake", out var fake) && fake.ValueKind == JsonValueKind.True) continue;
    var amounts = new Dictionary<string, double>();
    if (e.TryGetProperty("sourceResources", out var sr) && sr.TryGetProperty("resources", out var list))
        foreach (var r in list.EnumerateArray())
            foreach (var logged in resources.Where(x => x.LogResourceType == r.GetProperty("type").GetInt32()))
            {
                double amount = r.GetProperty("amount").GetDouble(), max = r.TryGetProperty("max", out var m) ? m.GetDouble() : 0;
                amounts[logged.Resource.AplName] = logged.ShareOfMaximum ? (max > 0 ? amount / max * logged.Resource.Max : 0) : amount * logged.Scale;
            }
    bool onEnemy = creatureOf.ContainsKey(target);
    casts.Add(new Cast(t, ability, Name(ability), amounts, target, targetInstance, onEnemy ? health.GetValueOrDefault((target, targetInstance), 100) : -1,
        onEnemy && IsBoss(target), Alive(t), BossUp(t), buffs.Keys.ToHashSet(),
        onEnemy ? debuffs.Where(d => d.Target == target && d.Instance == targetInstance).Select(d => d.Ability).ToHashSet() : []));
}
foreach (var (ability, since) in buffs) uptime[ability] = uptime.GetValueOrDefault(ability) + fightSeconds - since;

// Enemies hit: distinct enemies hit by the cast's ability ID from 0.05 s before the cast to 0.3 s after it.
var hitsByAbility = hits.GroupBy(h => h.Ability).ToDictionary(g => g.Key, g => g.OrderBy(h => h.T).ToList());
int EnemiesHit(Cast c) => hitsByAbility.TryGetValue(c.Ability, out var l)
    ? l.Where(h => h.T >= c.T - 0.05 && h.T <= c.T + 0.3).Select(h => (h.Target, h.Instance)).Distinct().Count() : 0;
var enemiesHit = casts.ToDictionary(c => c, EnemiesHit);

string Bucket(int n) => n <= 1 ? "1" : n == 2 ? "2" : n <= 4 ? "3-4" : n <= 7 ? "5-7" : "8+";
string[] buckets = ["1", "2", "3-4", "5-7", "8+"];
string Counts<T>(IEnumerable<T> values) where T : notnull => string.Join("  ", values.GroupBy(v => v).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}"));
string Slug(int buffId) => AplNames.Slug(Name(buffId));
double heroDamage = hits.Sum(h => h.Amount);

var sb = new StringBuilder();
sb.AppendLine($"{meta["fightName"]}, {fightSeconds:F0} s. {meta["playerName"]} ({player}), {hero}. Report {meta["report"]} fight {meta["fight"]}.");
sb.AppendLine($"Hero DPS {heroDamage / fightSeconds:N0}. {casts.Count} casts. Mean enemies alive {Enumerable.Range(0, (int)fightSeconds).Average(s => (double)Alive(s)):F1}; a boss is up {Enumerable.Range(0, (int)fightSeconds).Count(s => BossUp(s))} s.");
sb.AppendLine($"Resources at cast: {string.Join(", ", resources.Select(r => $"{r.Resource.AplName} (log type {r.LogResourceType})"))}.");
sb.AppendLine();
sb.AppendLine("== Casts");
sb.AppendLine($"{"Ability",-28}{"n",6}{"/min",7}" + string.Concat(resources.Select(r => $"{r.Resource.AplName,14}")) + $"{"alive",7}{"hit",6}{"tgtHP",7}{"bossUp",8}{"onBoss",8}");
foreach (var g in casts.GroupBy(c => c.Name).OrderByDescending(g => g.Count()))
{
    var l = g.ToList();
    var onEnemy = l.Where(c => c.TargetHealth >= 0).ToList();
    sb.Append($"{g.Key,-28}{l.Count,6}{l.Count / fightSeconds * 60,7:F2}");
    foreach (var r in resources) sb.Append($"{(l.Any(c => c.Resources.ContainsKey(r.Resource.AplName)) ? l.Where(c => c.Resources.ContainsKey(r.Resource.AplName)).Average(c => c.Resources[r.Resource.AplName]) : double.NaN),14:F1}");
    sb.AppendLine($"{l.Average(c => c.Alive),7:F1}{l.Average(c => enemiesHit[c]),6:F1}{(onEnemy.Count > 0 ? onEnemy.Average(c => c.TargetHealth) : double.NaN),7:F0}{l.Count(c => c.BossUp),8}{l.Count(c => c.OnBoss),8}");
}

sb.AppendLine();
sb.AppendLine("== Casts a minute by enemies alive, over the seconds with that many alive");
foreach (bool boss in new[] { true, false })
{
    var seconds = buckets.ToDictionary(b => b, b => (double)Enumerable.Range(0, (int)fightSeconds).Count(s => Alive(s) > 0 && BossUp(s) == boss && Bucket(Alive(s)) == b));
    sb.AppendLine(boss ? "Boss up:" : "Trash only:");
    sb.AppendLine($"  {"Ability",-28}" + string.Concat(buckets.Select(b => $"{b + " (" + seconds[b] + "s)",14}")));
    foreach (var g in casts.Where(c => c.Alive > 0 && c.BossUp == boss).GroupBy(c => c.Name).OrderByDescending(g => g.Count()))
        sb.AppendLine($"  {g.Key,-28}" + string.Concat(buckets.Select(b => $"{(seconds[b] > 0 ? g.Count(c => Bucket(c.Alive) == b) / seconds[b] * 60 : 0),14:F2}")));
}

sb.AppendLine();
sb.AppendLine($"== Each ability's casts: resources, enemies alive and hit, target health (20% steps), and the next cast on the global cooldown (passing over {string.Join(", ", offGcd.Select(Name))})");
foreach (var g in casts.GroupBy(c => c.Name).OrderByDescending(g => g.Count()))
{
    var l = g.ToList();
    sb.AppendLine($"{g.Key} ({l.Count}):");
    foreach (var r in resources)
        if (l.Any(c => c.Resources.ContainsKey(r.Resource.AplName)))
            sb.AppendLine($"  {r.Resource.AplName}: {Counts(l.Where(c => c.Resources.ContainsKey(r.Resource.AplName)).Select(c => (int)Math.Round(r.Resource.Max > 20 ? Math.Floor(c.Resources[r.Resource.AplName] / 10) * 10 : c.Resources[r.Resource.AplName])))}");
    sb.AppendLine($"  alive: {string.Join("  ", buckets.Select(b => $"{b}:{l.Count(c => Bucket(c.Alive) == b)}"))}");
    sb.AppendLine($"  hit: {string.Join("  ", buckets.Select(b => $"{b}:{l.Count(c => enemiesHit[c] > 0 && Bucket(enemiesHit[c]) == b)}"))}  none:{l.Count(c => enemiesHit[c] == 0)}");
    var onEnemy = l.Where(c => c.TargetHealth >= 0).ToList();
    if (onEnemy.Count > 0) sb.AppendLine($"  target health: {Counts(onEnemy.Select(c => (int)Math.Min(80, Math.Floor(c.TargetHealth / 20) * 20)))}; boss target {l.Count(c => c.OnBoss)}");
    var next = new List<string>();
    foreach (var c in l)
    {
        int i = casts.IndexOf(c) + 1;
        while (i < casts.Count && offGcd.Contains(casts[i].Ability)) i++;
        if (i < casts.Count) next.Add(casts[i].Name);
    }
    sb.AppendLine($"  next: {string.Join("  ", next.GroupBy(n => n).OrderByDescending(n => n.Count()).Take(8).Select(n => $"{n.Key}:{n.Count()}"))}");
}

sb.AppendLine();
sb.AppendLine("== The player's buffs at each ability's casts, against the buff's uptime (buffs up at 10% of the casts or more, the largest gap first)");
sb.AppendLine("An action-list name in [brackets] is not in the hero's vocabulary.");
foreach (var g in casts.GroupBy(c => c.Name).OrderByDescending(g => g.Count()))
{
    var l = g.ToList();
    // The rows with the largest gap between the share at cast and the uptime come first: the buffs the player waits for or spends.
    var rows = l.SelectMany(c => c.Buffs).GroupBy(b => b).Where(b => b.Count() >= 0.1 * l.Count)
        .OrderByDescending(b => Math.Abs((double)b.Count() / l.Count - uptime.GetValueOrDefault(b.Key) / fightSeconds)).ToList();
    if (rows.Count == 0) continue;
    sb.AppendLine($"{g.Key} ({l.Count}):");
    foreach (var b in rows)
    {
        string slug = Slug(b.Key);
        sb.AppendLine($"  {Name(b.Key),-32}{(auraSlugs.Contains(slug) ? slug : $"[{slug}]"),-36}{b.Key,9}  at cast {(double)b.Count() / l.Count,5:P0}  uptime {uptime.GetValueOrDefault(b.Key) / fightSeconds,5:P0}");
    }
}

sb.AppendLine();
sb.AppendLine("== The player's debuffs on the target at each ability's casts on an enemy (debuffs up at 10% of the casts or more)");
foreach (var g in casts.Where(c => c.TargetHealth >= 0).GroupBy(c => c.Name).OrderByDescending(g => g.Count()))
{
    var l = g.ToList();
    var rows = l.SelectMany(c => c.TargetDebuffs).GroupBy(d => d).Where(d => d.Count() >= 0.1 * l.Count).OrderByDescending(d => d.Count()).ToList();
    if (rows.Count == 0) continue;
    sb.AppendLine($"{g.Key} ({l.Count} on an enemy):");
    foreach (var d in rows)
    {
        string slug = Slug(d.Key);
        sb.AppendLine($"  {Name(d.Key),-32}{(auraSlugs.Contains(slug) ? slug : $"[{slug}]"),-36}{d.Key,9}  at cast {(double)d.Count() / l.Count,5:P0}");
    }
}

sb.AppendLine();
sb.AppendLine("== Damage by damage ID");
sb.AppendLine($"{"Damage",-34}{"ID",9}{"DPS",10}{"hits/min",10}{"avg hit",10}{"crit",7}");
foreach (var g in hits.GroupBy(h => h.Ability).OrderByDescending(g => g.Sum(h => h.Amount)))
    sb.AppendLine($"{Name(g.Key),-34}{g.Key,9}{g.Sum(h => h.Amount) / fightSeconds,10:N0}{g.Count() / fightSeconds * 60,10:F1}{g.Average(h => h.Amount),10:N0}{(double)g.Count(h => h.Crit) / g.Count(),7:P0}");
File.WriteAllText(Path.Combine(outDir, "log-tables.txt"), sb.ToString());

var sequence = new StringBuilder();
foreach (var c in casts)
    sequence.AppendLine($"{c.T,8:F2} {c.Name,-26}" + string.Concat(resources.Select(r => $" {r.Resource.AplName}={(c.Resources.TryGetValue(r.Resource.AplName, out var v) ? v.ToString("F0") : "-"),-4}")) +
                        $" alive={c.Alive,-3} hit={enemiesHit[c],-3} hp={(c.TargetHealth >= 0 ? c.TargetHealth.ToString("F0") : "-"),-4} {(c.BossUp ? "BOSS" : "    ")} {(c.TargetHealth >= 0 ? CreatureName(c.Target) : "")}");
File.WriteAllText(Path.Combine(outDir, "log-casts.txt"), sequence.ToString());

File.WriteAllText(Path.Combine(outDir, "log.json"), new JsonObject
{
    ["fightSeconds"] = fightSeconds,
    ["meanAlive"] = combat.Sum(c => Math.Max(0, Math.Min(c.To, fightSeconds) - Math.Max(c.From, 0))) / fightSeconds,
    ["heroDamage"] = heroDamage,
    ["casts"] = new JsonArray(casts.GroupBy(c => c.Ability).Select(g => (JsonNode)new JsonObject { ["id"] = g.Key, ["name"] = Name(g.Key), ["count"] = g.Count() }).ToArray()),
    ["damage"] = new JsonArray(hits.GroupBy(h => h.Ability).Select(g => (JsonNode)new JsonObject
    {
        ["id"] = g.Key, ["name"] = Name(g.Key), ["damage"] = g.Sum(h => h.Amount), ["hits"] = g.Count(), ["crits"] = g.Count(h => h.Crit),
    }).ToArray()),
}.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"Hero DPS {heroDamage / fightSeconds:N0}, {casts.Count} casts, {fightSeconds:F0} s.");
Console.WriteLine($"Wrote {Path.Combine(outDir, "log-tables.txt")}, log-casts.txt and log.json.");
return 0;

record Cast(double T, int Ability, string Name, Dictionary<string, double> Resources, int Target, int TargetInstance, double TargetHealth,
    bool OnBoss, int Alive, bool BossUp, HashSet<int> Buffs, HashSet<int> TargetDebuffs);
