using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How much a critical strike deals, whatever the hero and wherever the crit power comes from.
///
/// Rime's Frost Bolt is the instrument, and every hit is forced to crit or not. The crit power comes from Biting Cold (10%, a talent)
/// or Blessing of the Deathdealer - II (6%, the Amethyst gem's rank 10 node): both must follow the same formula.
/// </summary>
public class CriticalStrike
{
    private const string NoCritPower = "no crit power";
    private const string BitingColdTalent = "Biting Cold";
    private const string Deathdealer = "Blessing of the Deathdealer - II";

    /// <summary>A bare Rime with <paramref name="critChance"/> and the crit power of <paramref name="source"/>.</summary>
    private static StepDriver Rime(string source, double critChance = 0) => source switch
    {
        NoCritPower => StepFights.Rime(crit: critChance),
        BitingColdTalent => StepFights.Rime(crit: critChance, talents: [BitingCold.TalentId]),
        // the gem's power unlocks every node up to rank 10; the others add crit chance, read from the fight below
        Deathdealer => StepFights.Rime(crit: critChance, gemPower: new() { [GemColor.Amethyst] = 1500 }),
        _ => throw new ArgumentException(source),
    };

    /// <summary>Below 100% crit chance, a critical strike deals 2 x (1 + crit power) times the base damage.</summary>
    [Theory]
    [InlineData(0.2, NoCritPower, 0.0)]
    [InlineData(0.5, NoCritPower, 0.0)]
    [InlineData(0.2, BitingColdTalent, 0.1)]
    [InlineData(0.5, BitingColdTalent, 0.1)]
    [InlineData(0.2, Deathdealer, 0.06)]
    [InlineData(0.5, Deathdealer, 0.06)]
    public void Crit_Power_Scales_Critical_Strikes(double critChance, string source, double critPower)
    {
        var plainScenario = Rime(source).WithoutDamageSpread();

        plainScenario.Cast<FrostBolt>(crit: false);
        double plainDamage = plainScenario.Damage<FrostBolt>().First().Value;

        var critScenario = Rime(source, critChance).WithoutDamageSpread();
        Assert.True(critScenario.CritChance() < 1.0); // still below 100%, the gem's crit chance included

        critScenario.Cast<FrostBolt>(crit: true);
        double critDamage = critScenario.Damage<FrostBolt>().First().Value;

        AssertRatio(2 * (1 + critPower), critDamage / plainDamage);
    }

    /// <summary>
    /// Above 100% crit chance, every strike is a grievous critical strike: it deals the critical strike's 2 x (1 + crit power), plus
    /// the crit chance over 100%, times the base damage. At 150% with Biting Cold: 2.2 + 0.5 = 2.7.
    /// </summary>
    [Theory]
    [InlineData(1.5, NoCritPower, 0.0)]
    [InlineData(2.0, NoCritPower, 0.0)]
    [InlineData(1.5, BitingColdTalent, 0.1)]
    [InlineData(2.0, BitingColdTalent, 0.1)]
    [InlineData(1.5, Deathdealer, 0.06)]
    [InlineData(2.0, Deathdealer, 0.06)]
    public void Crit_Power_And_Crit_Chance_Over_100_Percent_Scale_Grievous_Crits(double critChance, string source, double critPower)
    {
        var plainScenario = Rime(source).WithoutDamageSpread();

        plainScenario.Cast<FrostBolt>(crit: false);
        double plainDamage = plainScenario.Damage<FrostBolt>().First().Value;

        var critScenario = Rime(source, critChance).WithoutDamageSpread();
        double heroCritChance = critScenario.CritChance(); // the gem's crit chance included

        critScenario.Cast<FrostBolt>(crit: true);
        double critDamage = critScenario.Damage<FrostBolt>().First().Value;

        AssertRatio(2 * (1 + critPower) + (heroCritChance - 1), critDamage / plainDamage);
    }
}
