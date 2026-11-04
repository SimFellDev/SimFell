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
        using var raw = JsonDocument.Parse(Fight(events));
        var e = Assert.Throws<ArgumentException>(() => ReplayReview.Run(raw.RootElement, 1));
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
}
