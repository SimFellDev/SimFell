using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// The Amethyst gem on every hero: Sealed Fate (+5% / +15% crit chance against enemies above 50% health), Killer Instinct (+2% / +6% crit chance),
/// Blessing of the Deathdealer (+2% / +6% crit power); Berserker's Zeal adds crit rating and Reaper's Reprieve heals.
/// Dev: no character sheet, so the engine adds the node's stats itself; a node is the difference between the power that unlocks it and the one below.
/// Base crit chance and crit power (5% and x2.0) are read from the hero, not assumed.
/// </summary>
public class GemAmethyst
{
    private const double Ratio = 0.001;
    private const GemColor Color = GemColor.Amethyst;

    private static double CritAgainst(StepDriver f, double healthFraction)
    {
        f.Enemy.CurrentHealth = f.Enemy.MaxHealth * healthFraction;
        return f.Hero.CritChanceAgainst(f.Enemy, f.Now);
    }

    /// <summary>
    /// Player: Sealed Fate gives +5% critical strike chance (+15% at rank II) against enemies above 50% health, and nothing against the others.
    /// Dev: crit chance against the enemy at 100%, 51% and 49% health, 79 / 80 gem power, then 599 / 600 (rank I to rank II: +10%). The exact 50% is not tested.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Sealed_Fate_Adds_Crit_Chance_Against_Healthy_Enemies(GearHero hero)
    {
        foreach (double health in new[] { 1.0, 0.51 })
        {
            Assert.Equal(0.05, CritAgainst(GemFights.Fight(hero, Color, 80), health) - CritAgainst(GemFights.Fight(hero, Color, 79), health), 1e-9);
            Assert.Equal(0.10, CritAgainst(GemFights.Fight(hero, Color, 600), health) - CritAgainst(GemFights.Fight(hero, Color, 599), health), 1e-9);
        }
        Assert.Equal(0.0, CritAgainst(GemFights.Fight(hero, Color, 80), 0.49) - CritAgainst(GemFights.Fight(hero, Color, 79), 0.49), 1e-9);
        Assert.Equal(0.0, CritAgainst(GemFights.Fight(hero, Color, 600), 0.49) - CritAgainst(GemFights.Fight(hero, Color, 599), 0.49), 1e-9);
    }

    /// <summary>
    /// Player: the crit chance Sealed Fate gives counts in the hit's crit chance, so the hit's expected damage rises with it.
    /// Dev: the crit chance of the first hit of the hero's Core ability (Tariq's Wild Swing cannot crit, so his Basic is no probe) on a full-health enemy rises by 0.05 from 79 to 80 gem power, and not on an enemy at 49% health.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Sealed_Fate_Is_In_The_Hits_Crit_Chance(GearHero hero)
    {
        double Chance(double power, double health)
        {
            var f = GemFights.Fight(hero, Color, power);
            f.Enemy.CurrentHealth = f.Enemy.MaxHealth * health;
            hero.CastProbe(f, hero.Core);
            f.Wait(5);
            return f.Events.First(e => e.Kind == StepEventKind.Damage && e.Value > 0).CritChance;
        }
        Assert.Equal(0.05, Chance(80, 1.0) - Chance(79, 1.0), 1e-9);
        Assert.Equal(0.0, Chance(80, 0.49) - Chance(79, 0.49), 1e-9);
    }

    /// <summary>
    /// Player: Killer Instinct gives +2% critical strike chance (+6% at rank II).
    /// Dev: crit chance at 299 / 300 gem power (+0.02) and 1249 / 1250 (rank I to rank II: +0.04).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Killer_Instinct_Adds_Crit_Chance(GearHero hero)
    {
        Assert.Equal(0.02, GemFights.Delta(hero, Color, 299, 300, GemFights.Crit), 1e-9);
        Assert.Equal(0.04, GemFights.Delta(hero, Color, 1249, 1250, GemFights.Crit), 1e-9);
    }

    /// <summary>
    /// Player: Blessing of the Deathdealer makes critical strikes hit 2% harder (6% at rank II): the crit multiplier is multiplied.
    /// Dev: the crit multiplier at 449 / 450 gem power is x1.02 and at 1499 / 1500 x(1.06 / 1.02).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Blessing_Of_The_Deathdealer_Raises_Crit_Power(GearHero hero)
    {
        Assert.Equal(1.02, GemFights.Ratio(hero, Color, 449, 450, GemFights.CritPower), 1e-9);
        Assert.Equal(1.06 / 1.02, GemFights.Ratio(hero, Color, 1499, 1500, GemFights.CritPower), 1e-9);
    }

    /// <summary>
    /// Player: Reaper's Reprieve (healing) does not change your damage or crit.
    /// Dev: the unit hit is the same at 219 and 220 gem power, and at 999 and 1000.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Reapers_Reprieve_Does_Not_Change_Damage(GearHero hero)
    {
        Assert.Equal(GemFights.UnitHit(hero, Color, 219), GemFights.UnitHit(hero, Color, 220), 1e-9);
        Assert.Equal(GemFights.UnitHit(hero, Color, 999), GemFights.UnitHit(hero, Color, 1000), 1e-9);
    }

    /// <summary>
    /// Player: Berserker's Zeal gives 8 critical strike rating (24 at rank II).
    /// Dev: a hero built from gem power (no character sheet, so nothing else gives rating): the crit chance at the node's power over the one just below is the
    /// percentage of 8 rating (rank II: of 24 less 8, as it replaces the rank I node), through the stat's diminishing returns.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Berserkers_Zeal_Adds_Crit_Rating(GearHero hero)
    {
        Assert.Equal(StatDiminishingReturns.FromRating(8), GemFights.Delta(hero, Color, 149, 150, GemFights.Crit), 1e-9);
        Assert.Equal(StatDiminishingReturns.FromRating(24) - StatDiminishingReturns.FromRating(8), GemFights.Delta(hero, Color, 799, 800, GemFights.Crit), 1e-9);
    }
}
