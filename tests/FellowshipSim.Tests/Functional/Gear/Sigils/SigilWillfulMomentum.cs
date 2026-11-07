using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Willful Momentum (sigil 39): "Your Spirit Rating is increased by 7 / 11 / 17 / 23. Whenever you trigger a Spirit Refund your Main Stat
/// is increased by 1.5% / 2.6% / 3.7% / 4.8% for 4 seconds" (ranks 1 to 4). A rating point is 0.16%. Spirit Refunds are forced.
/// </summary>
public class SigilWillfulMomentum
{
    /// <summary>A hero with the sigil at <paramref name="rank"/> (none at 0), its Spirit Refunds forced to succeed.</summary>
    private static StepDriver Fight(string hero, int rank)
    {
        var f = StepFights.Of(hero, augmentations: StepFights.Ranked(WillfulMomentum.SigilId, rank));
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.FillResources(); // enough to cast the spender
        f.Set(Hero.SpiritPoints, 0); // room for the refund's Spirit Points, which time it
        return f;
    }

    /// <summary>Willful Momentum adds 7 / 11 / 17 / 23 Spirit rating.</summary>
    [Theory]
    [InlineData("Rime", 1, 7)]
    [InlineData("Rime", 2, 11)]
    [InlineData("Rime", 3, 17)]
    [InlineData("Rime", 4, 23)]
    [InlineData("Ardeos", 4, 23)]
    [InlineData("Elarion", 4, 23)]
    [InlineData("Gunde", 4, 23)]
    [InlineData("Mara", 4, 23)]
    [InlineData("Tariq", 4, 23)]
    public void Willful_Momentum_Adds_Spirit_Rating(string hero, int rank, double rating)
    {
        var f = StepFights.Of(hero, augmentations: [new(WillfulMomentum.SigilId, rank)]);

        Assert.Equal(rating * 0.0016, f.Spirit(), 1e-9);
    }

    /// <summary>
    /// A Spirit Refund gives you Willful Momentum: 1.5% / 2.6% / 3.7% / 4.8% more main stat.
    ///
    /// Compared with the same refund without the sigil, which gives the same Rising Spirit stack. TODO: confirm in the game that the two
    /// multiply, as the engine's main stat layers have it (Hero.PrimaryAttribute).
    /// </summary>
    [Theory]
    [InlineData("Rime", "Glacial Blast", 1, 0.015)]
    [InlineData("Rime", "Glacial Blast", 2, 0.026)]
    [InlineData("Rime", "Glacial Blast", 3, 0.037)]
    [InlineData("Rime", "Glacial Blast", 4, 0.048)]
    [InlineData("Ardeos", "Detonate", 4, 0.048)]
    [InlineData("Elarion", "Highwind Arrow", 4, 0.048)]
    [InlineData("Gunde", "Rupture", 4, 0.048)]
    [InlineData("Mara", "Queen's Fang", 4, 0.048)]
    [InlineData("Tariq", "Skull Crusher", 4, 0.048)]
    public void A_Spirit_Refund_Gives_Willful_Momentums_Main_Stat(string hero, string spender, int rank, double bonus)
    {
        var plainScenario = Fight(hero, rank: 0);
        var momentumScenario = Fight(hero, rank);

        plainScenario.Cast(spender);
        plainScenario.Wait(0.5); // the refund lands
        momentumScenario.Cast(spender);
        momentumScenario.Wait(0.5); // the refund lands

        AssertRatio(1 + bonus, momentumScenario.MainStat() / plainScenario.MainStat());
    }

    /// <summary>Willful Momentum lasts 4 seconds.</summary>
    [Fact]
    public void Willful_Momentum_Lasts_4_Seconds()
    {
        var f = Fight("Rime", rank: 1);

        f.Cast("Glacial Blast");
        f.Wait(0.5); // the refund lands
        double start = f.Gains("Spirit").Last().Time.TotalSeconds;

        f.WaitUntil(start + 4 - 0.1);
        Assert.True(f.Has(WillfulMomentum.Buff));
        f.WaitUntil(start + 4 + 0.1);
        Assert.False(f.Has(WillfulMomentum.Buff));
    }

    /// <summary>A second Spirit Refund during Willful Momentum restarts its 4 seconds: the bonus does not stack.</summary>
    [Fact]
    public void A_Second_Spirit_Refund_Restarts_Willful_Momentum()
    {
        var plainScenario = Fight("Rime", rank: 0);
        var momentumScenario = Fight("Rime", rank: 1);

        foreach (var f in new[] { plainScenario, momentumScenario })
        {
            f.Cast("Glacial Blast");
            f.FillResources(); // enough to cast it again
            f.Set(Hero.SpiritPoints, 0); // room for the refund's Spirit Points
            f.Cast("Glacial Blast");
            f.Wait(0.5); // the refund lands
        }
        double start = momentumScenario.Gains("Spirit").Last().Time.TotalSeconds;

        using var check = new Checks(momentumScenario);
        check.Ratio(1.015, momentumScenario.MainStat() / plainScenario.MainStat());
        momentumScenario.WaitUntil(start + 4 - 0.1);
        check.True(momentumScenario.Has(WillfulMomentum.Buff));
        momentumScenario.WaitUntil(start + 4 + 0.1);
        check.False(momentumScenario.Has(WillfulMomentum.Buff));
    }
}
