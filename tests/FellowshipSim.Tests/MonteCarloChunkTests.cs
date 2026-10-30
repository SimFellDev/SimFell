using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// A run split into chunks, each run by its own simulator and sent through JSON as the browser app's workers do, combines
/// to the same result as the run on threads, to the last bit.
/// </summary>
public class MonteCarloChunkTests
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

    private static readonly JsonSerializerOptions Json = new(JsonSerializerOptions.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    [Theory]
    [InlineData(250, 2, true)]
    [InlineData(37, 1, false)]
    public void Chunks_Run_Apart_Combine_To_The_Runs_Result(int iterations, int targets, bool keepFightDamage)
    {
        var build = JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        var duration = SimTime.FromSeconds(60);
        var whole = new MonteCarloSimulator(build, AplParser.ParseScript(Apl))
            .Run(duration, iterations, targets, firstSeed: 5, degreeOfParallelism: 4, keepFightDamage: keepFightDamage, rollTypicalFight: true);

        // Two "workers", each with its own simulator, take the chunks in turn.
        var workers = new[] { new MonteCarloSimulator(build, AplParser.ParseScript(Apl)), new MonteCarloSimulator(build, AplParser.ParseScript(Apl)) };
        var chunks = Enumerable.Range(0, MonteCarloSimulator.ChunkCount(iterations))
            .Select(i => workers[i % 2].RunChunk(i, duration, iterations, targets, firstSeed: 5, keepFightDamage: keepFightDamage))
            .Select(c => JsonSerializer.Deserialize<MonteCarloChunk>(JsonSerializer.Serialize(c, Json), Json)!)
            .ToList();
        var combined = MonteCarloSimulator.Combine(chunks, duration, iterations, keepFightDamage);
        // A combined run's typical fight is rolled again, as SimChunks.Combine has it.
        var rolled = workers[1].RollFight(combined.TypicalFight!.Value, duration, targets, firstSeed: 5);

        Assert.Equal(JsonSerializer.Serialize(whole.Hits, Json), JsonSerializer.Serialize(rolled.Hits, Json));
        Assert.Equal(JsonSerializer.Serialize(whole.Casts, Json), JsonSerializer.Serialize(rolled.Casts, Json));
        var wholeJson = JsonSerializer.SerializeToNode(whole, Json)!.AsObject();
        var combinedJson = JsonSerializer.SerializeToNode(combined, Json)!.AsObject();
        foreach (string rolledPart in new[] { "hits", "casts" })
        {
            wholeJson.Remove(rolledPart);
            combinedJson.Remove(rolledPart);
        }
        Assert.Equal(wholeJson.ToJsonString(), combinedJson.ToJsonString());
    }

    [Fact]
    public void Combine_Wants_Every_Chunk_In_Order()
    {
        var build = JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        var simulator = new MonteCarloSimulator(build, AplParser.ParseScript(Apl));
        var duration = SimTime.FromSeconds(30);
        var first = simulator.RunChunk(0, duration, 3);
        var second = simulator.RunChunk(1, duration, 3);
        Assert.Throws<ArgumentException>(() => MonteCarloSimulator.Combine([second, first, simulator.RunChunk(2, duration, 3)], duration, 3));
        Assert.Throws<ArgumentException>(() => MonteCarloSimulator.Combine([first, second], duration, 3));
    }
}
