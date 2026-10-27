using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearShared;

/// <summary>
/// Rising Spirit, the buff of Spirit Refunds: +5% Main Stat per stack, up to 10 stacks, for 10 seconds.
/// Dev: the aura is put on the hero directly (its source, the Spirit Refund, is in <see cref="SharedSpiritRefund"/>); Main Stat is read off the hero
/// with 1000 base, so n stacks make 1000 x (1 + 0.05 n).
/// </summary>
public class SharedRisingSpirit
{
    public static IEnumerable<object[]> Heroes => SharedFights.Heroes;

    /// <summary>
    /// Player: each stack of Rising Spirit gives 5% more Main Stat.
    /// Dev: 1, 5 and 10 stacks.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Each_Stack_Gives_Five_Percent_Main_Stat(GearHero hero)
    {
        foreach (int stacks in new[] { 0, 1, 5, 10 })
        {
            var f = hero.Fight();
            if (stacks > 0) f.Hero.Auras.Apply(Hero.RisingSpirit, f.Now, setStacks: stacks);
            Assert.Equal(1000 * (1 + 0.05 * stacks), f.Hero.PrimaryAttribute(f.Now), 1e-6);
        }
    }

    /// <summary>
    /// Player: Rising Spirit stops at 10 stacks.
    /// Dev: 12 applications; 10 stacks and 1500 Main Stat.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void It_Stops_At_Ten_Stacks(GearHero hero)
    {
        var f = hero.Fight();
        for (int i = 0; i < 12; i++) f.Hero.Auras.Apply(Hero.RisingSpirit, f.Now);
        Assert.Equal(10, f.SharedStacks(Hero.RisingSpirit));
        Assert.Equal(1500, f.Hero.PrimaryAttribute(f.Now), 1e-6);
    }

    /// <summary>
    /// Player: Rising Spirit lasts 10 seconds.
    /// Dev: one stack; present at 9.9 seconds, gone at 10.1.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void It_Lasts_Ten_Seconds(GearHero hero)
    {
        var f = hero.Fight();
        f.Hero.Auras.Apply(Hero.RisingSpirit, f.Now);
        f.Wait(9.9);
        Assert.Equal(1, f.SharedStacks(Hero.RisingSpirit));
        f.Wait(0.2);
        Assert.Equal(0, f.SharedStacks(Hero.RisingSpirit));
    }

    /// <summary>
    /// Player: a further Spirit Refund adds 2 seconds to the time left instead of restarting the 10 seconds (measured from combat logs).
    /// Dev: one stack at 0, a second at 4 seconds (6 left, so it now ends at 12): two stacks at 11.9 seconds, none at 12.1.
    /// TODO: check in game; the engine's 2 seconds are measured from 10 Rime logs, not from game data.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Further_Application_Adds_Two_Seconds_To_The_Time_Left(GearHero hero)
    {
        var f = hero.Fight();
        f.Hero.Auras.Apply(Hero.RisingSpirit, f.Now);
        f.Wait(4);
        f.Hero.Auras.Apply(Hero.RisingSpirit, f.Now);
        f.Wait(7.9);
        Assert.Equal(2, f.SharedStacks(Hero.RisingSpirit));
        f.Wait(0.2);
        Assert.Equal(0, f.SharedStacks(Hero.RisingSpirit));
    }
}
