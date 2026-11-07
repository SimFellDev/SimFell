using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Spirit Refund, which every hero has: when the hero spends its resource (Gunde: when a cast applies Rend), a chance of a refund, which
/// gives the resource back a moment later, a Rising Spirit stack and Spirit Points.
///
/// Each test runs over every hero with its spender: an ability that spends the hero's refund resource. Every resource is filled first so
/// the spender can be cast. What a refund gives a hero beyond this (Rime's orbs coming back, Mara's Energy...) is in the hero's files.
/// </summary>
public class SpiritRefund
{
    /// <summary>A Spirit Refund's chance is Spirit / (1 + Spirit): one in six at 20% Spirit.</summary>
    [Theory]
    [InlineData("Rime", "Glacial Blast")]
    [InlineData("Ardeos", "Detonate")]
    [InlineData("Elarion", "Highwind Arrow")]
    [InlineData("Gunde", "Rupture")]
    [InlineData("Mara", "Queen's Fang")]
    [InlineData("Tariq", "Skull Crusher")]
    public void The_Chance_Is_Spirit_Over_1_Plus_Spirit(string hero, string spender)
    {
        var f = StepFights.Of(hero, spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // only to read the chance
        f.FillResources(); // enough to cast the spender

        f.Cast(spender);

        using var check = new Checks(f);
        check.True(procs.ChancesOf(Hero.SpiritRefundProc).Count > 0, "the spender rolled a Spirit Refund");
        check.True(procs.ChancesOf(Hero.SpiritRefundProc).All(chance => Math.Abs(chance - 0.2 / (1 + 0.2)) < 1e-9), "every roll at 0.2 / 1.2");
    }

    /// <summary>A Spirit Refund gives a Rising Spirit stack and Spirit Points: 2 for Rime, 1 for the other heroes.</summary>
    [Theory]
    [InlineData("Rime", "Glacial Blast", 2)]
    [InlineData("Ardeos", "Detonate", 1)]
    [InlineData("Elarion", "Highwind Arrow", 1)]
    [InlineData("Gunde", "Rupture", 1)]
    [InlineData("Mara", "Queen's Fang", 1)]
    [InlineData("Tariq", "Skull Crusher", 1)]
    public void A_Spirit_Refund_Gives_A_Rising_Spirit_Stack_And_Spirit_Points(string hero, string spender, int spiritPoints)
    {
        var f = StepFights.Of(hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.FillResources(); // enough to cast the spender
        f.Set(Hero.SpiritPoints, 0);

        f.Cast(spender);
        f.Wait(0.5); // the refund lands, before the first passive Spirit Point (3s)

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(Hero.RisingSpirit));
        check.Equal(spiritPoints, f.Amount(Hero.SpiritPoints));
    }

    /// <summary>
    /// A Spirit Refund gives back what the spender spent, a moment later: the resource is full again.
    ///
    /// Gunde's refund comes from applying Rend, not from a spend, so he is not in this test; Elarion's Focus regenerates, so he has his own.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Glacial Blast", "Winter Orbs")]
    [InlineData("Ardeos", "Detonate", "Cinders")]
    [InlineData("Mara", "Queen's Fang", "Combo Points")]
    [InlineData("Tariq", "Skull Crusher", "Fury")]
    public void A_Spirit_Refund_Gives_The_Spent_Resource_Back(string hero, string spender, string resource)
    {
        var f = StepFights.Of(hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.FillResources();
        double full = f.Amount(resource);

        f.Cast(spender);
        f.Wait(0.5); // the refund lands

        using var check = new Checks(f);
        check.True(f.Spends(resource).Count > 0, $"{spender} spent {resource}");
        check.Equal(full, f.Amount(resource));
    }

    /// <summary>
    /// A Spirit Refund gives Elarion back the 30 Focus Highwind Arrow spent, a moment later.
    ///
    /// Focus also regenerates at 5 per second while the cast and the wait go on, so it ends at the 30 refunded plus 5 per second
    /// elapsed.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Elarion_The_Spent_Focus_Back()
    {
        var f = StepFights.Of("Elarion");
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Set(f.Resource("Focus"), 30); // just enough for Highwind Arrow

        f.Cast("Highwind Arrow");
        f.Wait(0.5); // the refund lands

        Assert.Equal(30 + 5.0 * f.Seconds(), f.Amount("Focus"), 1e-9);
    }

    /// <summary>A Spirit Refund does not take Spirit Points past 100.</summary>
    [Theory]
    [InlineData("Rime", "Glacial Blast")]
    [InlineData("Ardeos", "Detonate")]
    [InlineData("Elarion", "Highwind Arrow")]
    [InlineData("Gunde", "Rupture")]
    [InlineData("Mara", "Queen's Fang")]
    [InlineData("Tariq", "Skull Crusher")]
    public void Spirit_Points_Stop_At_100(string hero, string spender)
    {
        var f = StepFights.Of(hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.FillResources(); // Spirit Points at 100 too

        f.Cast(spender);
        f.Wait(0.5); // the refund lands

        Assert.Equal(100, f.Amount(Hero.SpiritPoints));
    }
}
