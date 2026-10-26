using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each Ardeos ability keeps him busy: from the start of the cast until he can act again.
/// Dev: every test casts at the start of a fight, so the fight clock after <c>Cast</c> returns is the down time; 1ms tolerance.
/// </summary>
public class ArdeosCastTimes
{
    private const double Tolerance = 0.001;

    private static void AssertDownTime(StepDriver f, double expected) =>
        Assert.InRange(f.Now.TotalSeconds, expected - Tolerance, expected + Tolerance);

    /// <summary>
    /// Player: Detonate always keeps you busy for 1 second, whatever your haste.
    /// Dev: one Detonate (an Ember and a Searing Blaze on the enemy) at 0%, 10% and 20% haste; the clock is 1.0.
    /// TODO: the engine has 0.967s (29/30), from 896 Detonates in combat logs at every haste; the cast time is known to be 1s in game, so this is
    /// expected to fail until the reason for the logs' 0.967s is understood.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Detonate_Takes_1_Second_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste);
        f.Cast<SearingBlaze>();
        double before = f.Now.TotalSeconds;
        f.Hero.Resources.Get(FellowshipSim.Core.Heroes.Ardeos.Ardeos.Cinders).Current = 100;

        f.Cast<Detonate>();

        Assert.InRange(f.Now.TotalSeconds - before, 1.0 - Tolerance, 1.0 + Tolerance);
    }

    /// <summary>
    /// Player: Incinerate takes a hasted 1.5 second cast, then a 2.5 second channel that haste does not shorten.
    /// Dev: Spirit Points set; one Incinerate at 0%, 10% and 20% haste; the cast completes at 1.5 / (1 + haste) and he is free at that plus 2.5.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Incinerate_Takes_A_Hasted_Cast_Then_A_2_5_Second_Channel(double haste)
    {
        var f = StepFights.Ardeos(haste);
        f.GiveSpiritAbility();

        f.Cast<Incinerate>();

        double castEnd = f.Events.Single(e => e.Kind == StepEventKind.CastCompleted && e.Name == "Incinerate").Time.TotalSeconds;
        Assert.InRange(castEnd, 1.5 / (1 + haste) - Tolerance, 1.5 / (1 + haste) + Tolerance);
        AssertDownTime(f, 1.5 / (1 + haste) + 2.5);
    }

    /// <summary>
    /// Player: Engulfing Flames takes a 1.5 second cast, shortened by haste.
    /// Dev: one Engulfing Flames at 0%, 10% and 20% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Engulfing_Flames_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Ardeos(haste);

        f.Cast<EngulfingFlames>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: Searing Blaze, Fire Ball and Fire Frogs are instant, so you are busy for the global cooldown: 1.5 seconds, shortened by haste.
    /// Dev: each cast once at 0% and 20% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Instant_Abilities_Take_The_Global_Cooldown(double haste)
    {
        var searing = StepFights.Ardeos(haste);
        searing.Cast<SearingBlaze>();
        AssertDownTime(searing, 1.5 / (1 + haste));

        var fireBall = StepFights.Ardeos(haste);
        fireBall.Cast<FireBall>();
        AssertDownTime(fireBall, 1.5 / (1 + haste));

        var frogs = StepFights.Ardeos(haste);
        frogs.Cast<FireFrogs>();
        AssertDownTime(frogs, 1.5 / (1 + haste));
    }
}
