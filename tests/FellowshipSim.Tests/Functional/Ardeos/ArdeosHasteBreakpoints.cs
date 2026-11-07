using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How many ticks each Ardeos damage over time effect deals over its whole duration, and how many hits Incinerate's channel deals, at each
/// haste. Haste makes them tick faster without making them last longer, so the number of ticks goes up in steps (breakpoints) rather than
/// smoothly; the last tick is a partial one when the time left is less than a tick. Every test casts once on a single enemy and counts the
/// ticks or hits. How fast each effect ticks is in Specific/ArdeosDotTickSpeed.cs, and Fire Ball's burn, 6 ticks at any haste, in
/// Specific/ArdeosFireBallDot.cs.
/// </summary>
public class ArdeosHasteBreakpoints
{
    // ---- Searing Blaze

    /// <summary>
    /// Searing Blaze ticks every 2 seconds, faster with haste, for 30 seconds: once per full tick that fits, floor(30 / 2 x (1 + haste)),
    /// and once more when at least a server frame (1/30 second) is left over.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Searing_Blaze_Ticks_Every_2_Hasted_Seconds_For_30_Seconds(double haste)
    {
        double tick = 2.0 / (1 + haste);
        // the full ticks, then a partial one
        int ticks = FullTicks(30.0, tick) + PartialTick(30.0, tick, PeriodicEffect.ServerFrameSeconds);
        var f = StepFights.Ardeos(haste: haste);

        f.Cast<SearingBlaze>();
        f.Wait(60); // the effect ticks out

        Assert.Equal(ticks, f.Ticks(SearingBlazeDot.EffectId).Count);
    }

    // ---- Engulfing Flames

    /// <summary>
    /// Engulfing Flames ticks every 1.5 seconds, faster with haste, for 9 seconds: once per full tick that fits, floor(9 / 1.5 x
    /// (1 + haste)), and once more when at least a server frame (1/30 second) is left over.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Engulfing_Flames_Ticks_Every_1_5_Hasted_Seconds_For_9_Seconds(double haste)
    {
        double tick = 1.5 / (1 + haste);
        // the full ticks, then a partial one
        int ticks = FullTicks(9.0, tick) + PartialTick(9.0, tick, PeriodicEffect.ServerFrameSeconds);
        var f = StepFights.Ardeos(haste: haste);

        f.Cast<EngulfingFlames>();
        f.Wait(60); // the effect ticks out

        Assert.Equal(ticks, f.Ticks(EngulfingFlamesDot.EffectId).Count);
    }

    // ---- Incinerate (Spirit of Heroism, which every ultimate gives, is in Generic/Ultimate.cs)

    /// <summary>
    /// Incinerate's burn ticks every 3 seconds, faster with haste, and Incinerate gives Spirit of Heroism (30% haste), which hastes it
    /// too: 7 ticks at 0% and 10% haste, 8 at 20%.
    /// </summary>
    [Theory]
    [InlineData(0.0, 7)]
    [InlineData(0.1, 7)]
    [InlineData(0.2, 8)]
    public void Incinerates_Burn_Ticks_Every_3_Seconds_Hasted_By_Spirit_Of_Heroism(double haste, int ticks)
    {
        var f = StepFights.Ardeos(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<Incinerate>();
        f.Wait(60); // the effect ticks out

        Assert.Equal(ticks, f.Ticks(IncinerateDot.EffectId).Count);
    }

    /// <summary>
    /// Incinerate's channel lasts 2.5 seconds whatever your haste and hits every 0.5 seconds, faster with haste: once as it starts, once
    /// per full tick that fits, floor(2.5 / 0.5 x (1 + haste)), and once more when at least a server frame (1/30 second) is left over.
    /// The haste is the channel's, Spirit of Heroism's 30% included, which Incinerate gives as it is cast.
    ///
    /// The server frame is the engine's (PeriodicEffect.ServerFrameSeconds), which the user trusts over the Python sim's 5% of a tick. The
    /// rows include hastes that leave no partial tick (10%, 30%, 50%) and less than a frame of one (10.5%, 30.2%, 31.5%).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(0.1)]
    [InlineData(0.105)]
    [InlineData(0.15)]
    [InlineData(0.2)]
    [InlineData(0.25)]
    [InlineData(0.3)]
    [InlineData(0.302)]
    [InlineData(0.315)]
    [InlineData(0.4)]
    [InlineData(0.5)]
    public void Incinerate_Hits_On_Start_Then_Every_Hasted_Half_Second_Plus_A_Partial_Of_At_Least_A_Server_Frame(double haste)
    {
        double channelHaste = haste + 0.3; // Spirit of Heroism
        double tick = 0.5 / (1 + channelHaste);
        // the hit on start, the full ticks, then a partial one
        int hits = 1 + FullTicks(2.5, tick) + PartialTick(2.5, tick, PeriodicEffect.ServerFrameSeconds);
        var f = StepFights.Ardeos(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<Incinerate>();

        Assert.Equal(hits, f.DirectHits<Incinerate>().Count);
    }
}
