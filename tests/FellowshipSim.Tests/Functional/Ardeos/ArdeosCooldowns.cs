using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long Ardeos's cooldowns last, and how haste affects them.
///
/// Every duration is checked by the ability being not ready (or short of a charge) 0.1s before its end and ready 0.1s after.
/// </summary>
public class ArdeosCooldowns
{
    // ---- Base cooldowns

    /// <summary>Fire Frogs and Wildfire have a 45 second cooldown, counted from the cast.</summary>
    [Theory]
    [InlineData("Fire Frogs")]
    [InlineData("Wildfire")]
    public void Fire_Frogs_And_Wildfire_Have_A_45_Second_Cooldown(string ability)
    {
        var f = StepFights.Ardeos();

        f.Cast(ability);
        double end = f.CompletedAt(ability) + 45.0;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }

    // ---- Engulfing Flames' charges

    /// <summary>Engulfing Flames has 2 charges of 20 seconds, which haste does not shorten.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Engulfing_Flames_Has_2_Charges_Of_20_Seconds_Whatever_Your_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);
        Assert.Equal(2, f.Charges<EngulfingFlames>());

        f.Cast<EngulfingFlames>();
        double end = f.CompletedAt("Engulfing Flames") + 20.0;

        f.WaitUntil(end - 0.1);
        Assert.Equal(1, f.Charges<EngulfingFlames>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(2, f.Charges<EngulfingFlames>());
    }

    // ---- Fire Ball's charges

    /// <summary>
    /// Fire Ball has 2 charges of 30 seconds, and haste speeds the recharge: a charge comes back in 30 / (1 + haste) seconds.
    ///
    /// Haste is an acceleration: the charge keeps its 30 seconds and recharges 1 + haste times as fast (recovery rate 1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Fire_Ball_Has_2_Charges_Recharging_In_30_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);
        Assert.Equal(2, f.Charges<FireBall>());

        f.Cast<FireBall>();
        double end = f.CompletedAt("Fire Ball") + 30.0 / (1 + haste);

        Assert.Equal(1 + haste, f.AccelerationRate<FireBall>());
        f.WaitUntil(end - 0.1);
        Assert.Equal(1, f.Charges<FireBall>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(2, f.Charges<FireBall>());
    }
}
