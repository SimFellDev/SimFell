using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Replays every fight downloaded into the data folder (data/fights, e.g. through the web page) for each player whose
/// hero the engine supports, and writes their review pages to data/reviews. With no fights downloaded it does nothing.
/// Run with: dotnet test --filter AllDownloadedFights
/// </summary>
public class CombatLogReplayTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AllDownloadedFights_Replay_And_Write_Reviews()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        var fights = library.ListDownloadedFights();
        if (fights.Count == 0)
        {
            output.WriteLine($"No fights in {library.FightsDirectory}; import one through the web page first.");
            return;
        }

        foreach (var (reportCode, fightId, playerId) in fights)
        {
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId, playerId);
            foreach (var player in ReplayScriptBuilder.Players(raw).Where(p => p.Id == playerId && ReplayDriver.SupportsHero(p.Hero)))
            {
                byte[] scriptJson = ReplayScriptBuilder.Build(raw, player.Id);
                var script = ReplayScript.FromJson(scriptJson);
                var result = new ReplayDriver(ReplayDriver.CreateHero(script), script, new ReplayOptions { StopOnFirstFailure = false }).Run();
                var artifacts = ReplayReview.Artifacts(result, script, scriptJson, player.Id);
                library.Save(artifacts);
                var review = artifacts.Summary;
                output.WriteLine($"{review.PlayerName} ({review.Hero}) {review.FightName}: {review.MatchedHits}/{review.LogHits} hits " +
                                 $"({result.LoggedHitsDealt} dealt by the replay), engine damage {review.EngineDamage / review.LogDamage:P1} of log, " +
                                 $"{review.PassedSteps}/{review.TotalSteps} clean steps, {result.Issues.Count} issues " +
                                 $"({string.Join(", ", result.Issues.GroupBy(i => i.Category).Select(g => $"{g.Key} {g.Count()}"))}) " +
                                 $"-> {Path.Combine(library.ReviewsDirectory, review.HtmlFile)} ({artifacts.Html.Length / 1024:N0} KB)");
                ExactReplayAssertions.Hold(result);
            }
        }
    }

    [Fact]
    public async Task A_Cancelled_Replay_Stops_At_Its_First_Step()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        foreach (var (reportCode, fightId, playerId) in library.ListDownloadedFights())
        {
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId, playerId);
            if (!ReplayScriptBuilder.Players(raw).Any(p => p.Id == playerId && ReplayDriver.SupportsHero(p.Hero))) continue;
            var script = ReplayScript.FromJson(ReplayScriptBuilder.Build(raw, playerId));
            var driver = new ReplayDriver(ReplayDriver.CreateHero(script), script, new ReplayOptions { StopOnFirstFailure = false });

            Assert.Throws<OperationCanceledException>(() => driver.Run(new CancellationToken(canceled: true)));
            return;
        }
    }
}

internal static class TestPaths
{
    /// <summary>The folder holding FellowshipSim.slnx.</summary>
    public static string RepositoryRoot { get; } = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (dir.GetFiles("FellowshipSim.slnx").Length > 0) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Could not find the repository root (FellowshipSim.slnx).");
    }
}
