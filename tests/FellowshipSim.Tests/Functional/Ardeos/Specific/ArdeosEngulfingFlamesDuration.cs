using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long an Engulfing Flames lasts: 9 seconds (12 with Undying Flame), and with Slow Burn each tick of a Fire Ball's burn on the enemy
/// adds half a second, so the order and timing of Fire Ball, Engulfing Flames and Wildfire matter.
///
/// Every duration is measured from the end of the Engulfing Flames cast, which applies it, and checked by the effect being on the enemy
/// just before its end and gone just after: 0.1s either side, 0.05s with Slow Burn. The expected durations come from the C++ sim's tests;
/// the user checked the Slow Burn ones in game.
/// </summary>
public class ArdeosEngulfingFlamesDuration
{
    // ---- Base duration

    /// <summary>An Engulfing Flames lasts 9 seconds, with or without a Fire Ball cast before it.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Engulfing_Flames_Lasts_9_Seconds(bool afterFireBall)
    {
        var f = StepFights.Ardeos();

        if (afterFireBall) f.Cast<FireBall>();

        f.Cast<EngulfingFlames>();
        double end = f.CompletedAt("Engulfing Flames") + 9.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Engulfing Flames"));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Engulfing Flames"));
    }

    // ---- Undying Flame

    /// <summary>With Undying Flame an Engulfing Flames lasts 12 seconds.</summary>
    [Fact]
    public void Undying_Flame_Makes_Engulfing_Flames_Last_12_Seconds()
    {
        var f = StepFights.Ardeos(talents: [UndyingFlame.TalentId]);

        f.Cast<EngulfingFlames>();
        double end = f.CompletedAt("Engulfing Flames") + 12.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Engulfing Flames"));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Engulfing Flames"));
    }

    // ---- Slow Burn (its cap on extensions, and a partial tick's extension, are in ArdeosTalents.cs)

    /// <summary>
    /// With Slow Burn, a Fire Ball cast before an Engulfing Flames gives it 5 extensions of half a second: 11.5 seconds.
    ///
    /// Per the user: every Fire Ball tick extends the Engulfing Flames on its target by 0.5s, with no total, whatever Slow Burn's tooltip
    /// ("up to a total of 3 seconds over its full duration") says.
    /// </summary>
    [Fact]
    public void With_Slow_Burn_A_Fire_Ball_Cast_Before_Engulfing_Flames_Makes_It_Last_11_5_Seconds()
    {
        var f = StepFights.Ardeos(talents: [SlowBurn.TalentId]);

        f.Cast<FireBall>();

        f.Cast<EngulfingFlames>();
        double end = f.CompletedAt("Engulfing Flames") + 11.5;

        f.WaitUntil(end - 0.05);
        Assert.True(f.HasDebuff("Engulfing Flames"));
        f.WaitUntil(end + 0.05);
        Assert.False(f.HasDebuff("Engulfing Flames"));
    }

    /// <summary>
    /// With Slow Burn, a Fire Ball cast after an Engulfing Flames gives it only 4 extensions, since the ball takes a second to land: 11
    /// seconds.
    ///
    /// The user checked this duration in game. The ball is cast a quarter of a second after the Engulfing Flames cast ends, so its 5th
    /// tick lands 0.22s after the extended end; cast at once, it would land 30ms before it and extend it again.
    /// </summary>
    [Fact]
    public void With_Slow_Burn_A_Fire_Ball_Cast_After_Engulfing_Flames_Makes_It_Last_11_Seconds()
    {
        var f = StepFights.Ardeos(talents: [SlowBurn.TalentId]);

        f.Cast<EngulfingFlames>();
        double end = f.CompletedAt("Engulfing Flames") + 11.0;
        f.WaitUntil(1.75); // clear of the end's edge
        f.Cast<FireBall>(); // lands about a second later

        f.WaitUntil(end - 0.05);
        Assert.True(f.HasDebuff("Engulfing Flames"));
        f.WaitUntil(end + 0.05);
        Assert.False(f.HasDebuff("Engulfing Flames"));
    }

    /// <summary>
    /// With Slow Burn, Wildfire makes the Fire Ball's burn tick faster, so more of its ticks fit: a Fire Ball, then an Engulfing Flames,
    /// then Wildfire gives 7 extensions: 12.5 seconds.
    ///
    /// The user checked this duration in game. The burn's first tick lands before the Engulfing Flames, which a quarter of a second's wait
    /// keeps clear of the edge; the 7 extensions are the 6 full ticks after it and the last, partial one, which adds the full half second
    /// (ArdeosTalents.cs).
    /// </summary>
    [Fact]
    public void Slow_Burn_And_Wildfire_Make_Engulfing_Flames_Last_12_5_Seconds()
    {
        var f = StepFights.Ardeos(talents: [SlowBurn.TalentId]);

        f.Cast<FireBall>();
        f.WaitUntil(1.75); // the burn's first tick clear of the Engulfing Flames' start

        f.Cast<EngulfingFlames>();
        double end = f.CompletedAt("Engulfing Flames") + 12.5;
        f.Cast<Wildfire>(); // as soon as the cast ends

        f.WaitUntil(end - 0.05);
        Assert.True(f.HasDebuff("Engulfing Flames"));
        f.WaitUntil(end + 0.05);
        Assert.False(f.HasDebuff("Engulfing Flames"));
    }
}
