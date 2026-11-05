using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// The memory a replay script build takes from a long fight's UTF-8 JSON: the 26-minute Cithrel's Fall of report
/// T2qLPpmMkrAtJ9B1 (fight 18, its Elarion, player 4) and the 30-minute Wraithtide Vault of report rf7qpV3tATCx86hN
/// (fight 6, its Tariq, player 83). The browser app's worker has 2 GB for everything. With a fight not in data/fights it
/// does nothing.
/// </summary>
public class RawFightMemoryTests(ITestOutputHelper output)
{
    /// <summary>The most a replay script build allocates beyond the raw fight's bytes and the script's own UTF-8.</summary>
    private const long ScriptBuildBudget = 64L << 20;

    [Theory]
    [InlineData("T2qLPpmMkrAtJ9B1", 18, 4)]
    [InlineData("rf7qpV3tATCx86hN", 6, 83)]
    public async Task A_Long_Fight_Is_Read_In_Place(string reportCode, int fightId, int playerId)
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        if (!library.HasRawFight(reportCode, fightId, playerId))
        {
            output.WriteLine($"Player {playerId}'s fight {fightId} of {reportCode} is not in {library.FightsDirectory}.");
            return;
        }
        byte[] fight = await library.GetRawFightBytesAsync(null, reportCode, fightId, playerId);

        long before = GC.GetAllocatedBytesForCurrentThread();
        byte[] script = ReplayScriptBuilder.Build(fight, playerId);
        long scriptBuild = GC.GetAllocatedBytesForCurrentThread() - before;

        output.WriteLine($"Replay script {script.Length >> 20} MB, build {scriptBuild >> 20} MB allocated.");
        Assert.InRange(scriptBuild, 0, script.Length + ScriptBuildBudget);
    }
}
