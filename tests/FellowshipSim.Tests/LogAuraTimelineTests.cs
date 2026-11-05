using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

public class LogAuraTimelineTests
{
    private const int StealthId = 1001591;
    private const double Tolerance = 0.1;

    [Fact]
    public void A_Gain_After_An_Absence_Is_Read_From_The_Cast_Time()
    {
        var log = Timeline(Aura(516.058, "applybuff"), Aura(517.728, "removebuff"), Aura(533.935, "applybuff"));

        Assert.True(log.GainedAfterAbsence(StealthId, 533.934, Tolerance));
    }

    [Fact]
    public void A_Removal_Within_The_Tolerance_Before_The_Cast_Is_No_Absence()
    {
        var log = Timeline(Aura(10.0, "applybuff"), Aura(11.95, "removebuff"), Aura(12.001, "applybuff"));

        Assert.False(log.GainedAfterAbsence(StealthId, 12.0, Tolerance));
    }

    [Fact]
    public void An_Aura_Up_Before_The_Cast_Has_No_Absence()
    {
        var log = Timeline(Aura(10.0, "applybuff"), Aura(12.05, "removebuff"));

        Assert.False(log.GainedAfterAbsence(StealthId, 12.0, Tolerance));
    }

    [Fact]
    public void No_Gain_Within_The_Tolerance_Is_No_Gain()
    {
        var log = Timeline(Aura(10.0, "applybuff"), Aura(11.0, "removebuff"), Aura(12.5, "applybuff"));

        Assert.False(log.GainedAfterAbsence(StealthId, 12.0, Tolerance));
    }

    private static LogAuraTimeline Timeline(params LogAura[] events) => new(events, [StealthId], _ => 0);

    private static LogAura Aura(double time, string type) =>
        new() { Time = time, Type = type, AbilityId = StealthId, AbilityName = "Brooding Shadows" };
}
