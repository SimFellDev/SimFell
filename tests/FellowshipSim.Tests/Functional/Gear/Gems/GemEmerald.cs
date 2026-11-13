using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Emerald gem's nodes: First Strike (80 / 600 gem power), Vanguard's Resolve (150 / 800), Tactician's Accumen (300 / 1250) and
/// Blessing of the Commander (450 / 1500), rank I then rank II, which replaces rank I. A node is read at the gem power that unlocks it
/// against the power just below, so the other nodes are the same in both fights.
///
/// Sentinel's Bastion only shields, and the sim takes no damage. A rating point is 0.16%. The bare hero has no Expertise.
/// </summary>
public class GemEmerald
{
    private static readonly AuraDef FirstStrike = Emerald.AllNodes[0].Grants!;

    /// <summary>A bare hero, Rime by default, with Emerald at <paramref name="power"/>.</summary>
    private static StepDriver Fight(double power, string hero = "Rime", int enemies = 1) =>
        StepFights.Of(hero, enemies: enemies, gemPower: new() { [GemColor.Emerald] = power });

    /// <summary>The first time you damage an enemy, First Strike gives you 5% Expertise (15% at rank II).</summary>
    [Theory]
    [InlineData(80, 0.05)]
    [InlineData(600, 0.15)]
    public void First_Strike_Gives_Expertise_On_Your_First_Hit(double power, double bonus)
    {
        var f = Fight(power);
        double expertiseBefore = f.Expertise();

        f.Cast("Frost Bolt");
        f.Wait(1); // the bolt lands

        Assert.Equal(bonus, f.Expertise() - expertiseBefore, 1e-9);
    }

    /// <summary>First Strike lasts 15 seconds.</summary>
    [Fact]
    public void First_Strike_Lasts_15_Seconds()
    {
        var f = Fight(80);

        f.Cast("Frost Bolt");
        f.Wait(1); // the bolt lands
        double end = f.Damage("Frost Bolt")[0].Time.TotalSeconds + 15;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(FirstStrike));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(FirstStrike));
    }

    /// <summary>
    /// Only your first hit on each enemy gives First Strike: 10 seconds in, a first hit on a second enemy starts its 15 seconds again, and
    /// another hit on the first enemy does not.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Only_Your_First_Hit_On_Each_Enemy_Gives_First_Strike(bool newEnemy)
    {
        var f = Fight(80, enemies: 2);

        f.Cast("Frost Bolt");
        f.Wait(1); // the bolt lands
        double firstEnd = f.Damage("Frost Bolt")[0].Time.TotalSeconds + 15;
        f.WaitUntil(10);
        f.Cast("Frost Bolt", target: newEnemy ? f.Enemies()[1] : f.Enemy);

        f.WaitUntil(firstEnd + 0.1);
        Assert.Equal(newEnemy, f.Has(FirstStrike));
    }

    /// <summary>Vanguard's Resolve gives you 8 Expertise rating (24 at rank II).</summary>
    [Theory]
    [InlineData(150, 8)]
    [InlineData(800, 24 - 8)]
    public void Vanguards_Resolve_Gives_Expertise_Rating(double power, double rating)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        Assert.Equal(rating * 0.0016, nodeScenario.Expertise() - belowScenario.Expertise(), 1e-9);
    }

    /// <summary>Tactician's Accumen gives you 2% Expertise (6% at rank II).</summary>
    [Theory]
    [InlineData(300, 0.02)]
    [InlineData(1250, 0.06 - 0.02)]
    public void Tacticians_Accumen_Gives_Expertise(double power, double bonus)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        Assert.Equal(bonus, nodeScenario.Expertise() - belowScenario.Expertise(), 1e-9);
    }

    /// <summary>
    /// Blessing of the Commander makes your cooldowns 4% shorter (12% at rank II), from 450 gem power.
    ///
    /// Read on each hero's Major ability. Its weapon abilities are in WeaponCooldowns.cs.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Ice Blitz", 60.0, 449, 0.0)]
    [InlineData("Rime", "Ice Blitz", 60.0, 450, 0.04)]
    [InlineData("Rime", "Ice Blitz", 60.0, 1500, 0.12)]
    [InlineData("Ardeos", "Apocalypse", 45.0, 1500, 0.12)]
    [InlineData("Elarion", "Lunarlight Mark", 40.0, 1500, 0.12)]
    [InlineData("Gunde", "Slaughter", 30.0, 1500, 0.12)]
    [InlineData("Mara", "Maiden of Death", 60.0, 1500, 0.12)]
    [InlineData("Tariq", "Thunder Call", 45.0, 1500, 0.12)]
    public void Blessing_Of_The_Commander_Makes_Your_Cooldowns_X_Percent_Shorter(string hero, string major, double cooldown,
        double power, double cut)
    {
        var f = Fight(power, hero);
        f.FillResources(); // enough to cast it

        f.Cast(major);
        double end = f.CompletedAt(major) + cooldown * (1 - cut);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(major));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(major));
    }
}
