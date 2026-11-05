using System.Text.Json;
using System.Text.RegularExpressions;

namespace FellowshipSim.Logs;

/// <summary>A replay review that has been run: what was replayed and how closely the engine matched the log.</summary>
public sealed record ReviewSummary(
    string Id,
    string ReportCode,
    int FightId,
    string FightName,
    DateTime? FightDate,
    int PlayerId,
    string PlayerName,
    string Hero,
    int PassedSteps,
    int TotalSteps,
    int MatchedHits,
    int LogHits,
    double EngineDamage,
    double LogDamage,
    int ForcedCasts,
    DateTime CreatedAt)
{
    public string HtmlFile => $"{Id}.html";

    /// <summary>The latest free-dice run of the same fight, if any.</summary>
    public FreeDiceSummary? FreeDice { get; init; }
}

/// <summary>A free-dice run of a fight, as shown next to its review (saved as &lt;review&gt;.freedice.json).</summary>
public sealed record FreeDiceSummary(
    int Runs, double LogDamage, double EngineMean, double Engine5, double Engine95, double LogPercentile, DateTime CreatedAt);

/// <summary>
/// A replay review's files: the replay script, the review page, the review as plain text (its findings, and the
/// damage calibration per ability), and every engine hit with the log hit it matched, for checking damage formulas in a
/// spreadsheet, every issue of the review, and its findings as JSON.
/// </summary>
public sealed record ReviewArtifacts(ReviewSummary Summary, byte[] ScriptJson, string Html, string SummaryText, string HitsCsv, string IssuesCsv, string FindingsJson);

