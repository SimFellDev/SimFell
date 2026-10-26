using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// One Ardeos ability on its own: how Detonate and his damage over time effects scale with his stats, and which enemies Detonate hits.
/// Dev: damage is the expected value, so two casts' ratio is exact. The Python sim compares totals with fixed numbers (for example
/// Searing Blaze's 8166 over its duration); those are from an older patch (a 24 second Searing Blaze), so the ratios to a cast with
/// 1000 Intellect and no Expertise are tested instead: the scaling is the rule, not the base values.
/// </summary>
public class ArdeosAbilitiesSimple
{
    private const double Ratio = 0.01;

    // ---- Detonate

    private static List<StepEvent> DetonateHits(double haste, double expertise, double mainStat, double crit)
    {
        var f = StepFights.Ardeos(haste, crit: crit, mainStat: mainStat, expertise: expertise);
        f.Cast<SearingBlaze>();
        f.Hero.Resources.Get(Ardeos.Cinders).Current = 100;
        f.Cast<Detonate>();
        return f.Damage("Detonate");
    }

    /// <summary>
    /// Player: Detonate deals the average ticking damage of the effects on the enemy, in 3 equal hits, and it grows with your Haste (the
    /// effect ticks faster, so there is more damage per second), your Expertise and your Intellect: proportional to (1 + haste) x
    /// (1 + expertise) x Intellect / 1000.
    /// Dev: a Searing Blaze then a Detonate, against the same with no haste or Expertise and 1000 Intellect; 3 hits, each in that ratio. Crit is
    /// left out: the Python sim's rows with crit never crit (its dice are fixed), so they say nothing about crits.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, 1000.0)]
    [InlineData(0.1, 0.2, 1000.0)]
    [InlineData(0.2, 0.0, 1000.0)]
    [InlineData(0.0, 0.1, 1000.0)]
    [InlineData(0.2, 0.0, 2000.0)]
    public void Detonate_Scales_With_Haste_Expertise_And_Intellect(double haste, double expertise, double mainStat)
    {
        var reference = DetonateHits(0, 0, 1000, 0);

        var hits = DetonateHits(haste, expertise, mainStat, 0);

        Assert.Equal(3, hits.Count);
        Assert.All(hits, h => Assert.Equal((1 + expertise) * (1 + haste) * mainStat / 1000, h.Value / reference[0].Value, Ratio));
    }

    /// <summary>
    /// Player: Detonate only hurts enemies that have one of your damage over time effects: three hits on each, none on the others.
    /// Dev: 5 enemies; Searing Blaze on the second and third; Detonate cast at the first; 6 hits, 3 on each of the two.
    /// </summary>
    [Fact]
    public void Detonate_Only_Hits_Enemies_With_An_Effect()
    {
        var f = StepFights.Ardeos(enemies: 5);
        var enemies = f.Context.AllEnemies;
        f.Cast<SearingBlaze>(enemies[1]);
        f.Cast<SearingBlaze>(enemies[2]);
        f.Hero.Resources.Get(Ardeos.Cinders).Current = 100;

        f.Cast<Detonate>(enemies[0]);

        var hits = f.Damage("Detonate");
        Assert.Equal(6, hits.Count);
        Assert.Equal(3, hits.Count(h => h.Target == enemies[1].Name));
        Assert.Equal(3, hits.Count(h => h.Target == enemies[2].Name));
    }

    // ---- Damage over time effects

    private static double Total(StepDriver f, int effectId) => f.Ticks(effectId).Sum(t => t.Value);

    /// <summary>
    /// Player: a damage over time effect's total grows with your Intellect and Expertise: proportional to Intellect / 1000 x (1 + expertise).
    /// Dev: this base class casts the ability with 1000 Intellect and no Expertise, and with a set of stats, waits 60 seconds and compares the
    /// totals of the effect's ticks. A variant gives the ability and its effect.
    /// </summary>
    public abstract class DotScaling
    {
        protected abstract int EffectId { get; }
        protected abstract void Cast(StepDriver f);

        private double TotalFor(double expertise, double mainStat, double crit)
        {
            var f = StepFights.Ardeos(crit: crit, mainStat: mainStat, expertise: expertise);
            Cast(f);
            f.Wait(60);
            return Total(f, EffectId);
        }

        [Theory]
        [InlineData(0.0, 1000.0)]
        [InlineData(0.1, 1000.0)]
        [InlineData(0.2, 1000.0)]
        [InlineData(0.0, 2000.0)]
        [InlineData(0.0, 3000.0)]
        public void Total_Scales_With_Intellect_And_Expertise(double expertise, double mainStat)
        {
            double reference = TotalFor(0, 1000, 0);

            Assert.Equal((1 + expertise) * mainStat / 1000, TotalFor(expertise, mainStat, 0) / reference, Ratio);
        }
    }

    public class SearingBlazeScaling : DotScaling
    {
        protected override int EffectId => SearingBlazeDot.EffectId;
        protected override void Cast(StepDriver f) => f.Cast<SearingBlaze>();
    }

    public class EngulfingFlamesScaling : DotScaling
    {
        protected override int EffectId => EngulfingFlamesDot.EffectId;
        protected override void Cast(StepDriver f) => f.Cast<EngulfingFlames>();
    }

    public class FireBallScaling : DotScaling
    {
        protected override int EffectId => FireBallDot.EffectId;
        protected override void Cast(StepDriver f) => f.Cast<FireBall>();
    }
}
