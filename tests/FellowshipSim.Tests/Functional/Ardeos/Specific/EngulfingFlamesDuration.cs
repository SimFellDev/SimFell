using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using ArdeosTalents = FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long an Engulfing Flames lasts: 9 seconds (12 with Undying Flame), and with Slow Burn each tick of a Fire Ball's burn on the enemy
/// adds half a second, so the order and timing of Fire Ball, Engulfing Flames and Wildfire matter.
/// Dev: the duration is measured from the moment Engulfing Flames is applied (the end of its cast) to the moment it is gone, advancing 10ms at a
/// time; within 50ms (100ms with no Slow Burn). Game data from observation in the C++ tests.
/// </summary>
public class ArdeosEngulfingFlamesDuration
{
    private static double Duration(StepDriver f, double since)
    {
        var dot = f.Ability<EngulfingFlames>().Dot;
        while (f.ArdeosOf().Dots.On(f.Enemy, dot).Any(d => d.IsActive) && f.Now.TotalSeconds < 60) f.Wait(0.01);
        return f.Now.TotalSeconds - since;
    }

    /// <summary>
    /// Player: an Engulfing Flames lasts 9 seconds, with or without a Fire Ball cast before it.
    /// Dev: no talents; Engulfing Flames alone, and after a Fire Ball; 9 seconds, within 0.1.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Engulfing_Flames_Lasts_9_Seconds(bool afterFireBall)
    {
        var f = StepFights.Ardeos();
        if (afterFireBall) f.Cast<FireBall>();

        f.Cast<EngulfingFlames>();

        Assert.Equal(9.0, Duration(f, f.Now.TotalSeconds), 0.1);
    }

    /// <summary>
    /// Player: with Undying Flame an Engulfing Flames lasts 12 seconds.
    /// Dev: Undying Flame taken; Engulfing Flames alone; 12 seconds, within 0.1.
    /// </summary>
    [Fact]
    public void Undying_Flame_Makes_Engulfing_Flames_Last_12_Seconds()
    {
        var f = StepFights.Ardeos(talents: [ArdeosTalents.UndyingFlame.TalentId]);

        f.Cast<EngulfingFlames>();

        Assert.Equal(12.0, Duration(f, f.Now.TotalSeconds), 0.1);
    }

    /// <summary>
    /// Player: with Slow Burn, a Fire Ball cast before an Engulfing Flames gives it 5 extensions of half a second: 11.5 seconds.
    /// Dev: Slow Burn taken; a Fire Ball, then an Engulfing Flames; 11.5 seconds, within 50ms.
    /// </summary>
    [Fact]
    public void Slow_Burn_Fire_Ball_Then_Engulfing_Flames_Lasts_11_5_Seconds()
    {
        var f = StepFights.Ardeos(talents: [ArdeosTalents.SlowBurn.TalentId]);
        f.Cast<FireBall>();
        f.Cast<EngulfingFlames>();

        Assert.Equal(11.5, Duration(f, f.Now.TotalSeconds), 0.05);
    }

    /// <summary>
    /// Player: with Slow Burn, a Fire Ball cast after an Engulfing Flames gives it only 4 extensions (the ball takes a second to land): 11 seconds.
    /// Dev: Slow Burn taken; an Engulfing Flames, then a Fire Ball; 11.0 seconds from the Engulfing Flames, within 50ms.
    /// </summary>
    [Fact]
    public void Slow_Burn_Engulfing_Flames_Then_Fire_Ball_Lasts_11_Seconds()
    {
        var f = StepFights.Ardeos(talents: [ArdeosTalents.SlowBurn.TalentId]);
        f.Cast<EngulfingFlames>();
        double start = f.Now.TotalSeconds;
        f.Cast<FireBall>();

        Assert.Equal(11.0, Duration(f, start), 0.05);
    }

    /// <summary>
    /// Player: with Slow Burn and Wildfire (which speeds the burn's ticks up, so more of them fit), a Fire Ball then an Engulfing Flames then
    /// Wildfire gives 7 extensions: 12.5 seconds.
    /// Dev: Slow Burn taken; a Fire Ball, an Engulfing Flames, Wildfire; 12.5 seconds from the Engulfing Flames, within 50ms.
    /// </summary>
    [Fact]
    public void Slow_Burn_And_Wildfire_Make_Engulfing_Flames_Last_12_5_Seconds()
    {
        var f = StepFights.Ardeos(talents: [ArdeosTalents.SlowBurn.TalentId]);
        f.Cast<FireBall>();
        f.Cast<EngulfingFlames>();
        double start = f.Now.TotalSeconds;
        f.Cast<Wildfire>();

        Assert.Equal(12.5, Duration(f, start), 0.05);
    }
}
