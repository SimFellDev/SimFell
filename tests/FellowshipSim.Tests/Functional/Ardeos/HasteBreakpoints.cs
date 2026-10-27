using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many ticks each Ardeos damage over time effect deals over its whole duration, at each haste. Haste speeds ticks up, so the number
/// goes up in steps; the last tick of an application is a partial one when the time left is less than a tick.
/// Dev: one cast on one enemy and a 60 second wait, counting the effect's ticks. Incinerate gives Spirit of Heroism (+30% haste).
/// </summary>
public class ArdeosHasteBreakpoints
{
    /// <summary>
    /// Player: Engulfing Flames ticks every 1.5 seconds, shortened by haste, for 9 seconds: 6 ticks at no haste, 7 at 10%, 8 at 20% (the last
    /// being a partial tick at 10% and 20%).
    /// Dev: one Engulfing Flames at each haste; counts its ticks.
    /// </summary>
    [Theory]
    [InlineData(0.0, 6)]
    [InlineData(0.1, 7)]
    [InlineData(0.2, 8)]
    public void Engulfing_Flames_Ticks(double haste, int ticks)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<EngulfingFlames>();
        f.Wait(60);

        Assert.Equal(ticks, f.Ticks(EngulfingFlamesDot.EffectId).Count);
    }

    /// <summary>
    /// Player: Searing Blaze ticks every 2 seconds, shortened by haste, for 30 seconds: 15 ticks at no haste, 17 at 10% (the last a partial
    /// tick), 18 at 20%.
    /// Dev: one Searing Blaze at each haste; counts its ticks. (Season 4's 30 seconds; the Python sim, season 2, had 24.)
    /// </summary>
    [Theory]
    [InlineData(0.0, 15)]
    [InlineData(0.1, 17)]
    [InlineData(0.2, 18)]
    public void Searing_Blaze_Ticks(double haste, int ticks)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<SearingBlaze>();
        f.Wait(60);

        Assert.Equal(ticks, f.Ticks(SearingBlazeDot.EffectId).Count);
    }

    /// <summary>
    /// Player: Incinerate's burn ticks every 3 seconds, and Incinerate's Spirit of Heroism hastes it: 7 ticks at 0% and 10% haste, 8 at 20%.
    /// Dev: Spirit Points set; one Incinerate; counts the ticks of its burn.
    /// </summary>
    [Theory]
    [InlineData(0.0, 7)]
    [InlineData(0.1, 7)]
    [InlineData(0.2, 8)]
    public void Incinerate_Burn_Ticks(double haste, int ticks)
    {
        var f = StepFights.Ardeos(haste);
        f.GiveSpiritAbility();

        f.Cast<Incinerate>();
        f.Wait(60);

        Assert.Equal(ticks, f.Ticks(IncinerateDot.EffectId).Count);
    }

    /// <summary>
    /// Player: Fire Ball's burn ticks every 2 seconds whatever your haste, for 12 seconds: always 6 ticks (unhasted since season 4; it was hasted before).
    /// Dev: one Fire Ball at 0%, 10%, 20%; 6 ticks.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Fire_Ball_Burn_Ticks_6_Times_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<FireBall>();
        f.Wait(60);

        Assert.Equal(6, f.Ticks(FireBallDot.EffectId).Count);
    }
}
