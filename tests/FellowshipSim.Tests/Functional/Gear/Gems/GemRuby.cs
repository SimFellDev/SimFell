using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Ruby gem's nodes: Might of the Minotaur (80 / 600 gem power), Champion's Heart (150 / 800), Titan's Blood (300 / 1250) and Blessing
/// of the Conqueror (450 / 1500), rank I then rank II, which replaces rank I. A node is read at the gem power that unlocks it against the
/// power just below, so the other nodes are the same in both fights.
///
/// Unyielding Vitality only heals, and the sim takes no damage.
/// </summary>
public class GemRuby
{
    /// <summary>A bare hero, Rime by default, with Ruby at <paramref name="power"/>.</summary>
    private static StepDriver Fight(double power, string hero = "Rime") =>
        StepFights.Of(hero, gemPower: new() { [GemColor.Ruby] = power }).WithoutDamageSpread();

    /// <summary>
    /// Might of the Minotaur gives you 2% more main stat (6% at rank II) while you are above 80% health.
    ///
    /// The sim keeps the hero at full health.
    /// </summary>
    [Theory]
    [InlineData(80, 1.02)]
    [InlineData(600, 1.06 / 1.02)]
    public void Might_Of_The_Minotaur_Gives_2_Percent_Main_Stat(double power, double ratio)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        AssertRatio(ratio, nodeScenario.MainStat() / belowScenario.MainStat());
    }

    /// <summary>Champion's Heart gives you 2 main stat (6 at rank II), which Might of the Minotaur raises like the rest.</summary>
    [Theory]
    [InlineData(150, 2 * 1.02)]
    [InlineData(800, 4 * 1.06)]
    public void Champions_Heart_Gives_2_Main_Stat(double power, double extra)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        AssertRatio(extra, nodeScenario.MainStat() - belowScenario.MainStat());
    }

    /// <summary>
    /// Titan's Blood gives you 3% more Stamina (9% at rank II). Health grows with the Stamina part of it alone: the hero's base
    /// health is not raised.
    /// </summary>
    [Theory]
    [InlineData(300, 1.03)]
    [InlineData(1250, 1.09 / 1.03)]
    public void Titans_Blood_Gives_3_Percent_Stamina(double power, double ratio)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        AssertRatio(ratio, nodeScenario.Hero.Stats.TotalStamina / belowScenario.Hero.Stats.TotalStamina);
    }

    /// <summary>
    /// Blessing of the Conqueror makes you deal 5% more damage (15% at rank II) while you fight a boss.
    ///
    /// The training dummies count as a boss.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 450, 1.05)]
    [InlineData("Rime", "Frost Bolt", 1500, 1.15 / 1.05)]
    [InlineData("Ardeos", "Infernal Wave", 1500, 1.15 / 1.05)]
    [InlineData("Elarion", "Focused Shot", 1500, 1.15 / 1.05)]
    [InlineData("Gunde", "Double Strike", 1500, 1.15 / 1.05)]
    [InlineData("Mara", "Skittering Blades", 1500, 1.15 / 1.05)]
    [InlineData("Tariq", "Heavy Strike", 1500, 1.15 / 1.05)]
    public void Blessing_Of_The_Conqueror_Gives_5_Percent_Damage(string hero, string ability, double power, double ratio)
    {
        double FirstHit(double gemPower)
        {
            var f = Fight(gemPower, hero);
            f.FillResources(); // enough to cast it
            f.Cast(ability);
            f.Wait(1); // the hit lands
            return f.Damage(ability)[0].Value;
        }

        AssertRatio(ratio, FirstHit(power) / FirstHit(power - 1));
    }
}
