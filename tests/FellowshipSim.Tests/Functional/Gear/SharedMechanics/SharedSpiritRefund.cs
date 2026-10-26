using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearShared;

/// <summary>
/// Spirit Refund, which every hero has: a chance of Spirit / (1 + Spirit) each time the hero spends its resource (Gunde: each time a cast applies
/// Rend), which gives the resource back a moment later, a stack of Rising Spirit and Spirit Points.
/// Dev: the same scenario over all six heroes: a fresh hero with every resource full and no Spirit Points casts its Power ability (the hero's
/// spender), with the refund forced on or off. Spirit Points are read straight after the cast and a moment more (under 3 seconds, so before the
/// first passive Spirit Point).
/// </summary>
public class SharedSpiritRefund
{
    public static IEnumerable<object[]> Heroes => SharedFights.Heroes;
    public static IEnumerable<object[]> HeroesWithASpender => SharedFights.HeroesWithASpender;

    /// <summary>
    /// Player: a Spirit Refund gives you a stack of Rising Spirit and Spirit Points (2 for Rime, 1 for the others).
    /// Dev: refund forced on, one cast of the Power ability, then 0.5 seconds for the refund to come.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Spirit_Refund_Gives_Rising_Spirit_And_Spirit_Points(GearHero hero)
    {
        var f = SharedFights.Ready(hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SharedCastRefund(hero);
        f.Wait(0.5);
        Assert.Equal(1, f.SharedStacks(Hero.RisingSpirit));
        Assert.Equal(SharedFights.SpiritPointsPerRefund(hero), f.SharedSpirit());
    }

    /// <summary>
    /// Player: without a Spirit Refund you get no Rising Spirit and no Spirit Points.
    /// Dev: refund forced off.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Without_A_Spirit_Refund_There_Is_Nothing(GearHero hero)
    {
        var f = SharedFights.Ready(hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.SharedCastRefund(hero);
        f.Wait(0.5);
        Assert.Equal(0, f.SharedStacks(Hero.RisingSpirit));
        Assert.Equal(0, f.SharedSpirit());
    }

    /// <summary>
    /// Player: a Spirit Refund gives back what the cast spent, a moment (about 0.2 seconds) later.
    /// Dev: refund forced on; the resource amount the cast spent is read from the events; the hero's resource is full again after 0.5 seconds but
    /// not at the moment of the spend. Gunde has no spent resource.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroesWithASpender))]
    public void A_Spirit_Refund_Gives_The_Spent_Resource_Back(GearHero hero)
    {
        var f = SharedFights.Ready(hero);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SharedCastRefund(hero);
        var spent = f.Events.Where(e => e.Kind == StepEventKind.ResourceSpent && e.Name != "Spirit").ToList();
        Assert.NotEmpty(spent);
        f.Wait(0.5);
        foreach (var pool in spent.Select(s => s.Name).Distinct())
        {
            var def = f.Hero.ResourceDefs.First(r => r.Name == pool);
            Assert.Equal(def.Max, f.Hero.ResourceAmount(def), 1e-6);
        }
    }

    /// <summary>
    /// Player: you get a Spirit Refund with a chance of Spirit / (1 + Spirit): at 20% Spirit one in six.
    /// Dev: 20% Spirit, refund forced off so only the chance is read; the engine's chance for the roll of the cast is 0.2 / 1.2.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Chance_Is_Spirit_Over_One_Plus_Spirit(GearHero hero)
    {
        var f = SharedFights.Ready(hero, new GearLoadout { Spirit = 0.2 });
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.SharedCastRefund(hero);
        var rolls = procs.Rolls.Where(r => r.Key == Hero.SpiritRefundProc).ToList();
        Assert.NotEmpty(rolls);
        Assert.All(rolls, r => Assert.Equal(0.2 / 1.2, r.Chance, 1e-9));
    }

    /// <summary>
    /// Player: with no Spirit there is no chance of a refund.
    /// Dev: 0% Spirit; the roll's chance is 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void No_Spirit_Means_No_Chance(GearHero hero)
    {
        var f = SharedFights.Ready(hero);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.SharedCastRefund(hero);
        Assert.All(procs.Rolls.Where(r => r.Key == Hero.SpiritRefundProc), r => Assert.Equal(0.0, r.Chance, 1e-9));
    }

    /// <summary>
    /// Player: Spirit Points never go past 100.
    /// Dev: 100 Spirit Points, refund forced on; still 100 after the refund.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Spirit_Points_Stop_At_The_Cap(GearHero hero)
    {
        var f = SharedFights.Ready(hero);
        f.SharedSetSpirit(100);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SharedCastRefund(hero);
        f.Wait(0.5);
        Assert.Equal(100, f.SharedSpirit());
    }
}
