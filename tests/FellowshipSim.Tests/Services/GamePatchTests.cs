using FellowshipSim.Logs;

namespace FellowshipSim.Tests.Services;

/// <summary>Telling a log from the patch the engine models apart from an older one.</summary>
public class GamePatchTests
{
    [Theory]
    [InlineData("2026-10-23T18:00:00Z", 9, true)]
    [InlineData("2026-10-22T00:00:00Z", 9, true)]
    [InlineData("2026-10-23T18:00:00Z", null, true)]
    [InlineData("2026-10-21T23:59:59Z", 9, false)]
    [InlineData("2026-10-01T12:00:00Z", 8, false)]
    [InlineData("2026-10-23T18:00:00Z", 8, false)]
    public void A_Log_Is_Current_From_The_Release_Day_With_The_Patchs_Log_Format(string started, int? logVersion, bool current)
    {
        var startUtc = DateTime.Parse(started, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.Equal(current, new GamePatch("0.5.0.0", new DateOnly(2026, 10, 22), 9).Covers(startUtc, logVersion));
    }
}
