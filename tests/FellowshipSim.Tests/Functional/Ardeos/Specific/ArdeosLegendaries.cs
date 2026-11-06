using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's legendary items, each worn on a bare Ardeos: Devouring Flame (his bracers). Any legendary also gives Mote of Creation's +10%
/// main stat, so a hero without one gets 10 more main stat on top of the base 100, to compare like with like. Explosivo's burn and its
/// effect on Detonate's cast time are in Specific/ArdeosDotTickSpeed.cs and ArdeosCastTimes.cs.
/// </summary>
public class ArdeosLegendaries
{
    // ---- Devouring Flame

    /// <summary>
    /// With the Devouring Flame legendary, enemies take 7% more damage from you for each Engulfing Flames on them, Detonate included.
    /// </summary>
    [Fact]
    public void Devouring_Flame_Raises_Detonate_Damage_By_7_Percent_Per_Engulfing_Flames()
    {
        double FirstDetonateHit(bool devouringFlame)
        {
            var f = StepFights.Ardeos(
                legendaries: devouringFlame ? [DevouringFlame.BracersItemId] : null,
                mainStat: devouringFlame ? 0 : 10).WithoutDamageSpread(); // Mote of Creation's 10% of the base 100

            f.Cast<SearingBlaze>();
            f.Cast<EngulfingFlames>();
            f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend

            f.Cast<Detonate>();

            return f.Damage<Detonate>()[0].Value;
        }

        AssertRatio(1.07, FirstDetonateHit(devouringFlame: true) / FirstDetonateHit(devouringFlame: false));
    }

    /// <summary>
    /// With the Devouring Flame legendary and K Engulfing Flames on the enemy, Detonate deals base x K x 1.07^K, base being Detonate with one
    /// Engulfing Flames and no legendary: the legendary counts once, not once in the effects' damage and again on Detonate's hits.
    ///
    /// Per the user: the bonus multiplies, 1.07 per Engulfing Flames, and a double count has been a bug in the game.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Devouring_Flame_Counts_Once_On_Detonate_As_1_07_Per_Engulfing_Flames(int engulfingFlames)
    {
        double FirstDetonateHit(bool devouringFlame, int engulfingFlames)
        {
            var f = StepFights.Ardeos(
                legendaries: devouringFlame ? [DevouringFlame.BracersItemId] : null,
                mainStat: devouringFlame ? 0 : 10).WithoutDamageSpread(); // Mote of Creation's 10% of the base 100

            for (int i = 0; i < engulfingFlames; i++)
            {
                f.ResetCooldown<EngulfingFlames>(); // a charge for each, as Rekindled Flame would give
                f.Cast<EngulfingFlames>();
            }
            f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend

            f.Cast<Detonate>();

            return f.Damage<Detonate>()[0].Value;
        }

        double baseHit = FirstDetonateHit(devouringFlame: false, engulfingFlames: 1);
        double legendaryHit = FirstDetonateHit(devouringFlame: true, engulfingFlames);

        AssertRatio(engulfingFlames * Math.Pow(1.07, engulfingFlames), legendaryHit / baseHit);
    }
}
