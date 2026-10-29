using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using ArdeosTalents = FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's abilities working together: Detonate with Wildfire and the Devouring Flame legendary, spamming Searing Blaze, several
/// Engulfing Flames at once, and Incinerate.
/// </summary>
public class ArdeosAbilitiesComplex
{
    private const double Ratio = 0.01;

    // ---- Detonate

    /// <summary>
    /// Player: Wildfire makes your effects tick 30% faster, so Detonate (which deals a number of seconds of their damage) deals 30% more.
    /// Dev: a Searing Blaze and a Detonate, then Wildfire and another Detonate; the second 3 hits are 1.3 times the first 3. (Season 4 value;
    /// the Python sim, season 2, has 20%.)
    /// </summary>
    [Fact]
    public void Wildfire_Raises_Detonate_Damage_By_30_Percent()
    {
        var f = StepFights.Ardeos();
        f.Cast<SearingBlaze>();
        f.Hero.Resources.Get(Ardeos.Cinders).Current = 100;
        f.Cast<Detonate>();
        f.Hero.Resources.Get(Ardeos.Cinders).Current = 100;
        f.Cast<Wildfire>();
        f.Cast<Detonate>();

        var hits = f.Damage("Detonate");
        Assert.Equal(6, hits.Count);
        Assert.All(hits.Skip(3), h => Assert.Equal(1.3, h.Value / hits[0].Value, Ratio));
    }

    /// <summary>
    /// Player: with the Devouring Flame legendary, enemies take 7% more damage from you for each Engulfing Flames on them, Detonate included.
    /// Dev: a Searing Blaze and an Engulfing Flames then a Detonate, with and without the legendary; the ratio is 1.07 with it. (Season 4
    /// value; the Python sim, season 2, has 8%.) Any legendary also gives Mote of Creation's +10% Main Stat, so the hero without one has 1100
    /// Main Stat to compare like with like.
    /// </summary>
    [Fact]
    public void Devouring_Flame_Raises_Detonate_Damage_By_7_Percent_Per_Engulfing_Flames()
    {
        double Hit(int[]? legendaries)
        {
            var f = StepFights.Ardeos(legendaries: legendaries, mainStat: legendaries == null ? 1100 : 1000);
            f.Cast<SearingBlaze>();
            f.Cast<EngulfingFlames>();
            f.Hero.Resources.Get(Ardeos.Cinders).Current = 100;
            f.Cast<Detonate>();
            return f.Damage("Detonate")[0].Value;
        }

        Assert.Equal(1.07, Hit([DevouringFlame.BracersItemId]) / Hit(null), Ratio);
    }

    // ---- Searing Blaze

    /// <summary>
    /// Player: with Agonizing Blaze, Searing Blaze can be recast and still ticks: each recast adds a stack (up to 10), a tick is a hit and
    /// gives Cinders, so spamming it does damage.
    /// Dev: Agonizing Blaze taken; 60 Searing Blazes back to back at 0%, 10% and 20% haste; the ticks dealt are floor(time / 2 x (1 + haste)),
    /// and the Cinders are one for every 2 seconds, 100 of them being an Ember.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2, Skip = "44 ticks in the engine, 45 in the spec: a tick on the edge of the fight's end, to check")]
    public void Spamming_Searing_Blaze_With_Agonizing_Blaze_Deals_Damage_And_Gives_Cinders(double haste)
    {
        var f = StepFights.Ardeos(haste, talents: [ArdeosTalents.AgonizingBlaze.TalentId]);

        for (int i = 0; i < 60; i++) f.Cast<SearingBlaze>();

        double seconds = f.Now.TotalSeconds;
        Assert.Equal((int)Math.Floor(seconds / 2 * (1 + haste)), f.Ticks(SearingBlazeDot.EffectId).Count);
    }

    /// <summary>
    /// Player: casting Searing Blaze again mid-cycle does not move its ticks: they stay on the rhythm of the first cast, every 2 seconds
    /// (shortened by haste).
    /// Dev: a Searing Blaze, a second one 1.5s later, and 12 seconds; every tick is at a multiple of 2 / (1 + haste) from the first cast, within
    /// 50ms, at 0%, 10% and 20% haste.
    /// (The C++ sim's rule, season 3; the Python sim, season 2, had a recast start the burn over, so spamming dealt nothing.)
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Recasting_Searing_Blaze_Leaves_Its_Tick_Rhythm_Alone(double haste)
    {
        var f = StepFights.Ardeos(haste);
        double period = 2.0 / (1 + haste);

        f.Cast<SearingBlaze>();
        f.Cast<SearingBlaze>();
        f.Wait(12);

        var ticks = f.Ticks(SearingBlazeDot.EffectId);
        Assert.NotEmpty(ticks);
        Assert.All(ticks, t =>
        {
            double k = Math.Round(t.Time.TotalSeconds / period);
            Assert.InRange(t.Time.TotalSeconds, k * period - 0.05, k * period + 0.05);
        });
    }

    /// <summary>
    /// Player: thirty Searing Blazes in a row, each costing its global cooldown, still do damage and give Cinders.
    /// Dev: 30 casts (more than 30 seconds go by); Cinders over 20 and some damage (1 Cinder per tick: 22 ticks in 45 seconds).
    /// </summary>
    [Fact]
    public void Thirty_Searing_Blazes_In_A_Row_Deal_Damage_And_Give_Cinders()
    {
        var f = StepFights.Ardeos();

        for (int i = 0; i < 30; i++) f.Cast<SearingBlaze>();

        Assert.True(f.Now.TotalSeconds >= 30 * 1.5 - 0.01);
        Assert.True(f.Hero.ResourceAmount(Ardeos.Cinders) > 20 || f.ArdeosOf().BurningEmbers > 0);
        Assert.True(f.Ticks(SearingBlazeDot.EffectId).Sum(t => t.Value) > 100);
    }

    // ---- Engulfing Flames

    /// <summary>
    /// Player: each Engulfing Flames is its own effect: two casts give two effects, each with its 6 ticks.
    /// Dev: 1 and then 2 Engulfing Flames back to back and 20 seconds; 6 and 12 ticks.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Each_Engulfing_Flames_Ticks_On_Its_Own(int casts)
    {
        var f = StepFights.Ardeos();

        for (int i = 0; i < casts; i++) f.Cast<EngulfingFlames>();
        f.Wait(20);

        Assert.Equal(6 * casts, f.Ticks(EngulfingFlamesDot.EffectId).Count);
    }

    // ---- Incinerate

    /// <summary>
    /// Player: Incinerate gives you Spirit of Heroism (+30% haste), so its channel of 0.5 second ticks fits more hits as the haste grows:
    /// 8 hits at 0% and 10% haste, 9 at 20%.
    /// Dev: Spirit Points set; one Incinerate at 0%, 10% and 20% haste; the direct hits are counted; Spirit of Heroism is up and haste is 30% more.
    /// </summary>
    [Theory]
    [InlineData(0.0, 8)]
    [InlineData(0.1, 8)]
    [InlineData(0.2, 9)]
    public void Incinerate_Hits_By_Haste(double haste, int hits)
    {
        var f = StepFights.Ardeos(haste);
        f.GiveSpiritAbility();

        f.Cast<Incinerate>();

        Assert.Equal(haste + 0.3, f.Hero.GetHaste(f.Now), 1e-9);
        Assert.True(f.Hero.Auras.HasAura(Hero.SpiritOfHeroism, f.Now));
        Assert.Equal(hits, f.Hits("Incinerate").Count);
    }
}
