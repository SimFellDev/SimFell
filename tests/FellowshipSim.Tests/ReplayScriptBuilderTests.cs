using System.Text;
using System.Text.Json;
using FellowshipSim.Engine;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// A raw fight the replay cannot be built from (a download cut short): a review of it is refused with the reason, never a
/// NullReferenceException.
/// </summary>
public class ReplayScriptBuilderTests
{
    private static string Fight(string events) => $$"""
        { "report_code": "rf7qpV3tATCx86hN", "report_start_time": 1000,
          "fight": { "id": 6, "name": "Wraithtide Vault", "startTime": 100, "endTime": 200, "friendlyPlayers": [1] },
          "master_data": { "actors": [ { "id": 1, "name": "Frost", "type": "Player", "subType": "Rime" } ], "abilities": [] },
          "events": {{events}} }
        """;

    public static TheoryData<string, string> Refused => new()
    {
        { "null", "has no events" },
        { """[ { "timestamp": 100, "type": "dungeonstart" } ]""", "has no character information (combatantinfo) for player 1" },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void A_Fight_Without_The_Players_Events_Is_An_Argument_Error(string events, string reason)
    {
        var e = Assert.Throws<ArgumentException>(() => ReplayReview.Run(Encoding.UTF8.GetBytes(Fight(events)), 1));
        Assert.Contains(reason, e.Message);
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task The_Engine_Refuses_Its_Review(string events, string reason)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Fight(events)));
        var e = await Assert.ThrowsAsync<SimEngineException>(() => new LocalSimEngine().ReviewAsync(stream, 1));
        Assert.Contains(reason, e.Message);
    }

    private const string Combatant = """{ "timestamp": 100, "type": "combatantinfo", "sourceID": 1, "gear": [] }""";

    private static byte[] Casts(params (double Timestamp, int AbilityId)[] casts) => Encoding.UTF8.GetBytes(Fight(
        "[" + string.Join(", ", casts.Select(c => $$"""{ "timestamp": {{c.Timestamp}}, "type": "cast", "sourceID": 1, "abilityGameID": {{c.AbilityId}} }""").Prepend(Combatant)) + "]"));

    [Fact]
    public void An_Event_Earlier_Than_The_One_Before_It_Is_An_Argument_Error()
    {
        var e = Assert.Throws<ArgumentException>(() => ReplayScriptBuilder.Build(Casts((150, 7), (120, 7)), 1));
        Assert.Contains("out of time order at timestamp 120", e.Message);
    }

    [Fact]
    public void An_Event_Equal_To_One_At_Its_Timestamp_Is_Dropped()
    {
        using var script = JsonDocument.Parse(ReplayScriptBuilder.Build(Casts((150, 7), (150, 8), (150, 7), (160, 7)), 1));
        var casts = script.RootElement.GetProperty("commands").EnumerateArray()
            .Select(c => (c.GetProperty("t").GetDouble(), c.GetProperty("ability_id").GetInt32())).ToList();
        Assert.Equal([(0.05, 7), (0.05, 8), (0.06, 7)], casts);
    }
}
