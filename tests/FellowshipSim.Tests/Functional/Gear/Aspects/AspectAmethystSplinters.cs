using FellowshipSim.Core.Customization.Aspects;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Amethyst Splinters (aspect 31): "When you deal Critical Damage to an enemy, you deal an additional 4% / 6% / 8% / 11% of that damage to
/// them over 8 seconds" (ranks 1 to 4). The splinter ticks every 2 seconds shortened by haste, each tick a quarter of its damage; one
/// splinter per enemy. Every cast is forced to crit.
/// </summary>
public class AspectAmethystSplinters
{
    private const string Splinter = "Amethyst Splinter";

    /// <summary>
    /// The crit damage of the first instant <paramref name="ability"/> hit: every hit of that instant adds to the splinter.
    /// </summary>
    private static double FirstCrits(StepDriver f, string ability)
    {
        var hits = f.Damage(ability);
        return hits.Where(hit => hit.Time == hits[0].Time).Sum(hit => hit.Value);
    }

    /// <summary>
    /// A critical strike makes a splinter that ticks every 2 seconds for a quarter of 4% / 6% / 8% / 11% of the crit's damage.
    ///
    /// Tariq casts Heavy Strike, since Wild Swing never crits.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.04)]
    [InlineData("Rime", "Frost Bolt", 2, 0.06)]
    [InlineData("Rime", "Frost Bolt", 3, 0.08)]
    [InlineData("Rime", "Frost Bolt", 4, 0.11)]
    [InlineData("Ardeos", "Infernal Wave", 4, 0.11)]
    [InlineData("Elarion", "Focused Shot", 4, 0.11)]
    [InlineData("Gunde", "Double Strike", 4, 0.11)]
    [InlineData("Mara", "Skittering Blades", 4, 0.11)]
    [InlineData("Tariq", "Heavy Strike", 4, 0.11)]
    public void A_Critical_Strike_Makes_An_Amethyst_Splinter(string hero, string ability, int rank, double share)
    {
        var f = StepFights.Of(hero, augmentations: [new(AmethystSplinters.AspectId, rank)]).WithoutDamageSpread();

        f.Cast(ability, crit: true);
        f.Wait(3); // the first tick

        AssertRatio(share / 4, f.Damage(Splinter)[0].Value / FirstCrits(f, ability));
    }

    /// <summary>A splinter deals its whole share in 4 ticks, 2 seconds apart, the last as its 8 seconds end.</summary>
    [Fact]
    public void An_Amethyst_Splinter_Ticks_4_Times_Over_8_Seconds()
    {
        var f = StepFights.Of("Rime", augmentations: [new(AmethystSplinters.AspectId, 4)]).WithoutDamageSpread();

        f.Cast("Frost Bolt", crit: true);
        f.Wait(10); // the splinter ticks out

        double crit = f.Damage("Frost Bolt")[0].Time.TotalSeconds;
        var ticks = f.Damage(Splinter);

        using var check = new Checks(f);
        check.Equal(4, ticks.Count);
        for (int i = 0; i < ticks.Count; i++) check.Seconds(crit + 2 * (i + 1), ticks[i].Time.TotalSeconds, what: $"tick {i + 1}");
        check.Ratio(0.11, ticks.Sum(tick => tick.Value) / FirstCrits(f, "Frost Bolt"));
    }

    /// <summary>
    /// Haste makes a splinter tick faster, and so more often in its 8 seconds, each full tick still a quarter of its share: at 30% haste,
    /// 5 full ticks (every 2 / 1.3 seconds), then a partial tick for the part of a period left as the 8 seconds end (0.2 of a tick).
    /// In all, the splinter deals its share x 1.3.
    /// </summary>
    [Fact]
    public void Haste_Adds_Amethyst_Splinter_Ticks()
    {
        var f = StepFights.Of("Rime", haste: 0.3, augmentations: [new(AmethystSplinters.AspectId, 4)]).WithoutDamageSpread();

        f.Cast("Frost Bolt", crit: true);
        f.Wait(10); // the splinter ticks out

        var ticks = f.Damage(Splinter);

        using var check = new Checks(f);
        int full = FullTicks(8, 2 / 1.3);
        double partial = 8 / (2 / 1.3) - full; // the part of a period left as the 8 seconds end
        check.Equal(full + 1, ticks.Count);
        for (int i = 0; i < ticks.Count; i++)
        {
            double share = i < full ? 0.11 / 4 : 0.11 / 4 * partial;
            check.Ratio(share, ticks[i].Value / FirstCrits(f, "Frost Bolt"), what: $"tick at {ticks[i].Time.TotalSeconds:F3}s");
        }
        check.Ratio(0.11 * 1.3, ticks.Sum(tick => tick.Value) / FirstCrits(f, "Frost Bolt"));
    }

    /// <summary>A splinter never crits, and Expertise does not raise it again: the crit it comes from already has it.</summary>
    [Fact]
    public void An_Amethyst_Splinter_Never_Crits_And_Is_Not_Raised_Again()
    {
        var f = StepFights.Of("Rime", crit: 0.5, expertise: 0.5, augmentations: [new(AmethystSplinters.AspectId, 4)])
            .WithoutDamageSpread();

        f.Cast("Frost Bolt", crit: true);
        f.Wait(3); // the first tick

        var tick = f.Damage(Splinter)[0];

        using var check = new Checks(f);
        check.Equal(0, tick.CritChance);
        check.Ratio(0.11 / 4, tick.Value / FirstCrits(f, "Frost Bolt"));
    }

    /// <summary>
    /// A new crit on an enemy with a splinter adds its share to what the splinter had left and restarts its 8 seconds, the ticks keeping
    /// their rhythm: the next tick deals a quarter of both. What the splinter had left is its share in proportion to the time it had left.
    /// </summary>
    [Fact]
    public void A_New_Crit_Adds_Its_Share_To_The_Amethyst_Splinter()
    {
        var f = StepFights.Of("Rime", augmentations: [new(AmethystSplinters.AspectId, 4)]).WithoutDamageSpread();

        f.Cast("Frost Bolt", crit: true);
        f.Cast("Frost Bolt", crit: true); // lands before the splinter's first tick
        f.Wait(1); // the first tick

        var bolts = f.Damage("Frost Bolt");
        double timeLeft = bolts[0].Time.TotalSeconds + 8 - bolts[1].Time.TotalSeconds;
        double splinterLeft = 0.11 * bolts[0].Value * timeLeft / 8;

        AssertRatio(1.0, f.Damage(Splinter)[0].Value / ((splinterLeft + 0.11 * bolts[1].Value) / 4));
    }

    /// <summary>Each enemy has its own splinter.</summary>
    [Fact]
    public void Each_Enemy_Has_Its_Own_Amethyst_Splinter()
    {
        var f = StepFights.Of("Rime", enemies: 2, augmentations: [new(AmethystSplinters.AspectId, 4)]);

        f.Cast("Frost Bolt", crit: true, target: f.Enemies()[0]);
        f.Cast("Frost Bolt", crit: true, target: f.Enemies()[1]);
        f.Wait(10); // the splinters tick out

        Assert.Equal(2, f.FirstHitOnEachEnemy(Splinter).Count);
    }
}
