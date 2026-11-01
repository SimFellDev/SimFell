using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;

namespace FellowshipSim.Tests;

/// <summary>
/// <see cref="ISimEngine"/> through <see cref="LocalSimEngine"/>: each result reads back from its JSON unchanged, so an
/// engine server can send it, and a refused request throws <see cref="SimEngineException"/>.
/// </summary>
public class SimEngineContractTests
{
    private const string Profile = """
        rime="sww"
        primary_stat=300
        crit=30%
        haste=24%
        expertise=0%
        spirit=0%
        talents=avalanche/coalescing_frost/cold_shower/chilling_finesse/cascading_blitz/bursting_swallows/soulfrost_torrent/supreme_torrent/wisdom_of_the_north
        weapon=ashas_chronoshift_spire

        actions+=/chronoshift,if=cooldown.ice_blitz.remains>10
        actions+=/ice_blitz
        actions+=/winters_blessing,if=buff.ice_blitz.up|cooldown.ice_blitz.remains>20
        actions+=/bursting_ice,target_if=min:debuff.bursting_ice.remains,if=debuff.bursting_ice.remains<2
        actions+=/freezing_torrent
        actions+=/frost_bolt
        """;

    private static readonly JsonSerializerOptions Web = JsonSerializerOptions.Web;
    private readonly LocalSimEngine _engine = new();

    [Fact]
    public async Task Heroes_Include_Rime()
    {
        Assert.Contains("Rime", await _engine.GetHeroesAsync());
    }

    [Fact]
    public async Task A_Vocabulary_Reads_Back_From_Its_Json()
    {
        var vocabulary = (await _engine.GetVocabularyAsync("Rime"))!;
        Assert.NotEmpty(vocabulary.BuildNames[PropertyExpression.TalentScope]);
        AssertRoundTrip(vocabulary);
        Assert.Null(await _engine.GetVocabularyAsync("Nobody"));
    }

    [Fact]
    public async Task An_Expression_Check_Reads_Back_From_Its_Json()
    {
        var check = (await _engine.CheckExpressionAsync("Rime", "buff.ice_blitz.up&cooldown.ice_blitz.remains>10|-!worb+100*(spirit>=15|talent.x)<=gcd.max<1"))!;
        Assert.NotNull(check.Condition);
        AssertRoundTrip(check);
        Assert.Null(await _engine.CheckExpressionAsync("Nobody", "1"));
    }

    [Fact]
    public async Task An_Action_List_Check_Reads_Back_From_Its_Json()
    {
        var check = (await _engine.CheckActionListAsync("Rime", "# opener\nactions+=/frost_bolt,if=buff.ice_blitz.up\nactions+=/nothing,if=("))!;
        Assert.Contains(check.Tokens, t => t.Kind == AplTokenKind.Comment);
        Assert.NotNull(check.Error);
        AssertRoundTrip(check);
        Assert.Null(await _engine.CheckActionListAsync("Nobody", "actions+=/frost_bolt"));
    }

    [Fact]
    public async Task Parsed_Actions_And_Written_Text_Read_Back_From_Their_Json()
    {
        var actions = await _engine.ParseActionListAsync(SimcProfile.Parse(Profile).ActionList);
        AssertRoundTrip(actions);
        var text = await _engine.WriteActionListAsync(actions.Actions);
        Assert.NotNull(text.Text);
        AssertRoundTrip(text);
    }

    [Fact]
    public async Task Text_Outside_The_Grammar_Is_Refused()
    {
        await Assert.ThrowsAsync<SimEngineException>(() => _engine.ParseActionListAsync("actions+=/frost_bolt,if=(("));
    }

    [Fact]
    public async Task A_Sim_Request_From_Json_Has_The_Result_Of_The_Request()
    {
        // One thread: with several, the threads' totals merge in the order the threads finish, and the mean and standard
        // deviation differ in their last digits from run to run.
        var request = new SimRequest(SimcProfile.Parse(Profile), new SimSettings(Iterations: 20, DurationSeconds: 60, TargetCount: 2, DegreeOfParallelism: 1));
        var sent = JsonSerializer.Deserialize<SimRequest>(JsonSerializer.Serialize(request, Web), Web)!;
        var progress = new Collected();

        var result = await _engine.SimulateAsync(request.Profile, request.Settings);
        var fromJson = await _engine.SimulateAsync(sent.Profile, sent.Settings, progress);

        Assert.Equal(JsonSerializer.Serialize(result, Web), JsonSerializer.Serialize(fromJson, Web));
        Assert.Equal(20, result.Iterations);
        Assert.Equal(new SimProgress(20, 20), progress.Reports[^1]);
        AssertRoundTrip(result);
        AssertRoundTrip(new SimStreamLine(Progress: progress.Reports[0]), SimStreamLine.Json);
        AssertRoundTrip(new SimStreamLine(Result: result), SimStreamLine.Json);
    }