/// <summary>
/// The local library of fights and replay reviews, kept in a data folder:
/// <c>fights/</c> raw fights downloaded from FellowshipLogs (each player's own), <c>scripts/</c> replay scripts built from them, and
/// <c>reviews/</c> the review pages with a summary of each.
/// </summary>
public sealed partial class ReplayLibrary
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public ReplayLibrary(string dataDirectory)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(FightsDirectory);
        Directory.CreateDirectory(ScriptsDirectory);
        Directory.CreateDirectory(ReviewsDirectory);
    }

    public string DataDirectory { get; }
    public string FightsDirectory => Path.Combine(DataDirectory, "fights");
    public string ScriptsDirectory => Path.Combine(DataDirectory, "scripts");
    public string ReviewsDirectory => Path.Combine(DataDirectory, "reviews");

    public static string ReviewId(string reportCode, int fightId, int playerId) => $"{reportCode}_fight{fightId}_p{playerId}";

    private string RawFightPath(string reportCode, int fightId, int playerId) =>
        Path.Combine(FightsDirectory, $"{ReviewId(reportCode, fightId, playerId)}.raw.json");

    /// <summary>True when the player's raw fight is already downloaded.</summary>
    public bool HasRawFight(string reportCode, int fightId, int playerId) => File.Exists(RawFightPath(reportCode, fightId, playerId));

    /// <summary>
    /// The player's raw fight as a <see cref="JsonDocument"/> over the file's bytes, downloaded from FellowshipLogs the first
    /// time.
    /// </summary>
    public async Task<JsonDocument> GetRawFightDocumentAsync(FellowshipLogsClient? client, string reportCode, int fightId,
        int playerId, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        string path = await GetRawFightFileAsync(client, reportCode, fightId, playerId, progress, ct);
        // Read in place: a document parsed from a stream copies the text into buffers it rents, doubling as it reads.
        return JsonDocument.Parse(await File.ReadAllBytesAsync(path, ct));
    }

    /// <summary>The player's raw fight as UTF-8 JSON, downloaded from FellowshipLogs the first time.</summary>
    public async Task<byte[]> GetRawFightBytesAsync(FellowshipLogsClient? client, string reportCode, int fightId, int playerId,
        IProgress<string>? progress = null, CancellationToken ct = default) =>
        await File.ReadAllBytesAsync(await GetRawFightFileAsync(client, reportCode, fightId, playerId, progress, ct), ct);

    /// <summary>
    /// The path of the player's raw fight, downloaded from FellowshipLogs the first time. One download runs at a time, so
    /// two callers of one player's fight download it once.
    /// </summary>
    public async Task<string> GetRawFightFileAsync(FellowshipLogsClient? client, string reportCode, int fightId, int playerId,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        string path = RawFightPath(reportCode, fightId, playerId);
        await _oneDownloadAtATime.WaitAsync(ct);
        try
        {
            if (File.Exists(path))
            {
                progress?.Report("Using the downloaded fight");
                return path;
            }
            if (client == null) throw new FellowshipLogsException("Sign in with FellowshipLogs to download fights.");
            progress?.Report("Downloading the fight from FellowshipLogs");
            // Written to a file of its own and moved into place once whole, so a failed download leaves no fight.
            string part = path + ".part";
            try
            {
                await using (var file = File.Create(part))
                {
                    await client.DownloadFightAsync(reportCode, fightId, playerId, file, progress, ct);
                }
                File.Move(part, path, overwrite: true);
            }
            finally
            {
                File.Delete(part);
            }
            return path;
        }
        finally
        {
            _oneDownloadAtATime.Release();
        }
    }

    private readonly SemaphoreSlim _oneDownloadAtATime = new(1, 1);

    /// <summary>Writes a review's replay script, page, summary, text, hits, issues and findings.</summary>
    public void Save(ReviewArtifacts review)
    {
        string id = review.Summary.Id;
        File.WriteAllBytes(Path.Combine(ScriptsDirectory, $"{id}.replay.json"), review.ScriptJson);
        File.WriteAllText(Path.Combine(ReviewsDirectory, review.Summary.HtmlFile), review.Html);
        File.WriteAllText(Path.Combine(ReviewsDirectory, $"{id}.summary.json"), JsonSerializer.Serialize(review.Summary, Indented));
        File.WriteAllText(Path.Combine(ReviewsDirectory, $"{id}.summary.txt"), review.SummaryText);
        File.WriteAllText(Path.Combine(ReviewsDirectory, $"{id}.hits.csv"), review.HitsCsv);
        File.WriteAllText(Path.Combine(ReviewsDirectory, $"{id}.issues.csv"), review.IssuesCsv);
        File.WriteAllText(Path.Combine(ReviewsDirectory, $"{id}.findings.json"), review.FindingsJson);
    }

    /// <summary>Every review run so far, newest first.</summary>
    public IReadOnlyList<ReviewSummary> ListReviews() =>
        Directory.GetFiles(ReviewsDirectory, "*.summary.json")
            .Select(f => JsonSerializer.Deserialize<ReviewSummary>(File.ReadAllText(f)))
            .Where(s => s != null && File.Exists(Path.Combine(ReviewsDirectory, s.HtmlFile)))
            .Select(s => s! with { FreeDice = LoadFreeDice(s.Id) })
            .OrderByDescending(s => s!.CreatedAt)
            .ToList()!;

    /// <summary>The review <paramref name="id"/>, or null when its summary or page is not in the reviews folder.</summary>
    public ReviewSummary? GetReview(string id)
    {
        string path = Path.Combine(ReviewsDirectory, $"{Path.GetFileName(id)}.summary.json");
        if (!File.Exists(path) || JsonSerializer.Deserialize<ReviewSummary>(File.ReadAllText(path)) is not { } summary) return null;
        return File.Exists(Path.Combine(ReviewsDirectory, summary.HtmlFile)) ? summary with { FreeDice = LoadFreeDice(summary.Id) } : null;
    }

    private FreeDiceSummary? LoadFreeDice(string id)
    {
        string path = Path.Combine(ReviewsDirectory, $"{id}.freedice.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<FreeDiceSummary>(File.ReadAllText(path)) : null;
    }

    /// <summary>Players' fights already downloaded (report code, fight ID, player ID).</summary>
    public IReadOnlyList<(string ReportCode, int FightId, int PlayerId)> ListDownloadedFights() =>
        Directory.GetFiles(FightsDirectory, "*.raw.json")
            .Select(f => RawFightName().Match(Path.GetFileName(f)))
            .Where(m => m.Success)
            .Select(m => (m.Groups[1].Value, int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)))
            .ToList();

    // A raw fight's file name: <report code>_fight<fight ID>_p<player ID>.raw.json.
    [GeneratedRegex(@"^(.+)_fight(\d+)_p(\d+)\.raw\.json$")]
    private static partial Regex RawFightName();
}
