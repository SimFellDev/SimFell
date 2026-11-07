using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rising Spirit, the buff every Spirit Refund gives: +5% main stat per stack, up to 10 stacks, for 10 seconds. Each test runs over every
/// hero.
///
/// The stacks are put on the hero directly, except where the rule is about what a further Spirit Refund does: there it comes from the
/// hero's spender, as in Generic/SpiritRefund.cs, which has the refund's own rules.
/// </summary>
public class RisingSpirit
{
    /// <summary>Each stack of Rising Spirit gives 5% more main stat: main stat x (1 + 0.05 x stacks).</summary>
    [Theory]
    [InlineData("Rime", 1)]
    [InlineData("Rime", 5)]
    [InlineData("Ardeos", 1)]
    [InlineData("Ardeos", 5)]
    [InlineData("Elarion", 1)]
    [InlineData("Elarion", 5)]
    [InlineData("Gunde", 1)]
    [InlineData("Gunde", 5)]
    [InlineData("Mara", 1)]
    [InlineData("Mara", 5)]
    [InlineData("Tariq", 1)]
    [InlineData("Tariq", 5)]
    public void Each_Rising_Spirit_Stack_Gives_5_Percent_Main_Stat(string hero, int stacks)
    {
        var f = StepFights.Of(hero);
        double plainMainStat = f.MainStat();

        f.Apply(Hero.RisingSpirit, stacks: stacks);

        AssertRatio(1 + 0.05 * stacks, f.MainStat() / plainMainStat);
    }

    /// <summary>Rising Spirit stops at 10 stacks: a Spirit Refund at 10 stacks leaves 10, for 50% more main stat.</summary>
    [Theory]
    [InlineData("Rime", "Glacial Blast")]
    [InlineData("Ardeos", "Detonate")]
    [InlineData("Elarion", "Highwind Arrow")]
    [InlineData("Gunde", "Rupture")]
    [InlineData("Mara", "Queen's Fang")]
    [InlineData("Tariq", "Skull Crusher")]
    public void Rising_Spirit_Stops_At_10_Stacks(string hero, string spender)
    {
        var f = StepFights.Of(hero);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        double plainMainStat = f.MainStat();
        f.FillResources(); // enough to cast the spender
        f.Apply(Hero.RisingSpirit, stacks: 10);

        f.Cast(spender);
        f.Wait(0.5); // the refund lands

        using var check = new Checks(f);
        check.True(procs.ChancesOf(Hero.SpiritRefundProc).Count > 0, "a Spirit Refund");
        check.Equal(10, f.Stacks(Hero.RisingSpirit));
        check.Ratio(1.5, f.MainStat() / plainMainStat);
    }

    /// <summary>Rising Spirit lasts 10 seconds.</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    [InlineData("Gunde")]
    [InlineData("Mara")]
    [InlineData("Tariq")]
    public void Rising_Spirit_Lasts_10_Seconds(string hero)
    {
        var f = StepFights.Of(hero);

        f.Apply(Hero.RisingSpirit);

        f.WaitUntil(10 - 0.1);
        Assert.True(f.Has(Hero.RisingSpirit));
        f.WaitUntil(10 + 0.1);
        Assert.False(f.Has(Hero.RisingSpirit));
    }

    /// <summary>
    /// A further Spirit Refund adds 2 seconds to the time Rising Spirit has left, rather than restarting its 10 seconds: a stack from 0s,
    /// a refund at 4s, and both stacks end at 12s.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Glacial Blast")]
    [InlineData("Ardeos", "Detonate")]
    [InlineData("Elarion", "Highwind Arrow")]
    [InlineData("Gunde", "Rupture")]
    [InlineData("Mara", "Queen's Fang")]
    [InlineData("Tariq", "Skull Crusher")]
    public void A_Further_Spirit_Refund_Adds_2_Seconds_To_Rising_Spirit(string hero, string spender)
    {
        var f = StepFights.Of(hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Apply(Hero.RisingSpirit); // a first Spirit Refund, at 0s

        f.WaitUntil(4);
        f.FillResources(); // enough to cast the spender
        f.Cast(spender);

        using var check = new Checks(f);
        f.WaitUntil(12 - 0.1);
        check.Equal(2, f.Stacks(Hero.RisingSpirit));
        f.WaitUntil(12 + 0.1);
        check.False(f.Has(Hero.RisingSpirit));
    }

    /// <summary>
    /// Rising Spirit lasts at most 20 seconds from its first application, however many Spirit Refunds extend it: casting a
    /// spender for 20 seconds with every Spirit Refund triggering, it ends 20 seconds after the first cast.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Ice Comet")]
    [InlineData("Ardeos", "Detonate")]
    [InlineData("Elarion", "Celestial Shot")]
    [InlineData("Gunde", "Double Strike")]
    [InlineData("Mara", "Queen's Fang")]
    [InlineData("Tariq", "Skull Crusher")]
    public void Rising_Spirit_Lasts_At_Most_20_Seconds(string hero, string spender)
    {
        var f = StepFights.Of(hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);

        f.FillResources(); // enough to cast it
        f.Cast(spender);
        double firstCast = f.CompletedAt(spender); // the first Spirit Refund applies Rising Spirit
        while (f.Now.TotalSeconds < firstCast + 19)
        {
            f.FillResources(); // enough to cast it
            f.CastWhenReady(spender);
        }

        using var check = new Checks(f);
        f.WaitUntil(firstCast + 20 - 0.1);
        check.True(f.Has(Hero.RisingSpirit));
        f.WaitUntil(firstCast + 20 + 0.1);
        check.False(f.Has(Hero.RisingSpirit));
    }
}
