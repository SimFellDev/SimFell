using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Fire Ball's burn: the damage over time a Fire Ball sets enemies on fire with (20% of the hit over 12 seconds, a tick every 2 seconds at
/// any haste), when the ball lands, and how recasting and Wildfire change its ticks.
/// Dev: the ticks of the burn are read off the fight's events. The specs are the C++ tests' ("game-accurate expectation", not derived from the engine).
/// </summary>
public class ArdeosFireBallDot
{
    private const double Tolerance = 0.001;

    private static List<double> Ticks(StepDriver f) => f.Ticks(FireBallDot.EffectId).Select(t => t.Value).ToList();

    /// <summary>
    /// Player: a Fire Ball lands about 1 second after you cast it.
    /// Dev: one Fire Ball; its direct hit comes 1.0 seconds after the cast, within 50ms.
    /// </summary>
    [Fact]
    public void A_Fire_Ball_Lands_1_Second_After_The_Cast()
    {
        var f = StepFights.Ardeos();

        f.Cast<FireBall>();
        f.Wait(2);

        var hit = Assert.Single(f.Hits("Fire Ball"));
        Assert.InRange(hit.Time.TotalSeconds, 0.95, 1.05);
    }

    /// <summary>
    /// Player: Fire Ball's burn ticks 6 times, all for the same damage, whatever your haste.
    /// Dev: one Fire Ball at 0%, 5%, 10%, 20% and 30% haste; 6 ticks, equal.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void The_Burn_Ticks_6_Times_With_Equal_Damage_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<FireBall>();
        f.Wait(20);

        var ticks = Ticks(f);
        Assert.Equal(6, ticks.Count);
        Assert.All(ticks, t => Assert.True(t > 0));
        Assert.All(ticks, t => Assert.Equal(ticks[0], t, Tolerance));
    }

    /// <summary>
    /// Player: a second Fire Ball cast as soon as the first one's global cooldown is over lands before the burn's first tick: the burn then
    /// ticks 7 times, the 7th a partial tick (smaller than the others).
    /// Dev: two Fire Balls back to back at 0%, 5%, 10%, 20% and 30% haste; 7 ticks, the first 6 equal and the last smaller.
    /// TODO: the C++ test is disabled in its project for the engine giving 7 equal ticks; it is the game's expectation. Left active here.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void A_Fire_Ball_Recast_At_The_Global_Cooldown_Gives_7_Ticks_With_A_Partial_Last(double haste)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<FireBall>();
        f.Cast<FireBall>();
        f.Wait(20);

        var ticks = Ticks(f);
        Assert.Equal(7, ticks.Count);
        for (int i = 0; i < 6; i++) Assert.Equal(ticks[0], ticks[i], Tolerance);
        Assert.True(ticks[6] < ticks[0]);
    }

    /// <summary>
    /// Player: Wildfire during a Fire Ball's burn makes it tick 8 times, the last at about 35% of the others, whenever Wildfire is cast.
    /// Dev: one Fire Ball, Wildfire cast right away or 1 second after the global cooldown, at 0%, 5%, 10%, 20% and 30% haste; 8 ticks, the first 7
    /// equal and the last 0.35 of them within 2 points.
    /// TODO: the C++ test is disabled in its project for the engine's count depending on the timing of Wildfire; it is the game's expectation.
    /// Left active here.
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
    public void Wildfire_Gives_The_Burn_8_Ticks_The_Last_About_A_Third(double haste, double wildfireDelay)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<FireBall>();
        f.Wait(wildfireDelay);
        f.Cast<Wildfire>();
        f.Wait(20);

        var ticks = Ticks(f);
        Assert.Equal(8, ticks.Count);
        for (int i = 0; i < 7; i++) Assert.Equal(ticks[0], ticks[i], Tolerance);
        Assert.Equal(ticks[0] * 0.35, ticks[7], ticks[0] * 0.02);
    }
}
