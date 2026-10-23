using FellowshipSim.Core;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Tests.Functional;

public class StepDriverTests
{
    [Fact]
    public void Wait_Moves_The_Clock()
    {
        var fight = StepFights.Rime();
        fight.Wait(2.0);
        Assert.Equal(SimTime.FromSeconds(2.0), fight.Now);
    }

    [Fact]
    public void Cast_Returns_When_The_Hero_Is_Free_Again()
    {
        var fight = StepFights.Rime();
        fight.Cast<ColdSnap>(); // instant: free again when its global cooldown ends
        Assert.Equal(SimTime.FromSeconds(1.5), fight.Now);
        Assert.True(fight.IsFree);
    }

    [Fact]
    public void Cast_With_A_Cast_Time_Returns_After_The_Cast_And_The_Global_Cooldown()
    {
        var fight = StepFights.Rime();
        fight.Cast<FrostBolt>(); // 1.5s cast inside a 1.5s global cooldown
        Assert.Equal(SimTime.FromSeconds(1.5), fight.Now);
        Assert.True(fight.IsFree);
    }

    [Fact]
    public void An_Invalid_Cast_Throws_And_Does_Not_Move_Time()
    {
        var fight = StepFights.Rime();
        var ex = Assert.Throws<InvalidOperationException>(() => fight.Cast<GlacialBlast>()); // no Winter Orbs
        Assert.Contains("Glacial Blast", ex.Message);
        Assert.Equal(SimTime.Zero, fight.Now);
    }

    [Fact]
    public void TryCast_Returns_The_Reason_When_The_Cast_Is_Refused()
    {
        var fight = StepFights.Rime();
        var attempt = fight.TryCast<GlacialBlast>();
        Assert.False(attempt.Succeeded);
        Assert.False(string.IsNullOrEmpty(attempt.Reason));
    }

    [Fact]
    public void Events_Record_The_Fight_In_Order()
    {
        var fight = StepFights.Rime();
        fight.Cast<FrostBolt>();
        var kinds = fight.Events.Select(e => e.Kind).ToList();
        Assert.True(kinds.IndexOf(StepEventKind.CastStarted) < kinds.IndexOf(StepEventKind.CastCompleted));
    }
}
