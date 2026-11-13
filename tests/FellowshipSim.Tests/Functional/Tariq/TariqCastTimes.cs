using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each of Tariq's abilities keeps him busy. His abilities are instant: he is busy for the global cooldown, 1.5 seconds shortened
/// by haste, except Skull Crusher, whose global cooldown is shorter. Some abilities can be used during the global cooldown and take no time.
///
/// Every test casts at the start of the fight, so the fight clock when the cast returns is that time. Times are whole milliseconds.
/// </summary>
public class TariqCastTimes
{
    /// <summary>
    /// Wild Swing, Face Breaker, Heavy Strike, Chain Lightning and Hammer Storm take the 1.5 second global cooldown, shortened by haste:
    /// 1.5 / (1 + haste).
    ///
    /// Within 1ms.
    /// </summary>
    [Theory]
    [InlineData("Wild Swing", 0.0)]
    [InlineData("Wild Swing", 0.2)]
    [InlineData("Wild Swing", 0.5)]
    [InlineData("Face Breaker", 0.0)]
    [InlineData("Face Breaker", 0.2)]
    [InlineData("Face Breaker", 0.5)]
    [InlineData("Heavy Strike", 0.0)]
    [InlineData("Heavy Strike", 0.2)]
    [InlineData("Heavy Strike", 0.5)]
    [InlineData("Chain Lightning", 0.0)]
    [InlineData("Chain Lightning", 0.2)]
    [InlineData("Chain Lightning", 0.5)]
    [InlineData("Hammer Storm", 0.0)]
    [InlineData("Hammer Storm", 0.2)]
    [InlineData("Hammer Storm", 0.5)]
    public void Takes_The_1_5_Second_Global_Cooldown_Over_1_Plus_Haste(string ability, double haste)
    {
        var f = StepFights.Tariq(haste: haste);
        if (ability is "Hammer Storm") f.Set(Tariq.Fury, 100); // enough to cast it

        if (ability is "Chain Lightning") f.Cast<ThunderCall>(); // enables Chain Lightning
        double start = f.Seconds();

        f.Cast(ability);

        AssertSeconds(1.5 / (1 + haste), f.Seconds() - start, tolerance: 0.001);
    }

    /// <summary>
    /// Skull Crusher triggers a shortened global cooldown: 1 second, shortened by haste, 1 / (1 + haste).
    ///
    /// With 100 Fury. Within 1ms.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Skull_Crusher_Takes_The_1_Second_Shortened_Global_Cooldown_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Tariq(haste: haste);
        f.Set(Tariq.Fury, 100); // enough to cast it

        f.Cast<SkullCrusher>();

        AssertSeconds(1.0 / (1 + haste), f.Seconds(), tolerance: 0.001);
    }

    /// <summary>
    /// Leap Smash, Culling Strike, Thunder Call, Focused Wrath and Raging Tempest can be used during the global cooldown: they take no time.
    ///
    /// At 20% haste.
    /// </summary>
    [Theory]
    [InlineData("Leap Smash")]
    [InlineData("Culling Strike")]
    [InlineData("Thunder Call")]
    [InlineData("Focused Wrath")]
    [InlineData("Raging Tempest")]
    public void Off_Global_Cooldown_Abilities_Take_No_Time(string ability)
    {
        var f = StepFights.Tariq(haste: 0.2);
        f.SetHealth(0.2); // in execute range, for Culling Strike
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        f.Cast(ability);

        Assert.Equal(0, f.Seconds());
    }

    /// <summary>
    /// Abilities used during the global cooldown do not start one: Leap Smash, Thunder Call and Focused Wrath cast after a Wild Swing leave
    /// the clock where it was.
    /// </summary>
    [Fact]
    public void Off_Global_Cooldown_Abilities_Do_Not_Start_A_Global_Cooldown()
    {
        var f = StepFights.Tariq();

        f.Cast<WildSwing>();
        double afterWildSwing = f.Seconds();

        f.Cast<LeapSmash>();
        f.Cast<ThunderCall>();
        f.Cast<FocusedWrath>();

        Assert.Equal(afterWildSwing, f.Seconds());
    }
}
