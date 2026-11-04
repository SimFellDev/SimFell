using System.Text.Json;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// The memory a dungeon read and a replay script take from a long fight's <see cref="JsonDocument"/>: the 26-minute
/// Cithrel's Fall of report T2qLPpmMkrAtJ9B1 (fight 18, its Elarion, player 4) and the 30-minute Wraithtide Vault of report
/// rf7qpV3tATCx86hN (fight 6, its Tariq, player 83). Read as a JsonNode tree, a replay of either ran out of memory under a
/// 1.25 GB heap, and the browser app's worker has 2 GB for everything. With a fight not in data/fights it does nothing.
/// </summary>
public class RawFightMemoryTests(ITestOutputHelper output)
{
    /// <summary>The most a dungeon read allocates beyond the fight's document (the JsonNode tree read took 570 MB more).</summary>
    private const long DungeonReadBudget = 64L << 20;

    /// <summary>The most a replay script build allocates beyond the fight's document (about 440 MB for these fights).</summary>
    private const long ScriptBuildBudget = 768L << 20;

    [Theory]
    [InlineData("T2qLPpmMkrAtJ9B1", 18, 4)]
    [InlineData("rf7qpV3tATCx86hN", 6, 83)]
    public async Task A_Long_Fight_Is_Read_In_Place(string reportCode, int fightId, int playerId)
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        if (!library.HasRawFight(reportCode, fightId))
        {
            output.WriteLine($"Fight {fightId} of {reportCode} is not in {library.FightsDirectory}.");
            return;
        }
        using var fight = await library.GetRawFightDocumentAsync(null, reportCode, fightId);

        long before = GC.GetAllocatedBytesForCurrentThread();
        DungeonReader.FromRawFight(fight.RootElement, playerId);
        long dungeonRead = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        ReplayScriptBuilder.Build(fight.RootElement, playerId);
        long scriptBuild = GC.GetAllocatedBytesForCurrentThread() - before;

        output.WriteLine($"Dungeon read {dungeonRead >> 20} MB, replay script {scriptBuild >> 20} MB.");
        Assert.InRange(dungeonRead, 0, DungeonReadBudget);
        Assert.InRange(scriptBuild, 0, ScriptBuildBudget);
    }
}
