using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// The Diamond gem on every hero: Harmonious Soul (+0.6% crit, haste, Expertise and Spirit per stack for 5s when an enemy is defeated, up to 4 stacks,
/// 12 at rank II, one lost each time the duration runs out), Stoic's Teachings (+4 / +12 Main Stat), Ancient's Wisdom (+2% / +6% Main Stat);
/// Tranquil Resolve defends and Blessing of the Artisan shortens relic cooldowns.
/// Dev: no character sheet. Kills are made with <see cref="GemFights.Defeat"/>, with enough enemies for the test. Stat nodes are read at the two powers around
/// their threshold; Main Stat is 1000 and Ancient's Wisdom multiplies it, so values are literals such as (1000 + 4) x 1.02.
/// </summary>
public class GemDiamond
{
    private const GemColor Color = GemColor.Diamond;
    private const double Tolerance = 1e-9;

    private static Diamond Gem(StepDriver f) => (Diamond)f.TheGem();

    private static int Stacks(StepDriver f) => f.Hero.Auras.GetStacks(Gem(f).HarmoniousSoul!, f.Now);

    private static void Kill(StepDriver f, int count)
    {
        for (int i = 0; i < count; i++) f.Defeat(f.Context.ActiveEnemies[0]);
    }

    /// <summary>
    /// Player: each enemy defeated gives a stack of Harmonious Soul: +0.6% critical strike, haste, Expertise and Spirit per stack.
    /// Dev: three kills, the four stats before and after, at 80 and 600 gem power (the other nodes are the same on both sides).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Harmonious_Soul_Gives_Stats_Per_Stack(GearHero hero)
    {
        foreach (double power in new[] { 80.0, 600.0 })
        {
            var f = GemFights.Fight(hero, Color, power, enemies: 4);
            double crit = f.Crit(), haste = f.Haste(), expertise = f.ExpertiseNow(), spirit = f.SpiritNow();
            Kill(f, 3);
            Assert.Equal(3, Stacks(f));
            Assert.Equal(0.018, f.Crit() - crit, Tolerance);
            Assert.Equal(0.018, f.Haste() - haste, Tolerance);
            Assert.Equal(0.018, f.ExpertiseNow() - expertise, Tolerance);
            Assert.Equal(0.018, f.SpiritNow() - spirit, Tolerance);
        }
    }

    /// <summary>
    /// Player: Harmonious Soul stacks up to 4 times (12 at rank II).
    /// Dev: more kills than that leave 4 stacks at 80 gem power and 12 at 600.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Harmonious_Soul_Stacks_Up_To_Its_Maximum(GearHero hero)
    {
        var rankOne = GemFights.Fight(hero, Color, 80, enemies: 6);
        Kill(rankOne, 6);
        Assert.Equal(4, Stacks(rankOne));
        var rankTwo = GemFights.Fight(hero, Color, 600, enemies: 14);
        Kill(rankTwo, 14);
        Assert.Equal(12, Stacks(rankTwo));
    }

    /// <summary>
    /// Player: when Harmonious Soul's 5 seconds run out you lose one stack, and the 5 seconds start again for the rest.
    /// Dev: four kills at 0s: 4 stacks at 4.9s, 3 at 5.1s, 2 at 10.1s, 1 at 15.1s, none at 20.1s.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Harmonious_Soul_Loses_One_Stack_Every_Five_Seconds(GearHero hero)
    {
        var f = GemFights.Fight(hero, Color, 80, enemies: 4);
        Kill(f, 4);
        foreach (var (time, stacks) in new[] { (4.9, 4), (5.1, 3), (10.1, 2), (15.1, 1), (20.1, 0) })
        {
            f.Wait(time - f.Now.TotalSeconds);
            Assert.Equal(stacks, Stacks(f));
        }
    }

    /// <summary>
    /// Player: a kill adds a stack and restarts the 5 seconds for all of them.
    /// Dev: a kill at 0s and another at 3s: 2 stacks at 7.9s (5s after the second kill, not the first), 1 at 8.1s.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void A_Kill_Refreshes_Harmonious_Soul(GearHero hero)
    {
        var f = GemFights.Fight(hero, Color, 80, enemies: 2);
        Kill(f, 1);
        f.Wait(3);
        Kill(f, 1);
        Assert.Equal(2, Stacks(f));
        f.Wait(7.9 - f.Now.TotalSeconds);
        Assert.Equal(2, Stacks(f));
        f.Wait(8.1 - f.Now.TotalSeconds);
        Assert.Equal(1, Stacks(f));
    }

    /// <summary>
    /// Player: only a defeated enemy gives a stack, not damage.
    /// Dev: a hit with no kill leaves no stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Damage_Alone_Gives_No_Harmonious_Soul(GearHero hero)
    {
        var f = GemFights.Fight(hero, Color, 80);
        hero.CastProbe(f, hero.Basic);
        Assert.Equal(0, Stacks(f));
    }

    /// <summary>
    /// Player: Stoic's Teachings gives 4 Main Stat (12 at rank II); Ancient's Wisdom's percentage applies to it.
    /// Dev: absolute Main Stat at 149 / 150 gem power (1000 and 1004) and 799 / 800 (1004 x 1.02 and 1012 x 1.02).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Stoics_Teachings_Adds_Main_Stat(GearHero hero)
    {
        Assert.Equal(1000.0, GemFights.Fight(hero, Color, 149).Main(), 1e-6);
        Assert.Equal(1004.0, GemFights.Fight(hero, Color, 150).Main(), 1e-6);
        Assert.Equal(1024.08, GemFights.Fight(hero, Color, 799).Main(), 1e-6);
        Assert.Equal(1032.24, GemFights.Fight(hero, Color, 800).Main(), 1e-6);
    }

    /// <summary>
    /// Player: Ancient's Wisdom gives 2% more Main Stat (6% at rank II).
    /// Dev: Main Stat at 299 / 300 gem power is x1.02 (Stoic's Teachings' 4 is in both) and at 1249 / 1250 x(1.06 / 1.02).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Ancients_Wisdom_Multiplies_Main_Stat(GearHero hero)
    {
        Assert.Equal(1.02, GemFights.Ratio(hero, Color, 299, 300, GemFights.Main), 1e-9);
        Assert.Equal(1.06 / 1.02, GemFights.Ratio(hero, Color, 1249, 1250, GemFights.Main), 1e-9);
    }

    /// <summary>
    /// Player: Tranquil Resolve (defensive) and Blessing of the Artisan (relic cooldowns) do not change your damage.
    /// Dev: the unit hit is the same at 219 / 220 and 449 / 450 gem power.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Defensive_And_Relic_Nodes_Do_Not_Change_Damage(GearHero hero)
    {
        Assert.Equal(GemFights.UnitHit(hero, Color, 219), GemFights.UnitHit(hero, Color, 220), 1e-9);
        Assert.Equal(GemFights.UnitHit(hero, Color, 449), GemFights.UnitHit(hero, Color, 450), 1e-9);
    }
}
