using FellowshipSim.Core;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Import;
using FellowshipSim.UI.Build;

namespace FellowshipSim.Tests;

/// <summary>
/// Builds of the heroes the engine does not have yet (<see cref="BuildHeroes"/>): they edit, save and share as the
/// engine's heroes do, and never simulate. The engine's heroes' builds and links stay as they were.
/// </summary>
public class BuildOnlyHeroTests
{
    private const string Site = "https://simfelldev.github.io/";

    /// <summary>A Sylvie build: 1a (Nettle to the Petal, 2 points) and 2a (Bloomin' Boomshrooms, 1 point).</summary>
    private static CharacterBuild Sylvie() => new()
    {
        Hero = "Sylvie",
        Name = "Flutter",
        PrimaryStat = 161,
        Stamina = 306,
        CritRating = 89,
        HasteRating = 71,
        Talents = [176, 32],
        Relic1ItemId = 5921,
    };

    [Fact]
    public void Every_Hero_Of_The_Game_Has_A_Tree_And_Main_Stat()
    {
        Assert.Equal(12, BuildHeroes.Names.Count);
        foreach (string hero in BuildHeroes.Names)
        {
            Assert.Equal(18, BuildHeroes.Create(hero).TalentTreeSlots.Count);
            Assert.Equal(18, BuildCatalog.For(hero).Talents.Count);
        }
    }

    /// <summary>
    /// The engine's trees are the game's, but for a talent the engine knows by its combat-log ID: Mara's Macabre Stratagem
    /// (game data talent 8, combat logs talent 10000).
    /// </summary>
    [Fact]
    public void The_Engines_Trees_Match_The_Game_Data()
    {
        var logIds = new Dictionary<int, int> { [8] = 10000 };
        foreach (string hero in Catalog.Heroes.Keys)
        {
            var game = GameHeroData.TalentTrees[hero].Select(s => s with { TalentId = logIds.GetValueOrDefault(s.TalentId, s.TalentId) });
            Assert.Equal(game, Catalog.CreateHero(hero).TalentTreeSlots);
            Assert.Equal(GameHeroData.MainStats[hero], Catalog.CreateHero(hero).MainStat);
        }
    }

    [Fact]
    public void A_Build_Only_Hero_Is_Not_In_The_Engine()
    {
        Assert.True(BuildHeroes.IsBuildOnly("Sylvie"));
        Assert.False(BuildHeroes.IsBuildOnly("Rime"));
        Assert.DoesNotContain("Sylvie", Catalog.Heroes.Keys);
        Assert.DoesNotContain(Catalog.Heroes.Values, t => t == typeof(BuildOnlyHero));
        Assert.Throws<NotSupportedException>(() => HeroImporter.Import(Sylvie()));
    }

    [Fact]
    public void Its_Talents_Have_The_Games_Names_And_Are_Not_Modeled()
    {
        var talent = BuildCatalog.For("Sylvie").Talents.Single(t => t.Slot.TalentId == 176);

        Assert.Equal("Nettle to the Petal", talent.Talent.Name);
        Assert.False(talent.Talent.Modeled);
        Assert.Equal(BuildHeroes.Create("Rime").TalentTreeSlots.Count, BuildCatalog.For("Rime").Talents.Count(t => t.Talent.Modeled));
    }

    [Fact]
    public void Its_Build_Survives_A_Profile_And_A_Share_Link()
    {
        string profile = new SimcProfile(Sylvie(), "").Format();
        Assert.Contains("sylvie=\"Flutter\"", profile);
        Assert.Contains("talents=nettle_to_the_petal/bloomin_boomshrooms", profile);

        var fromProfile = SimcProfile.Parse(profile).Build;
        Assert.Equal("Sylvie", fromProfile.Hero);
        Assert.Equal([176, 32], fromProfile.Talents);
        Assert.Equal(profile, new SimcProfile(fromProfile, "").Format());

        var fromLink = BuildShareLink.TryRead(BuildShareLink.Create(Site, Sylvie()))!;
        Assert.Equal("Sylvie", fromLink.Hero);
        Assert.Equal("Flutter", fromLink.Name);
        Assert.Equal([32, 176], fromLink.Talents.Order());
        Assert.Equal(5921, fromLink.Relic1ItemId);
    }

    [Fact]
    public void Every_Hero_Has_Its_Four_Weapons()
    {
        foreach (string hero in BuildHeroes.Names)
        {
            var expected = GameIcons.WeaponHeroes.Where(w => w.Value.Contains(hero)).Select(w => w.Key).Order();
            var weapons = BuildCatalog.For(hero).Weapons;
            Assert.Equal(expected, weapons.Select(w => w.Id).Order());
            Assert.Equal(4, weapons.Select(w => w.Name).Distinct().Count());
        }
        Assert.Equal(["Asha's Chronoshift Spire", "Branch of the Everliving", "Frozen Staff of An'zhyr", "Void-touched Conduit"],
            BuildCatalog.For("Rime").Weapons.Select(w => w.Name));
    }

    /// <summary>Tariq's Earthbreaker weapon (ability 1559), which the engine does not model, stays in his build.</summary>
    [Fact]
    public void A_Weapon_The_Engine_Does_Not_Model_Stays_In_The_Build()
    {
        var build = new CharacterBuild { Hero = "Tariq", Name = "Quake", PrimaryStat = 161, WeaponAbilityId = 1559 };

        string profile = new SimcProfile(build, "").Format();
        Assert.Contains("weapon=earthbreaker", profile);
        Assert.Equal(1559, SimcProfile.Parse(profile).Build.WeaponAbilityId);
        Assert.Equal(1559, BuildShareLink.TryRead(BuildShareLink.Create(Site, build))!.WeaponAbilityId);
        Assert.Empty(BuildEditor.RemoveUnknown(build));
        Assert.Contains("Weapon with ability 1559", HeroImporter.Import(build).UnmodeledFeatures);
    }

    [Fact]
    public void An_Import_Keeps_Its_Talents()
    {
        var build = Sylvie();

        Assert.Empty(BuildEditor.RemoveUnknown(build));
        Assert.Equal([176, 32], build.Talents);
    }

    [Fact]
    public void Its_Character_Sheet_Has_Its_Main_Stat_And_Gear()
    {
        var editor = new BuildEditor(Sylvie());

        Assert.Equal(MainStat.Intellect, HeroImporter.ImportForSheet(Sylvie()).MainStat);
        Assert.True(editor.TotalMainStat > 161);
        Assert.True(editor.Percent(RatedStat.Crit) > 0.05);
    }

    /// <summary>A link made before build-only heroes, for an engine hero: it opens the same build, and makes the same link.</summary>
    [Fact]
    public void A_Link_Shared_Before_Opens_As_It_Did()
    {
        const string link = "https://simfelldev.github.io/build#rime.QSFsoDZx6ieJVpATKidf6AFdg.sww";

        var build = BuildShareLink.TryRead(link)!;

        Assert.Equal("Rime", build.Hero);
        Assert.Equal("sww", build.Name);
        Assert.Equal([161.0, 306, 89, 71, 40, 30],
            [build.PrimaryStat, build.Stamina, build.CritRating, build.HasteRating, build.ExpertiseRating, build.SpiritRating]);
        Assert.Equal([43, 48, 657], build.Talents);
        Assert.Equal(31, Assert.Single(build.Augmentations).Id);
        Assert.Equal(5921, build.Relic1ItemId);
        Assert.Equal(161, build.WeaponAbilityId);
        Assert.Equal(link, BuildShareLink.Create(Site, build));
    }
}
