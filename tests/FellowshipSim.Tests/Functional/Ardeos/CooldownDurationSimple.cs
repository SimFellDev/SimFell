using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's base cooldowns and charges, and how haste affects them (Fire Ball's charges recharge with haste).
/// Dev: a spent charge is timed by advancing the fight 10ms at a time until all charges are back, from the end of the cast; within 20ms.
/// </summary>
public class ArdeosCooldownDurationSimple
{
    private const double Tolerance = 0.02;

    private static double WaitUntilFull(StepDriver f, Ability ability)
    {
        var cooldown = f.Hero.CooldownOf(ability);
        while (cooldown.ChargesAt(f.Now) < cooldown.MaxCharges && f.Now.TotalSeconds < 300) f.Wait(0.01);
        return f.Now.TotalSeconds;
    }

    private static double CastEnd(StepDriver f, string ability) =>
        f.Events.Last(e => e.Kind == StepEventKind.CastCompleted && e.Name == ability).Time.TotalSeconds;

    /// <summary>
    /// Player: Engulfing Flames has 2 charges of 20 seconds, which haste does not shorten.
    /// Dev: at 0% and 20% haste; one cast; two charges, and a charge is back 20 seconds after the cast.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Engulfing_Flames_Has_2_Charges_Of_20_Seconds(double haste)
    {
        var f = StepFights.Ardeos(haste);
        var ability = f.Ability<EngulfingFlames>();
        Assert.Equal(2, f.Hero.CooldownOf(ability).MaxCharges);

        f.Cast<EngulfingFlames>();
        double castEnd = CastEnd(f, "Engulfing Flames");

        Assert.Equal(castEnd + 20, WaitUntilFull(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: Fire Ball has 2 charges of 30 seconds, and your haste speeds the recharge: 30 / (1 + haste) seconds.
    /// Dev: at 0% and 20% haste; one cast; the charge is back 30 / (1 + haste) seconds after it.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Fire_Ball_Has_2_Charges_Recharging_With_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste);
        var ability = f.Ability<FireBall>();
        Assert.Equal(2, f.Hero.CooldownOf(ability).MaxCharges);

        f.Cast<FireBall>();
        double castEnd = CastEnd(f, "Fire Ball");

        Assert.Equal(castEnd + 30 / (1 + haste), WaitUntilFull(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: Fire Frogs and Wildfire have a 45 second cooldown.
    /// Dev: one cast of each; ready 45 seconds after it.
    /// </summary>
    [Fact]
    public void Fire_Frogs_And_Wildfire_Have_45_Second_Cooldowns()
    {
        var frogs = StepFights.Ardeos();
        frogs.Cast<FireFrogs>();
        Assert.Equal(CastEnd(frogs, "Fire Frogs") + 45, WaitUntilFull(frogs, frogs.Ability<FireFrogs>()), Tolerance);

        var wildfire = StepFights.Ardeos();
        wildfire.Cast<Wildfire>();
        Assert.Equal(CastEnd(wildfire, "Wildfire") + 45, WaitUntilFull(wildfire, wildfire.Ability<Wildfire>()), Tolerance);
    }
}
