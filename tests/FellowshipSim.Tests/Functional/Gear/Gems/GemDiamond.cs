using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Diamond gem's nodes: Harmonious Soul (80 / 600 gem power), Stoic's Teachings (150 / 800) and Ancient's Wisdom (300 / 1250), rank I
/// then rank II, which replaces rank I. A node is read at the gem power that unlocks it against the power just below, so the other nodes
/// are the same in both fights.
///
/// Tranquil Resolve only reduces the damage you take, and the sim takes no damage. Blessing of the Artisan shortens your relics' cooldowns,
/// and the sim's relics have none.
/// </summary>
public class GemDiamond
{
    /// <summary>A bare Rime with Diamond at <paramref name="power"/>.</summary>
    private static StepDriver Fight(double power, int enemies = 1) =>
        StepFights.Of("Rime", enemies: enemies, gemPower: new() { [GemColor.Diamond] = power });

    /// <summary>Defeats the first <paramref name="count"/> enemies.</summary>
    private static void Defeat(StepDriver f, int count)
    {
        foreach (var enemy in f.Enemies().Take(count).ToList()) f.Defeat(enemy);
    }

    /// <summary>
    /// Each enemy defeated gives you a stack of Harmonious Soul: 0.6% crit chance, haste, Expertise and Spirit per stack.
    /// </summary>
    [Fact]
    public void Harmonious_Soul_Gives_0_6_Percent_Crit_Haste_Expertise_And_Spirit_Per_Kill()
    {
        var f = Fight(80, enemies: 4);
        double critBefore = f.CritChance(), hasteBefore = f.Haste(), expertiseBefore = f.Expertise(), spiritBefore = f.Spirit();

        Defeat(f, 3);

        using var check = new Checks(f);
        check.Equal(3, f.Stacks(Diamond.HarmoniousSoulSmall));
        check.Equal(3 * 0.006, f.CritChance() - critBefore);
        check.Equal(3 * 0.006, f.Haste() - hasteBefore);
        check.Equal(3 * 0.006, f.Expertise() - expertiseBefore);
        check.Equal(3 * 0.006, f.Spirit() - spiritBefore);
    }

    /// <summary>Harmonious Soul stacks up to 4 times (12 at rank II).</summary>
    [Theory]
    [InlineData(80, 4)]
    [InlineData(600, 12)]
    public void Harmonious_Soul_Stacks_Up_To_Its_Cap(double power, int maxStacks)
    {
        var f = Fight(power, enemies: maxStacks + 2);
        var soul = power < 600 ? Diamond.HarmoniousSoulSmall : Diamond.HarmoniousSoulMedium;

        Defeat(f, maxStacks + 2);

        Assert.Equal(maxStacks, f.Stacks(soul));
    }

    /// <summary>When Harmonious Soul's 5 seconds run out you lose one stack, and the 5 seconds start again for the others.</summary>
    [Fact]
    public void Harmonious_Soul_Loses_One_Stack_Every_5_Seconds()
    {
        var f = Fight(80, enemies: 4);

        Defeat(f, 4);

        using var check = new Checks(f);
        for (int lost = 0; lost < 4; lost++)
        {
            f.WaitUntil(5 * (lost + 1) - 0.1);
            check.Equal(4 - lost, f.Stacks(Diamond.HarmoniousSoulSmall), what: $"before {5 * (lost + 1)}s");
            f.WaitUntil(5 * (lost + 1) + 0.1);
            check.Equal(4 - lost - 1, f.Stacks(Diamond.HarmoniousSoulSmall), what: $"after {5 * (lost + 1)}s");
        }
    }

    /// <summary>A kill restarts Harmonious Soul's 5 seconds for every stack.</summary>
    [Fact]
    public void A_Kill_Restarts_Harmonious_Souls_5_Seconds()
    {
        var f = Fight(80, enemies: 2);

        f.Defeat(f.Enemies()[0]);
        f.WaitUntil(3);
        f.Defeat(f.Enemies()[1]);

        f.WaitUntil(3 + 5 - 0.1);
        Assert.Equal(2, f.Stacks(Diamond.HarmoniousSoulSmall));
        f.WaitUntil(3 + 5 + 0.1);
        Assert.Equal(1, f.Stacks(Diamond.HarmoniousSoulSmall));
    }

    /// <summary>Stoic's Teachings gives you 4 main stat (12 at rank II), which Ancient's Wisdom raises like the rest.</summary>
    [Theory]
    [InlineData(150, 4)]
    [InlineData(800, 8 * 1.02)]
    public void Stoics_Teachings_Gives_Main_Stat(double power, double extra)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        AssertRatio(extra, nodeScenario.MainStat() - belowScenario.MainStat());
    }

    /// <summary>Ancient's Wisdom gives you 2% more main stat (6% at rank II).</summary>
    [Theory]
    [InlineData(300, 1.02)]
    [InlineData(1250, 1.06 / 1.02)]
    public void Ancients_Wisdom_Gives_X_Percent_Main_Stat(double power, double ratio)
    {
        var belowScenario = Fight(power - 1);
        var nodeScenario = Fight(power);

        AssertRatio(ratio, nodeScenario.MainStat() / belowScenario.MainStat());
    }
}
