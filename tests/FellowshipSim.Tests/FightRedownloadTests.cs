using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// The first downloaded fight in data/fights downloaded again for its player from FellowshipLogs' answers
/// (<see cref="FellowshipLogsFromFight"/>): the player's replay script from it equals the script from the fight as it was.
/// Without fights it does nothing.
/// </summary>
public class FightRedownloadTests(ITestOutputHelper output)
{
    [Fact]
    public async Task A_Fight_Downloaded_Again_Builds_The_Same_Script()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        if (library.ListDownloadedFights() is not [var (reportCode, fightId, playerId), ..])
        {
            output.WriteLine($"No fights in {library.FightsDirectory}.");
            return;
        }
        byte[] fight = await library.GetRawFightBytesAsync(null, reportCode, fightId, playerId);
        var again = new MemoryStream();
        await new FellowshipLogsClient(new HttpClient(new FellowshipLogsFromFight(fight)), "id", "secret")
            .DownloadFightAsync(reportCode, fightId, playerId, again);

        Assert.True(ReplayScriptBuilder.Build(fight, playerId).AsSpan().SequenceEqual(ReplayScriptBuilder.Build(again.ToArray(), playerId)),
            $"Player {playerId}'s script differs.");
    }
}
