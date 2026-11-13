using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Amethyst gem's nodes: Sealed Fate (80 / 600 gem power), Berserker's Zeal (150 / 800), Killer Instinct (300 / 1250) and Blessing of
/// the Deathdealer (450 / 1500), rank I then rank II, which replaces rank I. A node is read at the gem power that unlocks it against the
/// power just below, so the other nodes are the same in both fights.
///
/// Reaper's Reprieve only heals, and the sim takes no damage. A rating point is 0.16%. The bare hero has no crit chance.
/// </summary>
public class GemAmethyst
{
    /// <summary>A bare hero, Rime by default, with Amethyst at <paramref name="power"/>.</summary>
    private static StepDriver Fight(double power, string hero = "Rime") =>
        StepFights.Of(hero, gemPower: new() { [GemColor.Amethyst] = power }).WithoutDamageSpread();

    /// <summary>
    /// Sealed Fate gives you 5% crit chance (15% at rank II) against enemies above 50% health.
    ///
    /// The enemy's health is a fraction of its maximum.
    /// </summary>
    [Theory]
    [InlineData(80, 1.0, 0.05)]
    [InlineData(80, 0.6, 0.05)]
    [InlineData(80, 0.4, 0.0)]
    [InlineData(600, 1.0, 0.15 - 0.05)]
    [InlineData(600, 0.4, 0.0)]
    public void Sealed_Fate_Gives_Crit_Chance_Against_Enemies_Above_Half_Health(double power, double health, double bonus)
    {
        var belowScenario = Fight(power - 1);
        belowScenario.SetHealth(health);
        var nodeScenario = Fight(power);
        nodeScenario.SetHealth(health);

        Assert.Equal(bonus, nodeScenario.CritChance() - belowScenario.CritChance(), 1e-9);
    }

    /// <summary>Berserker's Zeal gives you 8 crit rating (24 at rank II).</summary>
    [Theory]
    [InlineData(150, 8)]
    [InlineData(800, 24 - 8)]
    public void Berserkers_Zeal_Gives_Crit_Rating(double power, double rating)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        Assert.Equal(rating * 0.0016, nodeScenario.CritChance() - belowScenario.CritChance(), 1e-9);
    }

    /// <summary>Killer Instinct gives you 2% crit chance (6% at rank II).</summary>
    [Theory]
    [InlineData(300, 0.02)]
    [InlineData(1250, 0.06 - 0.02)]
    public void Killer_Instinct_Gives_Crit_Chance(double power, double bonus)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        Assert.Equal(bonus, nodeScenario.CritChance() - belowScenario.CritChance(), 1e-9);
    }

    /// <summary>Blessing of the Deathdealer makes your critical strikes deal 2% more damage (6% at rank II).</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 450, 1.02)]
    [InlineData("Rime", "Frost Bolt", 1500, 1.06 / 1.02)]
    [InlineData("Ardeos", "Infernal Wave", 1500, 1.06 / 1.02)]
    [InlineData("Elarion", "Focused Shot", 1500, 1.06 / 1.02)]
    [InlineData("Gunde", "Double Strike", 1500, 1.06 / 1.02)]
    [InlineData("Mara", "Skittering Blades", 1500, 1.06 / 1.02)]
    [InlineData("Tariq", "Heavy Strike", 1500, 1.06 / 1.02)]
    public void Blessing_Of_The_Deathdealer_Makes_Critical_Strikes_Deal_X_Percent_More(string hero, string ability, double power,
        double ratio)
    {
        double FirstCriticalHit(double gemPower)
        {
            var f = Fight(gemPower, hero);
            f.FillResources(); // enough to cast it
            f.Cast(ability, crit: true);
            f.Wait(1); // the hit lands
            return f.Damage(ability)[0].Value;
        }

        AssertRatio(ratio, FirstCriticalHit(power) / FirstCriticalHit(power - 1));
    }
}
