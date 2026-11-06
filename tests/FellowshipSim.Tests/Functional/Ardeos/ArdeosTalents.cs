using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's talents, on a bare Ardeos: each talent's own rule first, then how it works with other talents and abilities. How long an
/// Engulfing Flames lasts with Undying Flame and Slow Burn is in Specific/ArdeosEngulfingFlamesDuration.cs, and the highest a Searing Blaze
/// can be extended to after a recast in Specific/ArdeosSearingBlazeDuration.cs.
/// </summary>
public class ArdeosTalents
{
    // ---- Agonizing Blaze

    /// <summary>
    /// Casting Searing Blaze again and again still deals its ticks, with or without Agonizing Blaze: one every 2 seconds, shortened by
    /// haste, so floor(time / 2 x (1 + haste)) over the casts.
    ///
    /// A recast once moved the next tick back, so chain-casting dealt nothing. 59 casts in a row, 44.25 ticks' worth at any haste: with a
    /// multiple of 4 casts the last tick would land on the end of the last cast, an edge. With Agonizing Blaze each recast also adds a stack
    /// (up to 10), which this test does not read.
    /// </summary>
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(false, 0.1)]
    [InlineData(false, 0.2)]
    [InlineData(true, 0.0)]
    [InlineData(true, 0.1)]
    [InlineData(true, 0.2)]
    public void Spamming_Searing_Blaze_Ticks_Every_2_Hasted_Seconds_With_Or_Without_Agonizing_Blaze(bool agonizingBlaze, double haste)
    {
        var f = StepFights.Ardeos(haste: haste, talents: agonizingBlaze ? [AgonizingBlaze.TalentId] : null);

        for (int i = 0; i < 59; i++) f.Cast<SearingBlaze>();

        Assert.Equal((int)Math.Floor(f.Seconds() / 2 * (1 + haste)), f.Ticks(SearingBlazeDot.EffectId).Count);
    }

    // ---- Backdraft (its extensions after a Searing Blaze recast are in Specific/ArdeosSearingBlazeDuration.cs)

    /// <summary>
    /// With Backdraft, each Detonate extends your Searing Blaze by 1.5 seconds, but never past its full 30 seconds: three Detonates right
    /// after casting it leave it 30 seconds after the last one.
    ///
    /// Per the user: an extension never leaves an effect more time than its maximum duration (the Python sim's extend_duration).
    /// </summary>
    [Fact]
    public void Backdraft_Never_Extends_Searing_Blaze_Past_30_Seconds()
    {
        var f = StepFights.Ardeos(talents: [Backdraft.TalentId]);

        f.Cast<SearingBlaze>();

        for (int i = 0; i < 3; i++)
        {
            f.Set(Ardeos.Cinders, 100); // a Burning Ember for each
            f.Cast<Detonate>(); // 1.5 seconds added, about 1 second apart
        }
        double end = f.CompletedAt("Detonate") + 30.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Searing Blaze"));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Searing Blaze"));
    }

    // ---- Slow Burn (how long it makes an Engulfing Flames last is in Specific/ArdeosEngulfingFlamesDuration.cs)

    /// <summary>
    /// With Slow Burn, each Fire Ball tick extends the target's Searing Blaze or Engulfing Flames by 0.5 seconds, but never past its full
    /// duration: an effect applied a quarter of a second before a tick is back at its full duration after that tick, then gains 0.5 seconds
    /// from each of the burn's 4 later ticks.
    ///
    /// Per the user: an extension never leaves an effect more time than its maximum duration (the Python sim's extend_duration). The burn
    /// ticks every 2 seconds from the ball's landing (Specific/ArdeosFireBallDot.cs), so its later ticks are too far apart to reach the cap.
    /// </summary>
    [Theory]
    [InlineData("Searing Blaze", 30.0)]
    [InlineData("Engulfing Flames", 9.0)]
    public void Slow_Burn_Never_Extends_An_Effect_Past_Its_Full_Duration(string effect, double duration)
    {
        var f = StepFights.Ardeos(talents: [SlowBurn.TalentId]);

        f.Cast<FireBall>();
        f.Wait(1.0); // the ball lands
        double ballLanded = f.DirectHits<FireBall>().Single().Time.TotalSeconds;
        double cappingTick = ballLanded + 4.0; // the burn's second tick
        double castTime = effect == "Engulfing Flames" ? 1.5 : 0.0; // Searing Blaze is instant
        f.WaitUntil(cappingTick - 0.25 - castTime);

        f.Cast(effect); // lands 0.25s before that tick
        double end = cappingTick + duration + 0.5 * 4; // back to its full duration, then the 4 later ticks

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff(effect));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff(effect));
    }

    /// <summary>
    /// With Slow Burn, even a partial Fire Ball tick extends Searing Blaze by the full 0.5 seconds: a second Fire Ball landing before the
    /// burn's first tick gives the burn 6 full ticks and a partial one, and the Searing Blaze lasts 30 + 0.5 x 7 seconds.
    ///
    /// Per the user (Slow Burn's tooltip misleads). Why the burn ends on a partial tick is in Specific/ArdeosFireBallDot.cs. Checked 0.05s
    /// either side of the end, since a partial tick's share of the 0.5s would be a fraction of a second short.
    /// </summary>
    [Fact]
    public void Slow_Burn_Extends_By_The_Full_Half_Second_On_A_Partial_Fire_Ball_Tick()
    {
        var f = StepFights.Ardeos(talents: [SlowBurn.TalentId]).WithoutDamageSpread();

        f.Cast<SearingBlaze>();

        f.Cast<FireBall>();
        f.Cast<FireBall>(); // lands before the burn's first tick
        f.Wait(16); // the burn ticks out

        var burnTicks = f.Ticks(FireBallDot.EffectId);
        double end = f.CompletedAt("Searing Blaze") + 30.0 + 0.5 * burnTicks.Count;

        Assert.Equal(7, burnTicks.Count);
        Assert.True(burnTicks[^1].Value < burnTicks[0].Value); // the last tick is partial
        f.WaitUntil(end - 0.05);
        Assert.True(f.HasDebuff("Searing Blaze"));
        f.WaitUntil(end + 0.05);
        Assert.False(f.HasDebuff("Searing Blaze"));
    }

    // ---- Firestarter and Spontaneous Combustion

    /// <summary>
    /// Firestarter and Spontaneous Combustion make your damage over time effects crit more, but not Detonate: with Searing Blaze, Engulfing
    /// Flames and Fire Ball's burn on the enemy, Detonate's hits deal the same damage, with the same crit chance, with either talent, both
    /// or neither.
    ///
    /// Per the user. Detonate's hits are not crit and Spontaneous Combustion never empowers a tick, so the talent is the only difference.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Firestarter_And_Spontaneous_Combustion_Do_Not_Change_Detonate_Damage(bool firestarter, bool spontaneousCombustion)
    {
        List<StepEvent> DetonateHits(params int[] talents)
        {
            var f = StepFights.Ardeos(talents: talents).WithoutDamageSpread();
            f.ForceProcs(key => key is SpontaneousCombustion.SearingBlazeProcKey or SpontaneousCombustion.EngulfingFlamesProcKey
                ? false : null); // Spontaneous Combustion never empowers a tick

            f.Cast<SearingBlaze>();
            f.Cast<EngulfingFlames>();
            f.Cast<FireBall>();
            f.Wait(1.0); // the ball lands and sets the enemy on fire
            f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend

            f.Cast<Detonate>(crit: false);

            return f.Damage<Detonate>();
        }

        var talents = new List<int>();
        if (firestarter) talents.Add(Firestarter.TalentId);
        if (spontaneousCombustion) talents.Add(SpontaneousCombustion.TalentId);
        var plainHits = DetonateHits();
        var talentHits = DetonateHits([.. talents]);

        using var check = new Checks();
        check.Equal(3, talentHits.Count);
        for (int i = 0; i < Math.Min(plainHits.Count, talentHits.Count); i++)
        {
            check.Ratio(1.0, talentHits[i].Value / plainHits[i].Value, what: $"hit {i + 1}");
            check.Equal(plainHits[i].CritChance, talentHits[i].CritChance, what: $"crit chance of hit {i + 1}");
        }
    }

    // ---- Intensifying Inferno

    /// <summary>
    /// With Intensifying Inferno, Infernal Wave deals 15% more for each damage over time effect on the target, added up, and each Engulfing
    /// Flames counts on its own: 1 + 0.15 x N, so two Engulfing Flames and a Searing Blaze give 1.45.
    ///
    /// Per the user. The tooltip says "for each of your unique damage over time effects".
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void Intensifying_Inferno_Adds_15_Percent_Per_Effect_Counting_Each_Engulfing_Flames(int engulfingFlames, bool searingBlaze)
    {
        double WaveDamage(bool intensifyingInferno)
        {
            var f = StepFights.Ardeos(talents: intensifyingInferno ? [IntensifyingInferno.TalentId] : null).WithoutDamageSpread();

            if (searingBlaze) f.Cast<SearingBlaze>();
            for (int i = 0; i < engulfingFlames; i++) f.Cast<EngulfingFlames>();

            f.Cast<InfernalWave>();
            f.Wait(1.0); // the wave lands

            return f.Damage<InfernalWave>().Single().Value;
        }

        int effects = engulfingFlames + (searingBlaze ? 1 : 0);
        AssertRatio(1 + 0.15 * effects, WaveDamage(intensifyingInferno: true) / WaveDamage(intensifyingInferno: false));
    }
}
