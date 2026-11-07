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

    /// <summary>Detonate's first hit, with <paramref name="engulfingFlames"/> Engulfing Flames on the enemy and nothing else.</summary>
    private static double FirstDetonateHit(bool devouringFlame, int engulfingFlames)
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

    /// <summary>
    /// Devouring Flame counts once on Detonate: with one Engulfing Flames, Detonate deals 7% more, not 7% on the effect's damage and 7%
    /// again on Detonate's hits (1.07 x 1.07).
    ///
    /// Per the user: a double count has been a bug in the game.
    /// </summary>
    [Fact]
    public void Devouring_Flame_Counts_Once_On_Detonate()
    {
        double plainHit = FirstDetonateHit(devouringFlame: false, engulfingFlames: 1);
        double legendaryHit = FirstDetonateHit(devouringFlame: true, engulfingFlames: 1);

        AssertRatio(1.07, legendaryHit / plainHit);
    }

    /// <summary>
    /// Devouring Flame's bonus multiplies, 1.07 per Engulfing Flames: with K of them, Detonate deals base x K x 1.07^K, base being
    /// Detonate with one Engulfing Flames and no legendary.
    ///
    /// Per the user. The engine adds the bonus (1 + 0.07 x K, DevouringFlame.Bind), as the tooltip's "7% more ... for each" reads.
    /// TODO: confirm in the game data whether the bonus per Engulfing Flames adds or multiplies.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Devouring_Flame_Multiplies_1_07_Per_Engulfing_Flames(int engulfingFlames)
    {
        double baseHit = FirstDetonateHit(devouringFlame: false, engulfingFlames: 1);
        double legendaryHit = FirstDetonateHit(devouringFlame: true, engulfingFlames);

        AssertRatio(engulfingFlames * Math.Pow(1.07, engulfingFlames), legendaryHit / baseHit);
    }
}