    [Fact]
    public async Task A_Dungeon_Sim_Request_From_Json_Has_The_Result_Of_The_Request()
    {
        var settings = new SimSettings(Iterations: 10, DurationSeconds: DefaultDungeons.Default.TimeLimitSeconds, DegreeOfParallelism: 1,
            Dungeon: DefaultDungeons.Default);
        var sent = JsonSerializer.Deserialize<SimSettings>(JsonSerializer.Serialize(settings, Web), Web)!;

        Assert.Equal(settings.Dungeon!.Format(), sent.Dungeon!.Format());
        var result = await _engine.SimulateAsync(SimcProfile.Parse(Profile), settings);
        Assert.Equal(JsonSerializer.Serialize(result, Web), JsonSerializer.Serialize(await _engine.SimulateAsync(SimcProfile.Parse(Profile), sent), Web));
        Assert.True(result.BossDps > 0);
    }

    [Fact]
    public async Task A_Dungeon_With_A_Pull_Of_No_Hit_Points_Is_Refused()
    {
        var dungeon = new DungeonProfile("Broken", DungeonProfile.DefaultHeroShare, 600, [new DungeonPull("1", [new EnemyGroup(1, "Boss", HitPoints: 0, Boss: true)])]);
        await Assert.ThrowsAsync<SimEngineException>(() => _engine.SimulateAsync(SimcProfile.Parse(Profile), new SimSettings(1, 10, Dungeon: dungeon)));
    }

    [Fact]
    public async Task An_Action_List_Naming_No_Ability_Of_The_Hero_Is_Refused()
    {
        var profile = SimcProfile.Parse(Profile) with { ActionList = "actions+=/not_an_ability\n" };
        await Assert.ThrowsAsync<SimEngineException>(() => _engine.SimulateAsync(profile, new SimSettings(1, 10)));
        await Assert.ThrowsAsync<SimEngineException>(() => _engine.TraceFightAsync(profile, new SimSettings(1, 10)));
    }

    [Fact]
    public async Task A_Sims_Typical_Fight_Is_Traced_Again_From_Its_Seed()
    {
        var profile = SimcProfile.Parse(Profile);
        var settings = new SimSettings(Iterations: 40, DurationSeconds: 90, TargetCount: 8, DegreeOfParallelism: 2, LengthVariance: 0.2);
        var result = await _engine.SimulateAsync(profile, settings);
        int typical = result.TypicalFight!.Value;

        var trace = await _engine.TraceFightAsync(profile, settings with { Iterations = 1, FirstSeed = settings.FirstSeed + typical });
        Assert.Equal(result.Casts, trace.Casts);
    }

    [Fact]
    public async Task A_Traced_Fight_Reads_Back_From_Its_Json()
    {
        var trace = await _engine.TraceFightAsync(SimcProfile.Parse(Profile), new SimSettings(Iterations: 1, DurationSeconds: 30, TargetCount: 2));
        Assert.NotEmpty(trace.Decisions);
        AssertRoundTrip(trace, SimChunks.Json);
    }

    [Fact]
    public async Task A_Cancelled_Sim_Throws_OperationCanceledException()
    {
        using var cancel = new CancellationTokenSource();
        var progress = new Collected(cancel.Cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _engine.SimulateAsync(SimcProfile.Parse(Profile), new SimSettings(100_000, 60, DegreeOfParallelism: 2), progress, cancel.Token));
    }

    private static void AssertRoundTrip<T>(T value, JsonSerializerOptions? options = null)
    {
        options ??= Web;
        string json = JsonSerializer.Serialize(value, options);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<T>(json, options), options));
    }

    private sealed class Collected(Action? onReport = null) : IProgress<SimProgress>
    {
        public List<SimProgress> Reports { get; } = [];

        public void Report(SimProgress value)
        {
            lock (Reports) Reports.Add(value);
            onReport?.Invoke();
        }
    }
}
