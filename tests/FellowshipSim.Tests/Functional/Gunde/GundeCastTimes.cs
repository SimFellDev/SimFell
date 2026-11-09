using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each Gunde ability keeps you busy. His abilities are instant (Bloodbound Spirit lost its cast time in patch 0.4.5.0), so he is
/// busy for the global cooldown: 1.5 seconds, shortened by haste. Some abilities can be used during the global cooldown and take no time.
/// Every test casts at the start of the fight, so the fight clock when the cast returns is that time.
/// </summary>
public class GundeCastTimes
{
    /// <summary>
    /// Double Strike, Reaver's Edge, Blood Arc, Heart Splitter, Grim Carve and Rupture take the 1.5 second global cooldown, shortened by
    /// haste: 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData("Double Strike", 0.0)]
    [InlineData("Double Strike", 0.2)]
    [InlineData("Double Strike", 0.5)]
    [InlineData("Reaver's Edge", 0.0)]
    [InlineData("Reaver's Edge", 0.2)]
    [InlineData("Reaver's Edge", 0.5)]
    [InlineData("Blood Arc", 0.0)]
    [InlineData("Blood Arc", 0.2)]
    [InlineData("Blood Arc", 0.5)]
    [InlineData("Heart Splitter", 0.0)]
    [InlineData("Heart Splitter", 0.2)]
    [InlineData("Heart Splitter", 0.5)]
    [InlineData("Grim Carve", 0.0)]
    [InlineData("Grim Carve", 0.2)]
    [InlineData("Grim Carve", 0.5)]
    [InlineData("Rupture", 0.0)]
    [InlineData("Rupture", 0.2)]
    [InlineData("Rupture", 0.5)]
    public void Takes_The_1_5_Second_Global_Cooldown_Over_1_Plus_Haste(string ability, double haste)
    {
        var f = StepFights.Gunde(haste: haste);

        f.Cast(ability);

        AssertSeconds(1.5 / (1 + haste), f.Seconds());
    }

    /// <summary>
    /// Slaughter, Reign in Blood, Owed in Blood, Warbound, Butcher's Hook, Jawbreaker, Reckless Abandon and the auto attack can be used
    /// during the global cooldown: they take no time.
    ///
    /// At 20% haste, so a hasted global cooldown would show.
    /// </summary>
    [Theory]
    [InlineData("Slaughter")]
    [InlineData("Reign in Blood")]
    [InlineData("Owed in Blood")]
    [InlineData("Warbound")]
    [InlineData("Butcher's Hook")]
    [InlineData("Jawbreaker")]
    [InlineData("Reckless Abandon")]
    [InlineData("Attack")]
    public void Off_Global_Cooldown_Abilities_Take_No_Time(string ability)
    {
        var f = StepFights.Gunde(haste: 0.2);
        f.Apply(OwedInBlood.Buff, stacks: 1); // enough to cast Owed in Blood

        f.Cast(ability);

        Assert.Equal(0, f.Seconds());
    }

    /// <summary>
    /// Abilities used during the global cooldown do not start one: Reign in Blood, Owed in Blood and Warbound cast after a Heart
    /// Splitter leave the clock where it was.
    /// </summary>
    [Fact]
    public void Off_Global_Cooldown_Abilities_Do_Not_Start_A_Global_Cooldown()
    {
        var f = StepFights.Gunde();
        f.Apply(OwedInBlood.Buff, stacks: 1); // enough to cast Owed in Blood

        f.Cast<HeartSplitter>();
        double afterHeartSplitter = f.Seconds();

        f.Cast<ReignInBlood>();
        f.Cast<OwedInBlood>();
        f.Cast<Warbound>();

        Assert.Equal(afterHeartSplitter, f.Seconds());
    }

    /// <summary>
    /// Down times add up over a rotation: Double Strike, Reaver's Edge, Blood Arc, Heart Splitter and Grim Carve in a row take 5 global
    /// cooldowns, 5 x 1.5 / (1 + 0.5) = 5 seconds at 50% haste.
    ///
    /// Within 5ms, a millisecond per cast.
    /// </summary>
    [Fact]
    public void Down_Times_Add_Up_Over_A_Rotation()
    {
        var f = StepFights.Gunde(haste: 0.5);

        f.Cast<DoubleStrike>();
        f.Cast<ReaversEdge>();
        f.Cast<BloodArc>();
        f.Cast<HeartSplitter>();
        f.Cast<GrimCarve>();

        AssertSeconds(5 * 1.5 / (1 + 0.5), f.Seconds(), tolerance: 0.005);
    }
}
