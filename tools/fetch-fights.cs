// Downloads FellowshipLogs fights of one hero, from the patch the engine models (GamePatch.Current), into data/fights.
//
// The fights come from the hero's ranking lists in the latest zone, from rank 1 down, one encounter after another, so the
// set spans the zone's dungeons. A run is taken when it started on or after the patch's release day, its report has the
// patch's log format, and its fight has a player of the hero. A fight already in data/fights counts and is not
// downloaded again.
//
//     dotnet run tools/fetch-fights.cs -- --hero Rime [--count 12] [--max-per-player 2] [--pages 10]
//
// Credentials are those of the app: FELLOWSHIPLOGS_CLIENT_ID and FELLOWSHIPLOGS_CLIENT_SECRET, otherwise data/settings.json
// of this checkout, otherwise data/settings.json of the main checkout (for a worktree).
#:project ../src/FellowshipSim.Logs/FellowshipSim.Logs.csproj
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipSim.Logs;

string root = Path.GetDirectoryName((string)AppContext.GetData("EntryPointFileDirectoryPath")!)!;
string? hero = null;
int count = 12, maxPerPlayer = 2, maxPages = 10;
for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
    switch (args[i])
    {
        case "--hero": hero = Next(); break;
        case "--count": count = int.Parse(Next()); break;
        case "--max-per-player": maxPerPlayer = int.Parse(Next()); break;
        case "--pages": maxPages = int.Parse(Next()); break;
        default: Console.Error.WriteLine($"Unknown option {args[i]}."); return 1;
    }
}
if (hero == null)
{
    Console.Error.WriteLine("Usage: dotnet run tools/fetch-fights.cs -- --hero <Hero> [--count 12] [--max-per-player 2] [--pages 10]");
    return 1;
}

var credentials = Credentials(root);
if (credentials == null)
{
    Console.Error.WriteLine("No FellowshipLogs credentials: set FELLOWSHIPLOGS_CLIENT_ID and FELLOWSHIPLOGS_CLIENT_SECRET, " +
                            "or save them in data/settings.json through the desktop app.");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
var client = new FellowshipLogsClient(http, credentials.Value.Id, credentials.Value.Secret);
var library = new ReplayLibrary(Path.Combine(root, "data"));
Directory.CreateDirectory(library.FightsDirectory);
var patch = GamePatch.Current;
long releasedMs = new DateTimeOffset(patch.Released.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

var world = await client.QueryAsync("""
    { worldData { zones { id name encounters { id name } } }
      gameData { classes { slug specs { name slug } } } }
    """);
var zone = world["worldData"]!["zones"]!.AsArray().MaxBy(z => (int)z!["id"]!)!;
var encounters = zone["encounters"]!.AsArray().Select(e => ((int)e!["id"]!, (string)e["name"]!)).ToList();
var spec = world["gameData"]!["classes"]!.AsArray()
    .SelectMany(c => c!["specs"]!.AsArray().Select(s => (Class: (string)c["slug"]!, Name: (string)s!["name"]!, Slug: (string)s["slug"]!)))
    .FirstOrDefault(s => s.Name.Equals(hero, StringComparison.OrdinalIgnoreCase));
if (spec.Name == null)
{
    Console.Error.WriteLine($"FellowshipLogs has no hero named {hero}.");
    return 1;
}
Console.WriteLine($"Zone {(int)zone["id"]!} \"{(string)zone["name"]!}\", {encounters.Count} encounters; {spec.Name} runs of patch {patch.Name} " +
                  $"(from {patch.Released:yyyy-MM-dd}, log format {patch.LogVersion}).");

var taken = new List<(string Encounter, string Player, string Code, int FightId)>();
var skipped = new Dictionary<string, int>();
var queues = encounters.Select(e => new RankingQueue(e.Item1, e.Item2)).ToList();
var reports = new Dictionary<string, ReportSummary>();
while (taken.Count < count && queues.Any(q => !q.Exhausted))
{
    foreach (var queue in queues.Where(q => !q.Exhausted))
    {
        if (taken.Count >= count) break;
        while (await queue.NextAsync(client, spec.Class, spec.Slug, maxPages) is { } run)
        {
            string? reason = await Reject(run);
            if (reason == null) break;
            skipped[reason] = skipped.GetValueOrDefault(reason) + 1;
        }
    }
}

Console.WriteLine($"{taken.Count} of {count} fights wanted.");
foreach (var group in taken.GroupBy(t => t.Encounter)) Console.WriteLine($"  {group.Key}: {group.Count()}");
foreach (var (reason, n) in skipped.OrderByDescending(s => s.Value)) Console.WriteLine($"  skipped, {reason}: {n}");
if (taken.Count < count)
{
    Console.WriteLine($"The ranking lists hold {taken.Count} usable runs of patch {patch.Name} within {maxPages} pages per encounter. " +
                      "A larger --pages reads further down the lists.");
}
return 0;

async Task<string?> Reject(JsonNode run)
{
    string? code = (string?)run["report"]?["code"];
    int? fightId = (int?)run["report"]?["fightID"];
    string player = ((string?)run["name"] ?? "").Trim();
    if (code == null || fightId == null) return "no report";
    if ((long?)run["startTime"] is not { } startMs || startMs < releasedMs) return "before the patch";
    if (taken.Any(t => t.Code == code && t.FightId == fightId)) return "fight already taken";
    if (taken.Count(t => t.Player.Equals(player, StringComparison.OrdinalIgnoreCase)) >= maxPerPlayer) return "player cap";

    if (!reports.TryGetValue(code, out var report))
    {
        try { report = reports[code] = await client.GetSummaryAsync(code); }
        catch (FellowshipLogsException e) when (!Throttled(e)) { return "report unavailable"; }
    }
    if (!patch.Covers(DateTimeOffset.FromUnixTimeMilliseconds(report.StartTime).UtcDateTime, report.LogVersion)) return "older log format";
    var fight = report.Fights.FirstOrDefault(f => f.Id == fightId);
    if (fight == null || !fight.Players.Any(p => p.Hero.Equals(spec.Name, StringComparison.OrdinalIgnoreCase))) return "hero not in the fight";

    await WaitForPointsAsync();
    try { await library.GetRawFightFileAsync(client, code, fightId.Value); }
    catch (FellowshipLogsException e) when (!Throttled(e)) { return "download failed"; }
    string encounter = queues.First(q => q.Holds(run)).Name;
    taken.Add((encounter, player, code, fightId.Value));
    Console.WriteLine($"  {encounter}: {player} {code} fight {fightId} ({fight.Name}, {fight.DurationSeconds / 60:F0} min)");
    return null;
}

// A fight's events cost FellowshipLogs points. Below this many left in the hour, the download waits for the reset.
async Task WaitForPointsAsync()
{
    const int Reserve = 600;
    var rate = (await client.QueryAsync("{ rateLimitData { limitPerHour pointsSpentThisHour pointsResetIn } }"))["rateLimitData"]!;
    double left = (double)rate["limitPerHour"]! - (double)rate["pointsSpentThisHour"]!;
    if (left >= Reserve) return;
    int wait = (int)rate["pointsResetIn"]! + 5;
    Console.WriteLine($"  {left:F0} FellowshipLogs points left this hour; waiting {wait}s for the reset.");
    await Task.Delay(TimeSpan.FromSeconds(wait));
}

// A refusal for the rate limit or a server error stops the run; it says nothing about the report.
static bool Throttled(FellowshipLogsException e) =>
    e.Message.Contains("returned 429") || System.Text.RegularExpressions.Regex.IsMatch(e.Message, @"returned 5\d\d");

static (string Id, string Secret)? Credentials(string root)
{
    string? id = Environment.GetEnvironmentVariable("FELLOWSHIPLOGS_CLIENT_ID");
    string? secret = Environment.GetEnvironmentVariable("FELLOWSHIPLOGS_CLIENT_SECRET");
    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret)) return (id, secret);
    foreach (string folder in new[] { root, MainCheckout(root) }.OfType<string>().Distinct())
    {
        string path = Path.Combine(folder, "data", "settings.json");
        if (!File.Exists(path)) continue;
        var saved = JsonNode.Parse(File.ReadAllText(path));
        if ((string?)saved?["ClientId"] is { Length: > 0 } savedId && (string?)saved["ClientSecret"] is { Length: > 0 } savedSecret)
        {
            return (savedId, savedSecret);
        }
    }
    return null;
}

