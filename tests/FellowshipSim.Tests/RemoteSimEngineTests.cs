using System.Net;
using System.Text;
using System.Text.Json;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;
using FellowshipSim.EngineServer;
using FellowshipSim.Service.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace FellowshipSim.Tests;

/// <summary>
/// <see cref="RemoteSimEngine"/> against the engine server's endpoints (<see cref="EngineApp"/> on a test server): each
/// call has the result <see cref="LocalSimEngine"/> has, and each refusal throws the exception <see cref="ISimEngine"/> names.
/// </summary>
public sealed class RemoteSimEngineTests : IAsyncLifetime
{
    private const string Key = "remote-sim-engine-tests";

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

    private readonly WebApplication _server = EngineApp.Build([$"--EngineKey={Key}"], b => b.WebHost.UseTestServer());
    private readonly LocalSimEngine _local = new();
    private RemoteSimEngine _engine = null!;

    public async Task InitializeAsync()
    {
        await _server.StartAsync();
        _engine = Remote(Key);
    }

    public async Task DisposeAsync() => await _server.DisposeAsync();

    [Fact]
    public async Task Heroes_Are_The_Engines_Heroes()
    {
        Assert.Equal(await _local.GetHeroesAsync(), await _engine.GetHeroesAsync());
    }

    [Fact]
    public async Task A_Vocabulary_Is_The_Engines_And_No_Hero_Is_Null()
    {
        Assert.Equal(Json(await _local.GetVocabularyAsync("Rime")), Json(await _engine.GetVocabularyAsync("Rime")));
        Assert.Null(await _engine.GetVocabularyAsync("Nobody"));
    }

    [Fact]
    public async Task An_Expression_Check_Is_The_Engines_And_No_Hero_Is_Null()
    {
        const string text = "buff.ice_blitz.up&cooldown.ice_blitz.remains>10";
        Assert.Equal(Json(await _local.CheckExpressionAsync("Rime", text)), Json(await _engine.CheckExpressionAsync("Rime", text)));
        Assert.Null(await _engine.CheckExpressionAsync("Nobody", "1"));
    }

    [Fact]
    public async Task Parsed_Actions_And_Written_Text_Are_The_Engines()
    {
        string actionList = SimcProfile.Parse(Profile).ActionList;
        var actions = await _engine.ParseActionListAsync(actionList);
        Assert.Equal(Json(await _local.ParseActionListAsync(actionList)), Json(actions));
        Assert.Equal(Json(await _local.WriteActionListAsync(actions.Actions)), Json(await _engine.WriteActionListAsync(actions.Actions)));
    }

    [Fact]
    public async Task Text_Outside_The_Grammar_Is_Refused()
    {
        await Assert.ThrowsAsync<SimEngineException>(() => _engine.ParseActionListAsync("actions+=/frost_bolt,if=(("));
    }

    [Fact]
    public async Task A_Review_Of_A_Player_Missing_From_The_Fight_Is_Refused()
    {
        using var fight = new MemoryStream("""{"fight":{"id":7}}"""u8.ToArray());
        var e = await Assert.ThrowsAsync<SimEngineException>(() => _engine.ReviewAsync(fight, 1));
        Assert.Contains("Player 1", e.Message);
    }

    [Fact]
    public async Task A_Sim_Has_The_Engines_Result_And_Its_Progress()
    {
        var profile = SimcProfile.Parse(Profile);
        // One thread: with several, the threads' totals merge in the order the threads finish, and the mean and standard
        // deviation differ in their last digits from run to run.
        var settings = new SimSettings(Iterations: 20, DurationSeconds: 60, TargetCount: 2, DegreeOfParallelism: 1);
        var progress = new Collected();

        var result = await _engine.SimulateAsync(profile, settings, progress);

        Assert.Equal(Json(await _local.SimulateAsync(profile, settings)), Json(result));
        Assert.Equal(new SimProgress(20, 20), progress.Reports[^1]);
    }

    [Fact]
    public async Task A_Sim_With_No_Fights_Is_Refused()
    {
        await Assert.ThrowsAsync<SimEngineException>(() => _engine.SimulateAsync(SimcProfile.Parse(Profile), new SimSettings(0, 60)));
    }

    [Fact]
    public async Task An_Action_List_Naming_No_Ability_Of_The_Hero_Is_Refused()
    {
        var profile = SimcProfile.Parse(Profile) with { ActionList = "actions+=/not_an_ability\n" };
        var e = await Assert.ThrowsAsync<SimEngineException>(() => _engine.SimulateAsync(profile, new SimSettings(1, 10)));
        Assert.Contains("not_an_ability", e.Message);
    }

    [Fact]
    public async Task A_Cancelled_Sim_Throws_OperationCanceledException()
    {
        using var cancel = new CancellationTokenSource();
        var progress = new Collected(cancel.Cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _engine.SimulateAsync(SimcProfile.Parse(Profile), new SimSettings(100_000, 60, DegreeOfParallelism: 2), progress, cancel.Token));
    }

    [Fact]
    public async Task A_Request_Without_The_Key_Is_Refused()
    {
        var e = await Assert.ThrowsAsync<HttpRequestException>(() => Remote("not-the-key").GetHeroesAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, e.StatusCode);
    }

    private RemoteSimEngine Remote(string key)
    {
        var http = _server.GetTestServer().CreateClient();
        http.Timeout = Timeout.InfiniteTimeSpan;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EngineKey"] = key }).Build();
        return new RemoteSimEngine(http, configuration);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonSerializerOptions.Web);

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
