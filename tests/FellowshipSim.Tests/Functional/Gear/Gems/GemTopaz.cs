using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Topaz gem's nodes: Adrenaline Rush (80 / 600 gem power), Thief's Alacrity (150 / 800), Feline Grace (300 / 1250) and Blessing of the
/// Virtuoso (450 / 1500), rank I then rank II, which replaces rank I. A node is read at the gem power that unlocks it against the power
/// just below, so the other nodes are the same in both fights.
///
/// Rogue's Resurgence only heals, and the sim takes no damage. A rating point is 0.16%. The bare hero has no haste.
/// </summary>
public class GemTopaz
{
    private static readonly AuraDef AdrenalineRush = Topaz.AllNodes[0].Grants!;

    /// <summary>A bare hero, Rime by default, with Topaz at <paramref name="power"/>.</summary>
    private static StepDriver Fight(double power, string hero = "Rime") =>
        StepFights.Of(hero, gemPower: new() { [GemColor.Topaz] = power });

    /// <summary>
    /// Damaging an enemy at 30% health or less gives you Adrenaline Rush: 3% haste (9% at rank II).
    ///
    /// The enemy's health is a fraction of its maximum.
    /// </summary>
    [Theory]
    [InlineData(80, 0.26, 0.03)]
    [InlineData(80, 0.34, 0.0)]
    [InlineData(600, 0.26, 0.09)]
    public void Adrenaline_Rush_Gives_Haste_On_Hitting_An_Enemy_At_30_Percent_Health(double power, double health, double bonus)
    {
        var f = Fight(power);
        f.SetHealth(health);
        double hasteBefore = f.Haste();

        f.Cast("Frost Bolt");
        f.Wait(1); // the bolt lands

        Assert.Equal(bonus, f.Haste() - hasteBefore, 1e-9);
    }

    /// <summary>Adrenaline Rush lasts 10 seconds.</summary>
    [Fact]
    public void Adrenaline_Rush_Lasts_10_Seconds()
    {
        var f = Fight(80);
        f.SetHealth(0.26); // at 26% health

        f.Cast("Frost Bolt");
        f.Wait(1); // the bolt lands
        double end = f.Damage("Frost Bolt")[0].Time.TotalSeconds + 10;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(AdrenalineRush));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(AdrenalineRush));
    }

    /// <summary>Thief's Alacrity gives you 8 haste rating (24 at rank II).</summary>
    [Theory]
    [InlineData(150, 8)]
    [InlineData(800, 24 - 8)]
    public void Thiefs_Alacrity_Gives_Haste_Rating(double power, double rating)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        Assert.Equal(rating * 0.0016, nodeScenario.Haste() - belowScenario.Haste(), 1e-9);
    }

    /// <summary>Feline Grace gives you 2% haste (6% at rank II).</summary>
    [Theory]
    [InlineData(300, 0.02)]
    [InlineData(1250, 0.06 - 0.02)]
    public void Feline_Grace_Gives_Haste(double power, double bonus)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        Assert.Equal(bonus, nodeScenario.Haste() - belowScenario.Haste(), 1e-9);
    }

    /// <summary>
    /// Blessing of the Virtuoso: you keep 2% (6% at rank II) of Spirit of Heroism's haste while it is not active, and nothing more during it.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter", 450, 0.02)]
    [InlineData("Rime", "Wrath of Winter", 1500, 0.06 - 0.02)]
    [InlineData("Ardeos", "Incinerate", 1500, 0.06 - 0.02)]
    [InlineData("Elarion", "Event Horizon", 1500, 0.06 - 0.02)]
    [InlineData("Gunde", "Bloodbound Spirit", 1500, 0.06 - 0.02)]
    [InlineData("Mara", "Matriarch Macabre", 1500, 0.06 - 0.02)]
    [InlineData("Tariq", "Raging Tempest", 1500, 0.06 - 0.02)]
    public void Blessing_Of_The_Virtuoso_Keeps_Haste_Outside_Spirit_Of_Heroism(string hero, string ultimate, double power,
        double bonus)
    {
        var belowScenario = Fight(power - 1, hero);
        belowScenario.FillResources(); // enough to cast it
        var nodeScenario = Fight(power, hero);
        nodeScenario.FillResources(); // enough to cast it

        using var check = new Checks(nodeScenario);
        check.Equal(bonus, nodeScenario.Haste() - belowScenario.Haste());

        belowScenario.Cast(ultimate);
        nodeScenario.Cast(ultimate);
        double end = nodeScenario.CompletedAt(ultimate) + 20;
        check.True(nodeScenario.Has(Hero.SpiritOfHeroism));
        check.Equal(0, nodeScenario.Haste() - belowScenario.Haste());

        belowScenario.WaitUntil(end + 0.1);
        nodeScenario.WaitUntil(end + 0.1);
        check.False(nodeScenario.Has(Hero.SpiritOfHeroism));
        check.Equal(bonus, nodeScenario.Haste() - belowScenario.Haste());
    }
}
