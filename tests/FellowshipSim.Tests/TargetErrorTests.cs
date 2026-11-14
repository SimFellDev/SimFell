using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;

namespace FellowshipSim.Tests;

/// <summary>
/// A sim to a target error ends after the first chunk, in chunk order, at which the fights so far have a margin of error
/// below the target: on threads, from chunks run apart, and on the page that merges chunks from the browser's workers.
/// </summary>
public class TargetErrorTests
{
    private const string Apl = """
        actions+=/flight_of_the_navir
        actions+=/ice_blitz
        actions+=/winters_blessing
        actions+=/ice_comet
        actions+=/glacial_blast
        actions+=/freezing_torrent
        actions+=/bursting_ice
        actions+=/cold_snap
        actions+=/frost_bolt
        """;

    private const int MostFights = 2000;
    private const double Target = 0.5;
    private static readonly SimTime Duration = SimTime.FromSeconds(60);
    private static readonly FightOptions Fight = new(LengthVariance: 0.2);

    private static MonteCarloSimulator Simulator()
    {
        var build = JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        return new MonteCarloSimulator(build, AplParser.ParseScript(Apl));
    }

    private static List<MonteCarloChunk> AllChunks() =>
        Enumerable.Range(0, MonteCarloSimulator.ChunkCount(MostFights, Target))
            .Select(i => Simulator().RunChunk(i, Duration, MostFights, firstSeed: 3, fight: Fight, targetError: Target)).ToList();

    // The chunks up to the first at which the fights so far reach the target.
    private static int ChunksReaching(IReadOnlyList<MonteCarloChunk> chunks)
    {
        var merged = new DpsMoments();
        for (int i = 0; i < chunks.Count; i++)
        {
            merged = merged.Merge(new DpsMoments(chunks[i].Count, chunks[i].Mean, chunks[i].M2));
            if (merged.Reached(Target)) return i + 1;
        }
        return chunks.Count;
    }

    [Fact]
    public void A_Run_Ends_At_The_First_Chunk_That_Reaches_The_Target_On_Any_Threads()
    {
        var chunks = AllChunks();
        int reaching = ChunksReaching(chunks);
        Assert.True(reaching < chunks.Count, "The target is reached before the most fights.");
        int fights = (int)chunks.Take(reaching).Sum(c => c.Count);

        foreach (int threads in new[] { 1, 4 })
        {
            var result = Simulator().Run(Duration, MostFights, firstSeed: 3, degreeOfParallelism: threads, fight: Fight, targetError: Target);
            Assert.Equal(fights, result.Iterations);
            Assert.Equal(Target, result.TargetError);
            Assert.Equal(fights, result.Distribution!.Counts.Sum());
            Assert.True(DpsMoments.MarginPercentOf(result.StandardDeviation, result.Iterations, result.MeanDps) < Target);
        }
    }

    [Fact]
    public void Chunks_Up_To_The_Target_Combine_To_The_Runs_Result()
    {
        var chunks = AllChunks();
        int reaching = ChunksReaching(chunks);
        var whole = Simulator().Run(Duration, MostFights, firstSeed: 3, degreeOfParallelism: 4, fight: Fight, targetError: Target);
        var combined = MonteCarloSimulator.Combine(chunks.Take(reaching).ToList(), Duration, MostFights, targetError: Target);

        var wholeJson = JsonSerializer.SerializeToNode(whole, SimChunks.Json)!.AsObject();
        var combinedJson = JsonSerializer.SerializeToNode(combined, SimChunks.Json)!.AsObject();
        Assert.Equal(wholeJson.ToJsonString(), combinedJson.ToJsonString());

        Assert.Throws<ArgumentException>(() => MonteCarloSimulator.Combine(chunks.Take(reaching + 1).ToList(), Duration, MostFights, targetError: Target));
        Assert.Throws<ArgumentException>(() => MonteCarloSimulator.Combine(chunks.Take(reaching - 1).ToList(), Duration, MostFights, targetError: Target));
    }

    [Fact]
    public void The_Page_Ends_A_Browser_Run_At_The_Same_Chunk_Whatever_Order_Chunks_Finish()
    {
        var chunks = AllChunks();
        var settings = new SimSettings(MostFights, 60, FirstSeed: 3, LengthVariance: 0.2, TargetError: Target);
        var progress = new SimChunkProgress(settings);
        // The last chunks first, as a slow worker can have the first.
        foreach (var chunk in chunks.AsEnumerable().Reverse()) progress.Add(chunk.Index, new DpsMoments(chunk.Count, chunk.Mean, chunk.M2));
        Assert.Equal(ChunksReaching(chunks), progress.ResultChunks);
        Assert.True(progress.Progress.Fraction == 1);
    }

    [Fact]
    public void A_Run_With_Many_Most_Fights_Ends_At_The_Same_Fight_In_Chunks_Of_100()
    {
        const int most = 20_000;
        Assert.Equal(MonteCarloSimulator.ChunkCount(most) * 2, MonteCarloSimulator.ChunkCount(most, Target));
        var chunks = new List<MonteCarloChunk>();
        var merged = new DpsMoments();
        while (!merged.Reached(Target))
        {
            var chunk = Simulator().RunChunk(chunks.Count, Duration, most, firstSeed: 3, fight: Fight, targetError: Target);
            Assert.Equal(DpsMoments.TargetErrorMinimumFights, chunk.Count);
            chunks.Add(chunk);
            merged = merged.Merge(new DpsMoments(chunk.Count, chunk.Mean, chunk.M2));
        }
        var whole = Simulator().Run(Duration, most, firstSeed: 3, degreeOfParallelism: 4, fight: Fight, targetError: Target);
        var combined = MonteCarloSimulator.Combine(chunks, Duration, most, targetError: Target);
        Assert.Equal(JsonSerializer.Serialize(whole, SimChunks.Json), JsonSerializer.Serialize(combined, SimChunks.Json));
    }

    [Fact]
    public void A_Run_To_A_Target_Checks_Its_Error_At_Least_Every_100_Fights()
    {
        Assert.Equal(100, MonteCarloSimulator.ChunkCount(100_000));
        Assert.Equal(1000, MonteCarloSimulator.ChunkCount(100_000, targetError: 0.1));
        Assert.Equal(MonteCarloSimulator.ChunkCount(MostFights), MonteCarloSimulator.ChunkCount(MostFights, Target));
    }

    [Fact]
    public void A_Run_Without_A_Target_Fights_Every_Iteration()
    {
        var result = Simulator().Run(Duration, 150, firstSeed: 3, fight: Fight);
        Assert.Equal(150, result.Iterations);
        Assert.Null(result.TargetError);
    }

    [Fact]
    public void A_Profile_Keeps_Its_Target_Error()
    {
        var sim = SimProfile.Parse("rime=\"x\"\ntarget_error=0.1\n");
        Assert.Equal(0.1, sim.Settings.TargetError);
        Assert.Equal(SimProfile.DefaultTargetErrorIterations, sim.Settings.Iterations);
        Assert.Contains("target_error=0.1", sim.Format());
        Assert.Equal(sim.Settings, SimProfile.Parse(sim.Format()).Settings);

        var none = SimProfile.Parse("rime=\"x\"\n");
        Assert.Equal(0, none.Settings.TargetError);
        Assert.DoesNotContain(SimProfile.TargetError, none.Format());
    }
}
