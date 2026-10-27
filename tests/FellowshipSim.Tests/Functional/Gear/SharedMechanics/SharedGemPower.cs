using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional.GearShared;

/// <summary>
/// Covetous Greed (Orb of Hidden Riches): "Your Gem Power is increased by 25%". A character sheet's gem power already includes it, so it
/// applies only to builds that are not from a sheet (gem power given by the gems and gear).
/// Dev: a hand-written build with no sheet values (as <see cref="GearHero.Fight"/> builds always have percentages, they are sheet builds): the gem
/// power on the bound gem is read.
/// </summary>
public class SharedGemPower
{
    public static IEnumerable<object[]> Heroes => SharedFights.Heroes;

    private static double RubyPower(GearHero hero, bool sheet, params int[] relics)
    {
        var build = new CharacterBuild { Name = "Test " + hero.Name, Hero = hero.Name, PrimaryStat = 1000 };
        if (sheet) build.SheetPercents["haste"] = 0.0;
        build.GemPower[GemColor.Ruby] = 1000;
        if (relics.Length > 0) build.Relic1ItemId = relics[0];
        if (relics.Length > 1) build.Relic2ItemId = relics[1];
        var driver = new StepDriver(HeroImporter.Import(build));
        return driver.Hero.Gems.Single().Power;
    }

    /// <summary>Player: without Covetous Greed gem power is what the gems give. Dev: 1000 stays 1000.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Without_The_Relic_Gem_Power_Is_Unchanged(GearHero hero) => Assert.Equal(1000, RubyPower(hero, sheet: false), 1e-9);

    /// <summary>Player: Covetous Greed raises every colour's gem power by 25%. Dev: 1000 becomes 1250, in either relic slot.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Covetous_Greed_Adds_A_Quarter(GearHero hero)
    {
        Assert.Equal(1250, RubyPower(hero, sheet: false, CovetousGreed.OrbItemId), 1e-9);
        Assert.Equal(1250, RubyPower(hero, sheet: false, 0, CovetousGreed.OrbItemId), 1e-9);
    }

    /// <summary>Player: a character sheet's gem power already has Covetous Greed in it, so it is not added again. Dev: a sheet build keeps its 1000.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Sheet_Build_Is_Not_Raised_Again(GearHero hero) => Assert.Equal(1000, RubyPower(hero, sheet: true, CovetousGreed.OrbItemId), 1e-9);

    /// <summary>
    /// Player: other relics do not change gem power.
    /// Dev: Undying Heart's orb; 1000 stays 1000.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Other_Relics_Do_Not_Change_It(GearHero hero) => Assert.Equal(1000, RubyPower(hero, sheet: false, UndyingHeart.OrbItemId), 1e-9);
}
