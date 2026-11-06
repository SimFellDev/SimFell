using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's abilities, on a bare Ardeos (no talents unless the test is about one): each ability's own rule first, then how it works with
/// the others. Cinders and Burning Embers, cast times and cooldowns have their own files; how many ticks each damage over time effect deals
/// at each haste is in ArdeosHasteBreakpoints.cs, how fast they tick in Specific/ArdeosDotTickSpeed.cs, his legendaries in
/// Specific/ArdeosLegendaries.cs, and Spirit of Heroism, which every ultimate gives, in Generic/Ultimate.cs.
/// </summary>
public class ArdeosAbilities
{
    // ---- Damage over time effects (how many ticks each deals at each haste is in ArdeosHasteBreakpoints.cs)

    /// <summary>
    /// A damage over time effect's total damage grows with main stat and Expertise: main stat x (1 + expertise).
    ///
    /// Compared with the same effect at the base main stat (100) and no Expertise. The main stat of a row adds to the base 100.
    /// </summary>
    [Theory]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 0.0, 0.0)]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 0.1, 0.0)]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 0.2, 0.0)]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 0.0, 2000.0)]
    [InlineData("Searing Blaze", SearingBlazeDot.EffectId, 0.0, 3000.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 0.0, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 0.1, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 0.2, 0.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 0.0, 2000.0)]
    [InlineData("Engulfing Flames", EngulfingFlamesDot.EffectId, 0.0, 3000.0)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 0.0, 0.0)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 0.1, 0.0)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 0.2, 0.0)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 0.0, 2000.0)]
    [InlineData("Fire Ball", FireBallDot.EffectId, 0.0, 3000.0)]
    public void Damage_Over_Time_Scales_With_Main_Stat_And_Expertise(string ability, int effect, double expertise, double mainStat)
    {
        double TotalTicks(double expertise = 0, double mainStat = 0)
        {
            var f = StepFights.Ardeos(expertise: expertise, mainStat: mainStat).WithoutDamageSpread();

            f.Cast(ability);
            f.Wait(60); // the effect ticks out

            return f.Ticks(effect).Sum(tick => tick.Value);
        }

        double expected = (1 + expertise) * (StatBlock.BasePrimaryStat + mainStat) / StatBlock.BasePrimaryStat;
        AssertRatio(expected, TotalTicks(expertise, mainStat) / TotalTicks());
    }

    // ---- Searing Blaze (its Cinders are in ArdeosResourceEconomy.cs, how long a recast lasts in Specific/ArdeosSearingBlazeDuration.cs,
    // casting it again and again, with and without Agonizing Blaze, in ArdeosTalents.cs)

    /// <summary>
    /// Casting Searing Blaze again does not move its ticks: they stay on the rhythm of the first cast, every 2 seconds shortened by haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Recasting_Searing_Blaze_Leaves_Its_Tick_Rhythm_Alone(double haste)
    {
        var f = StepFights.Ardeos(haste: haste);

        f.Cast<SearingBlaze>();

        f.Cast<SearingBlaze>();
        f.Wait(12); // the ticks after the recast

        var ticks = f.Ticks(SearingBlazeDot.EffectId);
        double period = 2.0 / (1 + haste);

        using var check = new Checks(f);
        check.True(ticks.Count > 0);
        foreach (var tick in ticks)
        {
            double onRhythm = Math.Round(tick.Time.TotalSeconds / period) * period;
            check.Equal(onRhythm, tick.Time.TotalSeconds, 0.05, what: $"tick at {tick.Time.TotalSeconds:F3}s");
        }
    }

    // ---- Engulfing Flames (how long it lasts is in Specific/ArdeosEngulfingFlamesDuration.cs, its 6 ticks at each haste in
    // ArdeosHasteBreakpoints.cs)

    /// <summary>Each Engulfing Flames is its own effect: two casts give two effects, each with its 6 ticks.</summary>
    [Fact]
    public void Two_Engulfing_Flames_Tick_6_Times_Each()
    {
        var f = StepFights.Ardeos();

        f.Cast<EngulfingFlames>();
        f.Cast<EngulfingFlames>(); // its second charge
        f.Wait(20); // both effects tick out

        Assert.Equal(12, f.Ticks(EngulfingFlamesDot.EffectId).Count);
    }

    // ---- Detonate (its Burning Ember is in ArdeosResourceEconomy.cs, its fixed cast time in ArdeosCastTimes.cs)

    /// <summary>
    /// Detonate deals the ticking damage of the effects on the enemy in 3 equal hits, so it grows with what makes them tick harder or
    /// faster: (1 + haste) x (1 + expertise) x main stat.
    ///
    /// Compared with the same Detonate at the base main stat (100), no haste and no Expertise. The main stat of a row adds to the base
    /// 100.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(0.1, 0.2, 0.0)]
    [InlineData(0.2, 0.0, 0.0)]
    [InlineData(0.0, 0.1, 0.0)]
    [InlineData(0.2, 0.0, 2000.0)]
    public void Detonate_Scales_With_Haste_Expertise_And_Main_Stat(double haste, double expertise, double mainStat)
    {
        List<StepEvent> DetonateHits(double haste = 0, double expertise = 0, double mainStat = 0)
        {
            var f = StepFights.Ardeos(haste: haste, expertise: expertise, mainStat: mainStat).WithoutDamageSpread();

            f.Cast<SearingBlaze>();
            f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend

            f.Cast<Detonate>();

            return f.Damage<Detonate>();
        }

        double plainHit = DetonateHits()[0].Value;
        var statsHits = DetonateHits(haste, expertise, mainStat);
        double expected = (1 + haste) * (1 + expertise) * (StatBlock.BasePrimaryStat + mainStat) / StatBlock.BasePrimaryStat;

        using var check = new Checks();
        check.Equal(3, statsHits.Count);
        foreach (var hit in statsHits) check.Ratio(expected, hit.Value / plainHit, what: $"hit at {hit.Time.TotalSeconds:F3}s");
    }

    /// <summary>
    /// Detonate only hurts enemies that have one of your damage over time effects: three hits on each, none on the others.
    /// </summary>
    [Fact]
    public void Detonate_Hits_Each_Enemy_With_An_Effect_3_Times_And_No_Other()
    {
        var f = StepFights.Ardeos(enemies: 5);

        f.Cast<SearingBlaze>(target: f.Enemies()[1]);
        f.Cast<SearingBlaze>(target: f.Enemies()[2]);
        f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend

        f.Cast<Detonate>(target: f.Enemies()[0]); // the target has no effect

        using var check = new Checks(f);
        check.Equal(6, f.Damage<Detonate>().Count);
        check.Equal(3, f.Damage<Detonate>().Count(hit => hit.Target == f.Enemies()[1].Name));
        check.Equal(3, f.Damage<Detonate>().Count(hit => hit.Target == f.Enemies()[2].Name));
    }

    // ---- Wildfire (its faster ticks are in Specific/ArdeosDotTickSpeed.cs, its extra Cinders in ArdeosResourceEconomy.cs)

    /// <summary>
    /// Wildfire makes your effects tick 30% faster, so Detonate, which deals a number of seconds of their damage, deals 30% more.
    /// </summary>
    [Fact]
    public void Wildfire_Raises_Detonate_Damage_By_30_Percent()
    {
        List<StepEvent> DetonateHits(bool wildfire)
        {
            var f = StepFights.Ardeos().WithoutDamageSpread();

            f.Cast<SearingBlaze>();
            f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend
            if (wildfire) f.Cast<Wildfire>();

            f.Cast<Detonate>();

            return f.Damage<Detonate>();
        }

        double plainHit = DetonateHits(wildfire: false)[0].Value;
        var wildfireHits = DetonateHits(wildfire: true);

        using var check = new Checks();
        check.Equal(3, wildfireHits.Count);
        foreach (var hit in wildfireHits) check.Ratio(1.3, hit.Value / plainHit, what: $"hit at {hit.Time.TotalSeconds:F3}s");
    }

    // ---- Incinerate (its cast time is in ArdeosCastTimes.cs, its hits at each haste in ArdeosHasteBreakpoints.cs, its extensions after a
    // Searing Blaze recast in Specific/ArdeosSearingBlazeDuration.cs)

    /// <summary>
    /// Each Incinerate hit extends your other effects on the enemy by 1.5 seconds, but never past their full duration: Searing Blaze (30
    /// seconds), Engulfing Flames (9) or Fire Ball's burn (12) cast just before Incinerate ends that duration after its last hit.
    ///
    /// Per the user, and combat logs (Incinerate.cs): an extension never leaves an effect more time than its maximum duration. The 8 hits add
    /// 12 seconds over a 1.5 second cast and a 2.5 second channel.
    /// </summary>
    [Theory]
    [InlineData("Searing Blaze", 30.0)]
    [InlineData("Engulfing Flames", 9.0)]
    [InlineData("Fire Ball", 12.0)]
    public void Incinerate_Never_Extends_An_Effect_Past_Its_Full_Duration(string effect, double duration)
    {
        var f = StepFights.Ardeos();
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast(effect);

        f.Cast<Incinerate>();
        double lastHit = f.DirectHits<Incinerate>().Last().Time.TotalSeconds;
        double end = lastHit + duration;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff(effect));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff(effect));
    }

    /// <summary>
    /// Every hit of Incinerate's channel deals the same damage, the last partial one included: a partial hit is a full hit.
    ///
    /// Per the user, as the Python sim has it (a partial hit deals a full hit's damage). At these hastes, with Spirit of
    /// Heroism, the channel ends half a tick after its last full one (ArdeosHasteBreakpoints.cs).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Incinerates_Partial_Last_Hit_Deals_Full_Damage(double haste)
    {
        var f = StepFights.Ardeos(haste: haste).WithoutDamageSpread();
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<Incinerate>();

        var hits = f.DirectHits<Incinerate>();

        using var check = new Checks(f);
        foreach (var hit in hits) check.Ratio(1.0, hit.Value / hits[0].Value, what: $"hit at {hit.Time.TotalSeconds:F3}s");
    }
}
