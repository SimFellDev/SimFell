using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Fire Ball's burn: the damage over time a Fire Ball sets the enemy on fire with (20% of the hit over 12 seconds, a tick every 2 seconds
/// at any haste), when the ball lands, and how recasting and Wildfire change its ticks.
///
/// The expectations are the C++ sim's tests' ("game-accurate expectation", not derived from an engine). Ticks are compared with the
/// first tick of the same fight. How fast the burn ticks with Wildfire is in Specific/ArdeosDotTickSpeed.cs, and how a partial tick
/// extends effects with Slow Burn in ArdeosTalents.cs.
/// </summary>
public class ArdeosFireBallDot
{
    // ---- The ball

    /// <summary>
    /// A Fire Ball lands about 1 second after you cast it.
    ///
    /// Checked within 50ms.
    /// </summary>
    [Fact]
    public void A_Fire_Ball_Lands_1_Second_After_The_Cast()
    {
        var f = StepFights.Ardeos();

        f.Cast<FireBall>();
        f.Wait(2); // the ball lands

        var hit = Assert.Single(f.DirectHits<FireBall>());
        AssertSeconds(1.0, hit.Time.TotalSeconds, tolerance: 0.05);
    }

    // ---- The burn's ticks

    /// <summary>Fire Ball's burn ticks 6 times, all for the same damage, whatever your haste.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void Fire_Balls_Burn_Ticks_6_Times_For_Equal_Damage_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste: haste).WithoutDamageSpread();

        f.Cast<FireBall>();
        f.Wait(20); // the burn ticks out

        var ticks = f.Ticks(FireBallDot.EffectId);

        using var check = new Checks(f);
        check.Equal(6, ticks.Count);
        foreach (var tick in ticks) check.Ratio(1.0, tick.Value / ticks[0].Value, what: $"tick at {tick.Time.TotalSeconds:F3}s");
    }

    /// <summary>
    /// A second Fire Ball cast as soon as the first one's global cooldown is over lands before the burn's first tick: the burn then ticks
    /// 7 times, the 7th a partial tick, smaller than the others.
    ///
    /// Checked in game by the user.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void A_Fire_Ball_Recast_At_The_Global_Cooldown_Gives_The_Burn_7_Ticks_With_A_Partial_Last(double haste)
    {
        var f = StepFights.Ardeos(haste: haste).WithoutDamageSpread();

        f.Cast<FireBall>();

        f.Cast<FireBall>(); // lands before the burn's first tick
        f.Wait(20); // the burn ticks out

        var ticks = f.Ticks(FireBallDot.EffectId);

        using var check = new Checks(f);
        check.Equal(7, ticks.Count);
        foreach (var tick in ticks.Take(6)) check.Ratio(1.0, tick.Value / ticks[0].Value, what: $"tick at {tick.Time.TotalSeconds:F3}s");
        check.True(ticks[6].Value < ticks[0].Value);
    }

    /// <summary>
    /// Wildfire during a Fire Ball's burn makes it tick 8 times, the last at about 35% of the others, whether Wildfire is cast right
    /// after the Fire Ball or a second later.
    ///
    /// The last tick is 0.35 of the first within 0.02. Checked in game by the user.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(0.05, 0.0)]
    [InlineData(0.05, 1.0)]
    [InlineData(0.1, 0.0)]
    [InlineData(0.1, 1.0)]
    [InlineData(0.2, 0.0)]
    [InlineData(0.2, 1.0)]
    [InlineData(0.3, 0.0)]
    [InlineData(0.3, 1.0)]
    public void Wildfire_Gives_The_Burn_8_Ticks_The_Last_About_35_Percent(double haste, double wildfireDelay)
    {
        var f = StepFights.Ardeos(haste: haste).WithoutDamageSpread();

        f.Cast<FireBall>();
        f.Wait(wildfireDelay); // Wildfire right after the Fire Ball, or a second later

        f.Cast<Wildfire>();
        f.Wait(20); // the burn ticks out

        var ticks = f.Ticks(FireBallDot.EffectId);

        using var check = new Checks(f);
        check.Equal(8, ticks.Count);
        foreach (var tick in ticks.Take(7)) check.Ratio(1.0, tick.Value / ticks[0].Value, what: $"tick at {tick.Time.TotalSeconds:F3}s");
        check.Equal(0.35, ticks[7].Value / ticks[0].Value, 0.02);
    }
}
