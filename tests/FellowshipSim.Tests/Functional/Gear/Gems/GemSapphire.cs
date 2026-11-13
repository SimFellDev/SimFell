using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Sapphire gem's nodes: Ancestral Surge (80 / 600 gem power), Mystic's Intuition (150 / 800), Oracle's Foresight (300 / 1250) and
/// Blessing of the Prophet (450 / 1500), rank I then rank II, which replaces rank I. A node is read at the gem power that unlocks it
/// against the power just below, so the other nodes are the same in both fights.
///
/// Resonating Soul only reduces the damage you take, and the sim takes no damage. A rating point is 0.16%. The bare hero has no Spirit.
/// </summary>
public class GemSapphire
{
    /// <summary>A bare hero, Rime by default, with Sapphire at <paramref name="power"/>.</summary>
    private static StepDriver Fight(double power, string hero = "Rime") =>
        StepFights.Of(hero, gemPower: new() { [GemColor.Sapphire] = power });

    /// <summary>Ancestral Surge gives you 10 more Spirit Points (30 at rank II): the most you can hold is 110 instead of 100.</summary>
    [Theory]
    [InlineData(80, 110)]
    [InlineData(600, 130)]
    public void Ancestral_Surge_Gives_More_Spirit_Points(double power, double spiritPoints)
    {
        var f = Fight(power);

        Assert.Equal(spiritPoints, f.Max(Hero.SpiritPoints));
    }

    /// <summary>
    /// Ancestral Surge gives you 8% more main stat (24% at rank II) while Spirit of Heroism is active, and none before or after.
    /// </summary>
    [Theory]
    [InlineData(80, 1.08)]
    [InlineData(600, 1.24 / 1.08)]
    public void Ancestral_Surge_Gives_X_Percent_Main_Stat_During_Spirit_Of_Heroism(double power, double ratio)
    {
        var belowScenario = Fight(power - 1);
        belowScenario.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // its Rising Spirit would add main stat
        belowScenario.FillResources(); // enough to cast it
        var nodeScenario = Fight(power);
        nodeScenario.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // its Rising Spirit would add main stat
        nodeScenario.FillResources(); // enough to cast it

        using var check = new Checks(nodeScenario);
        check.Ratio(1.0, nodeScenario.MainStat() / belowScenario.MainStat());

        belowScenario.Cast("Wrath of Winter");
        nodeScenario.Cast("Wrath of Winter");
        check.Ratio(ratio, nodeScenario.MainStat() / belowScenario.MainStat());

        belowScenario.Wait(60); // Spirit of Heroism ends
        nodeScenario.Wait(60); // Spirit of Heroism ends
        check.Ratio(1.0, nodeScenario.MainStat() / belowScenario.MainStat());
    }

    /// <summary>Mystic's Intuition gives you 8 Spirit rating (24 at rank II).</summary>
    [Theory]
    [InlineData(150, 8)]
    [InlineData(800, 24 - 8)]
    public void Mystics_Intuition_Gives_Spirit_Rating(double power, double rating)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        Assert.Equal(rating * 0.0016, nodeScenario.Spirit() - belowScenario.Spirit(), 1e-9);
    }

    /// <summary>Oracle's Foresight gives you 2% Spirit (6% at rank II).</summary>
    [Theory]
    [InlineData(300, 0.02)]
    [InlineData(1250, 0.06 - 0.02)]
    public void Oracles_Foresight_Gives_Spirit(double power, double bonus)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        Assert.Equal(bonus, nodeScenario.Spirit() - belowScenario.Spirit(), 1e-9);
    }

    /// <summary>
    /// Blessing of the Prophet makes your ultimate cost 5% less (15% at rank II): 95 Spirit Points instead of 100. Cast from the most Spirit
    /// you can hold, it leaves that much minus the cost.
    /// </summary>
    [Theory]
    [InlineData(450, 95)]
    [InlineData(1500, 85)]
    public void Blessing_Of_The_Prophet_Makes_Your_Ultimate_Cost_X_Percent_Less(double power, double cost)
    {
        var f = Fight(power);

        using var check = new Checks(f);
        f.Set(Hero.SpiritPoints, cost - 1);
        check.False(f.TryCast("Wrath of Winter").Succeeded);
        f.Set(Hero.SpiritPoints, cost);
        check.True(f.TryCast("Wrath of Winter").Succeeded);

        // Check that cost not only gates the usage of ability, but also consumes the correct amount of spirit points
        f.Set(Hero.SpiritPoints, f.Max(Hero.SpiritPoints));
        f.Cast("Wrath of Winter");
        check.Equal(f.Max(Hero.SpiritPoints) - cost, f.Amount(Hero.SpiritPoints));
    }

    /// <summary>Blessing of the Prophet makes Spirit of Heroism last 6 seconds longer (18 at rank II): 26 seconds instead of 20.</summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter", 450, 26.0)]
    [InlineData("Rime", "Wrath of Winter", 1500, 38.0)]
    [InlineData("Ardeos", "Incinerate", 1500, 38.0)]
    [InlineData("Elarion", "Event Horizon", 1500, 38.0)]
    [InlineData("Gunde", "Bloodbound Spirit", 1500, 38.0)]
    [InlineData("Mara", "Matriarch Macabre", 1500, 38.0)]
    [InlineData("Tariq", "Raging Tempest", 1500, 38.0)]
    public void Blessing_Of_The_Prophet_Makes_Spirit_Of_Heroism_Last_Longer(string hero, string ultimate, double power,
        double seconds)
    {
        var f = Fight(power, hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // the gem gives Spirit; no Spirit Refund
        f.FillResources(); // enough to cast it

        f.Cast(ultimate);
        double end = f.CompletedAt(ultimate) + seconds;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(Hero.SpiritOfHeroism));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(Hero.SpiritOfHeroism));
    }
}
