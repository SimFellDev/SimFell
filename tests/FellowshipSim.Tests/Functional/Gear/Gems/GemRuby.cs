using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// The Ruby gem on every hero: Might of the Minotaur (+2% / +6% Main Stat), Champion's Heart (+2 / +6 Main Stat), Blessing of the Conqueror (+5% /
/// +15% damage); Unyielding Vitality and Titan's Blood only heal and add Stamina.
/// Dev: the hero has no character sheet, so the engine adds the node's stats itself. A node is read as the difference between the power that unlocks it
/// and the power just below (rank II nodes: from rank I to rank II). Main Stat is 1000 and Ruby's Might applies to it, so values are
/// literals such as (1000 + 2) x 1.02.
/// </summary>
public class GemRuby
{
    private const double Ratio = 0.001;
    private const GemColor Color = GemColor.Ruby;

    private static double Hit(GearHero hero, double power) => GemFights.UnitHit(hero, Color, power);

    /// <summary>
    /// Player: Might of the Minotaur gives 2% more Main Stat (6% at rank II).
    /// Dev: 79 to 80 gem power (79 has no node) and 599 to 600; the other nodes are the same on both sides.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Might_Of_The_Minotaur_Multiplies_Main_Stat(GearHero hero)
    {
        Assert.Equal(1.02, GemFights.Ratio(hero, Color, 79, 80, GemFights.Main), 1e-9);
        Assert.Equal(1.06 / 1.02, GemFights.Ratio(hero, Color, 599, 600, GemFights.Main), 1e-9);
    }

    /// <summary>
    /// Player: Champion's Heart gives 2 Main Stat (6 at rank II); Might of the Minotaur's percentage applies to it.
    /// Dev: absolute Main Stat at 149 / 150 (1000 x 1.02 and 1002 x 1.02) and at 799 / 800 (1002 x 1.06 and 1006 x 1.06).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Champions_Heart_Adds_Main_Stat(GearHero hero)
    {
        Assert.Equal(1020.0, GemFights.Fight(hero, Color, 149).Main(), 1e-6);
        Assert.Equal(1022.04, GemFights.Fight(hero, Color, 150).Main(), 1e-6);
        Assert.Equal(1062.12, GemFights.Fight(hero, Color, 799).Main(), 1e-6);
        Assert.Equal(1066.36, GemFights.Fight(hero, Color, 800).Main(), 1e-6);
    }

    /// <summary>
    /// Player: Blessing of the Conqueror makes your damage 5% higher (15% at rank II).
    /// Dev: the unit hit at 449 and 450 gem power, and at 1499 and 1500; ratios 1.05 and 1.15 / 1.05.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Blessing_Of_The_Conqueror_Raises_Damage(GearHero hero)
    {
        Assert.Equal(1.05, Hit(hero, 450) / Hit(hero, 449), Ratio);
        Assert.Equal(1.15 / 1.05, Hit(hero, 1500) / Hit(hero, 1499), Ratio);
    }

    /// <summary>
    /// Player: Unyielding Vitality (healing) and Titan's Blood (Stamina) do not change the damage you deal.
    /// Dev: the unit hit is the same at 220 / 219, 300 / 299, 1000 / 999 and 1250 / 1249 gem power.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Defensive_Nodes_Do_Not_Change_Damage(GearHero hero)
    {
        foreach (double power in new[] { 220.0, 300.0, 1000.0, 1250.0 })
            Assert.Equal(Hit(hero, power - 1), Hit(hero, power), 1e-9);
    }

    /// <summary>
    /// Player: a character sheet already includes the gem's Main Stat, so wearing the gem changes nothing on a hero built from one.
    /// Dev: with a sheet, Main Stat is 1000 at every power (the engine takes the node's percentage out of the sheet value and applies it as the aura).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void A_Character_Sheet_Already_Includes_The_Gem(GearHero hero)
    {
        foreach (double power in new[] { 80.0, 150.0, 600.0, 1500.0 })
            Assert.Equal(1000.0, GemFights.Fight(hero, Color, power, sheet: true).Main(), 1e-6);
    }

