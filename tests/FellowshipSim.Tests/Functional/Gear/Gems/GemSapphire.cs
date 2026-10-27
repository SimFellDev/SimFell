using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// The Sapphire gem on every hero: Ancestral Surge (+10 / +30 Spirit Points maximum, +8% / +24% Main Stat during Spirit of Heroism), Oracle's Foresight
/// (+2% / +6% Spirit), Blessing of the Prophet (Spirit ability costs 5% / 15% less, Spirit of Heroism lasts 6 / 18 seconds longer); Mystic's Intuition adds
/// Spirit rating and Resonating Soul reduces damage taken.
/// Dev: no character sheet. A node is the difference between the power that unlocks it and the one just below; Spirit of Heroism is started with the
/// hero's Spirit ability and read through its aura events.
/// </summary>
public class GemSapphire
{
    private const GemColor Color = GemColor.Sapphire;

    private static double MaxSpiritPoints(StepDriver f) => f.Hero.Resources.Get(Hero.SpiritPoints).Max;

    /// <summary>
    /// Player: Ancestral Surge raises your maximum Spirit Points by 10 (30 at rank II).
    /// Dev: the Spirit Points pool's maximum at 79, 80, 599 and 600 gem power: 100, 110, 110 and 130.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Ancestral_Surge_Raises_Maximum_Spirit_Points(GearHero hero)
    {
        Assert.Equal(100.0, MaxSpiritPoints(GemFights.Fight(hero, Color, 79)));
        Assert.Equal(110.0, MaxSpiritPoints(GemFights.Fight(hero, Color, 80)));
        Assert.Equal(110.0, MaxSpiritPoints(GemFights.Fight(hero, Color, 599)));
        Assert.Equal(130.0, MaxSpiritPoints(GemFights.Fight(hero, Color, 600)));
    }

    /// <summary>
    /// Player: while Spirit of Heroism is active Ancestral Surge gives +8% Main Stat (+24% at rank II); before and after it gives nothing.
    /// Dev: Main Stat of a hero with the node over a hero without it (79 against 80 gem power, 599 against 600: rank I to rank II), before the Spirit
    /// ability, right after it, and once Spirit of Heroism has ended.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Ancestral_Surge_Gives_Main_Stat_During_Spirit_Of_Heroism(GearHero hero)
    {
        foreach (var (low, high, ratio) in new[] { (79.0, 80.0, 1.08), (599.0, 600.0, 1.24 / 1.08) })
        {
            var a = GemFights.Fight(hero, Color, low);
            var b = GemFights.Fight(hero, Color, high);
            Assert.Equal(1.0, b.Main() / a.Main(), 1e-9);
            a.StartHeroism(hero);
            b.StartHeroism(hero);
            Assert.Equal(ratio, b.Main() / a.Main(), 1e-9);
            a.Wait(60);
            b.Wait(60);
            Assert.Equal(1.0, b.Main() / a.Main(), 1e-9);
        }
    }

    /// <summary>
    /// Player: Oracle's Foresight gives +2% Spirit (+6% at rank II).
    /// Dev: Spirit at 299 / 300 gem power (+0.02) and 1249 / 1250 (rank I to rank II: +0.04).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Oracles_Foresight_Adds_Spirit(GearHero hero)
    {
        Assert.Equal(0.02, GemFights.Delta(hero, Color, 299, 300, GemFights.SpiritNow), 1e-9);
        Assert.Equal(0.04, GemFights.Delta(hero, Color, 1249, 1250, GemFights.SpiritNow), 1e-9);
    }

    /// <summary>
    /// Player: Blessing of the Prophet makes your Spirit ability cost 5% less (15% at rank II): 95 Spirit Points, 85 at rank II.
    /// Dev: with exactly that many Spirit Points (and every other resource full) the cast goes through, with one less it is refused; without the node
    /// (449, 1499 for rank I) the old price still holds.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Blessing_Of_The_Prophet_Makes_The_Spirit_Ability_Cheaper(GearHero hero)
    {
        bool CanCast(double power, double points)
        {
            var f = GemFights.Fight(hero, Color, power);
            GearFights.Fill(f);
            f.Hero.Resources.Get(Hero.SpiritPoints).Current = points;
            return f.TryCast(hero.Get(f, hero.Spirit)).Succeeded;
        }
        Assert.False(CanCast(449, 99));
        Assert.True(CanCast(449, 100));
        Assert.False(CanCast(450, 94));
        Assert.True(CanCast(450, 95));
        Assert.False(CanCast(1500, 84));
        Assert.True(CanCast(1500, 85));
        Assert.False(CanCast(1499, 94));
        Assert.True(CanCast(1499, 95));
    }

    /// <summary>
    /// Player: Blessing of the Prophet lengthens Spirit of Heroism by 6 seconds (18 at rank II): 26 and 38 seconds instead of 20.
    /// Dev: the aura's gain and removal times at 449, 450 and 1500 gem power (tolerance 50ms: the aura ends on the server's frame).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Blessing_Of_The_Prophet_Lengthens_Spirit_Of_Heroism(GearHero hero)
    {
        double Duration(double power)
        {
            var f = GemFights.Fight(hero, Color, power);
            f.StartHeroism(hero);
            f.Wait(60);
            var gained = f.Auras(StepEventKind.AuraGained, "Spirit of Heroism").First();
            var removed = f.Auras(StepEventKind.AuraRemoved, "Spirit of Heroism").First();
            return (removed.Time - gained.Time).TotalSeconds;
        }
        Assert.Equal(20.0, Duration(449), 0.05);
        Assert.Equal(26.0, Duration(450), 0.05);
        Assert.Equal(38.0, Duration(1500), 0.05);
    }

    /// <summary>
    /// Player: Resonating Soul (damage reduction) does not change your damage.
    /// Dev: the unit hit is the same at 219 / 220 and 999 / 1000 gem power.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Resonating_Soul_Does_Not_Change_Damage(GearHero hero)
    {
        Assert.Equal(GemFights.UnitHit(hero, Color, 219), GemFights.UnitHit(hero, Color, 220), 1e-9);
        Assert.Equal(GemFights.UnitHit(hero, Color, 999), GemFights.UnitHit(hero, Color, 1000), 1e-9);
    }

    /// <summary>
    /// Player: Mystic's Intuition gives 8 Spirit rating (24 at rank II).
    /// Dev: a hero built from gem power (no character sheet, so nothing else gives rating): the Spirit at the node's power over the one just below is the
    /// percentage of 8 rating (rank II: of 24 less 8, as it replaces the rank I node), through the stat's diminishing returns.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Mystics_Intuition_Adds_Spirit_Rating(GearHero hero)
    {
        Assert.Equal(StatDiminishingReturns.FromRating(8), GemFights.Delta(hero, Color, 149, 150, GemFights.SpiritNow), 1e-9);
        Assert.Equal(StatDiminishingReturns.FromRating(24) - StatDiminishingReturns.FromRating(8), GemFights.Delta(hero, Color, 799, 800, GemFights.SpiritNow), 1e-9);
    }
}