static string? MainCheckout(string root)
{
    var git = Process.Start(new ProcessStartInfo("git", "rev-parse --path-format=absolute --git-common-dir")
    {
        WorkingDirectory = root,
        RedirectStandardOutput = true,
    })!;
    string commonDir = git.StandardOutput.ReadToEnd().Trim();
    git.WaitForExit();
    return git.ExitCode == 0 ? Path.GetDirectoryName(commonDir) : null;
}

/// <summary>One encounter's ranking list of the hero, read a page at a time from rank 1.</summary>
sealed class RankingQueue(int encounterId, string name)
{
    private readonly Queue<JsonNode> _runs = new();
    private readonly HashSet<JsonNode> _read = [];
    private int _page;
    private bool _lastPage;

    public string Name { get; } = name;
    public bool Exhausted => _lastPage && _runs.Count == 0;

    public bool Holds(JsonNode run) => _read.Contains(run);

    public async Task<JsonNode?> NextAsync(FellowshipLogsClient client, string classSlug, string specSlug, int maxPages)
    {
        if (_runs.Count == 0 && !_lastPage)
        {
            _page++;
            var data = await client.QueryAsync(
                $"{{ worldData {{ encounter(id: {encounterId}) {{ characterRankings(className: \"{classSlug}\", specName: \"{specSlug}\", page: {_page}) }} }} }}");
            var page = data["worldData"]!["encounter"]!["characterRankings"]!;
            foreach (var run in page["rankings"]?.AsArray() ?? [])
            {
                if (run == null) continue;
                _runs.Enqueue(run);
                _read.Add(run);
            }
            _lastPage = (bool?)page["hasMorePages"] != true || _page >= maxPages;
        }
        return _runs.TryDequeue(out var next) ? next : null;
    }
}