    /// <summary>The auras of Might of the Minotaur and Blessing of the Conqueror at both ranks, and the power that unlocks each rank.</summary>
    public static IEnumerable<object[]> SituationalRanks() =>
        from hero in GearFights.Heroes
        from rank in new (double Low, double High, int NodeLow, int NodeHigh)[] { (80, 600, 0, 5), (450, 1500, 4, 9) }
        select new object[] { hero, rank.Low, rank.High, rank.NodeLow, rank.NodeHigh };

    /// <summary>
    /// Player: when the gem's power raises a situational node (Might of the Minotaur or Blessing of the Conqueror) to its next rank, the new rank is
    /// up if the old one was up.
    /// Dev: bound at the rank I power and raised to the rank II power at 5s; the new aura is up and the old one gone, and the stats are those of a hero
    /// bound at rank II.
    /// </summary>
    [Theory]
    [MemberData(nameof(SituationalRanks))]
    public void A_Rank_Change_Keeps_A_Situational_Buff_That_Is_Up(GearHero hero, double low, double high, int nodeLow, int nodeHigh)
    {
        var (oldAura, newAura) = (Ruby.AllNodes[nodeLow].Grants!, Ruby.AllNodes[nodeHigh].Grants!);
        var f = GemFights.Fight(hero, Color, low);
        f.Wait(5);
        Assert.True(f.Hero.Auras.HasAura(oldAura, f.Now));
        f.TheGem().ChangePower(high, f.Now);
        Assert.False(f.Hero.Auras.HasAura(oldAura, f.Now));
        Assert.True(f.Hero.Auras.HasAura(newAura, f.Now));
        var fresh = GemFights.Fight(hero, Color, high);
        fresh.Wait(5);
        Assert.Equal(fresh.Main(), f.Main(), 1e-9);
    }

    /// <summary>
    /// Player: when the gem's power raises a situational node to its next rank while its buff is off (Might of the Minotaur below 80% health), the
    /// new rank is off too: the power change does not bring the buff back.
    /// Dev: bound at the rank I power, the aura removed as the log would at 5s, then raised to the rank II power: neither aura is up, and the stats are
    /// those of a hero bound at rank II with its aura removed.
    /// </summary>
    [Theory]
    [MemberData(nameof(SituationalRanks))]
    public void A_Rank_Change_Does_Not_Bring_Back_A_Situational_Buff_That_Is_Off(GearHero hero, double low, double high, int nodeLow, int nodeHigh)
    {
        var (oldAura, newAura) = (Ruby.AllNodes[nodeLow].Grants!, Ruby.AllNodes[nodeHigh].Grants!);
        var f = GemFights.Fight(hero, Color, low);
        f.Wait(5);
        f.Hero.Auras.Remove(oldAura, f.Now);
        f.TheGem().ChangePower(high, f.Now);
        Assert.False(f.Hero.Auras.HasAura(oldAura, f.Now));
        Assert.False(f.Hero.Auras.HasAura(newAura, f.Now));
        var fresh = GemFights.Fight(hero, Color, high);
        fresh.Wait(5);
        fresh.Hero.Auras.Remove(newAura, fresh.Now);
        Assert.Equal(fresh.Main(), f.Main(), 1e-9);
    }

    /// <summary>
    /// Player: Might of the Minotaur applies only while you are above 80% health.
    /// Dev: not testable: the engine does not track the hero's health and keeps the node up from the start of the fight.
    /// </summary>
    [Fact(Skip = "The engine does not track the hero's health: Might of the Minotaur is always up")]
    public void Might_Of_The_Minotaur_Needs_Health_Above_80_Percent()
    {
        // TODO: needs a hero health model (check in game that the 80% threshold is "above" or "at least").
    }

    /// <summary>
    /// Player: Blessing of the Conqueror applies only while you fight a boss.
    /// Dev: not testable: the engine applies it from the start of every fight (a situational aura).
    /// </summary>
    [Fact(Skip = "The engine applies Blessing of the Conqueror to every fight")]
    public void Blessing_Of_The_Conqueror_Needs_A_Boss()
    {
        // TODO: check in game what counts as a boss, and whether the +5% / +15% is also up against trash.
    }
}
