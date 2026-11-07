using FellowshipSim.Core;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Import;
using FellowshipSim.UI.Build;
using FellowshipSim.UI.Services;

namespace FellowshipSim.Tests.Services;

/// <summary>The Sim page's build editor, its catalog of choices, and saved builds.</summary>
public sealed class BuildEditorTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), $"simfell-profiles-{Guid.NewGuid():N}");

    [Fact]
    public void The_Main_Stat_Is_The_Gears_And_Its_Total_Adds_The_Base_100()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        Assert.Equal(0, editor.Build.PrimaryStat);
        Assert.Equal(StatBlock.BasePrimaryStat, editor.TotalMainStat);
        editor.Build.PrimaryStat = 130;
        Assert.Equal(230, editor.TotalMainStat);
        Assert.Equal(StatBlock.BaseStamina, editor.TotalStamina);
    }

    [Fact]
    public void The_Sheets_Gem_Power_Has_Covetous_Greed_And_The_Profile_Keeps_The_Gears()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        editor.SetGearBonuses(GemColor.Topaz, 1);
        double gear = editor.Gem(GemColor.Topaz);
        Assert.Equal(gear, editor.SheetGem(GemColor.Topaz));

        editor.Relic1 = CovetousGreed.OrbItemId;
        Assert.Equal(Math.Floor(gear * CovetousGreed.GemPowerBonus), editor.SheetGem(GemColor.Topaz));
        Assert.Equal(gear, editor.Gem(GemColor.Topaz));
        Assert.Equal(gear, SimcProfile.Parse(new SimcProfile(editor.Build, "").Format()).Build.GemPower[GemColor.Topaz]);
    }

    [Fact]
    public void The_Catalog_Has_Rimes_Tree_With_Icons_And_Every_Weapon()
    {
        var catalog = BuildCatalog.For("rime");
        Assert.Equal(18, catalog.Talents.Count);
        Assert.All(catalog.Talents, t => Assert.True(t.Talent.Modeled && t.Talent.Icon != null, t.Talent.Name));
        // Rime's four weapons alone, by their abilities: Voidbringer's Touch, Nature's Fury, Chronoshift, Icicles of An'zhyr.
        Assert.Equal([155, 161, 1558, 1561], catalog.Weapons.Select(w => w.Id).Order());
        Assert.Contains(catalog.Legendaries, l => l.Name == "Frostwyrm's Spite");
        Assert.Contains(catalog.Legendaries, l => l.Name == "Skandi's Decree" && l.Modeled);
        Assert.Contains(catalog.Relics, r => r.Name == AccursedTrophy.OrbItemName && r.PassiveName == AccursedTrophy.RelicPassiveName);
        Assert.All(catalog.Glyphs, g => Assert.Equal(4, g.MaxRank));
        Assert.NotNull(BuildCatalog.PortraitUrl("Rime"));
    }

    [Fact]
    public void Every_Engine_Heros_Talents_And_Legendaries_Have_Icons()
    {
        foreach (string hero in Catalog.Heroes.Keys)
        {
            var catalog = BuildCatalog.For(hero);
            Assert.All(catalog.Talents, t => Assert.True(t.Talent.Icon != null, $"{hero}: {t.Talent.Name}"));
            Assert.All(catalog.Legendaries, l => Assert.True(l.Icon != null, $"{hero}: {l.Name}"));
        }
    }

    /// <summary>
    /// Every legendary item game data gives an engine hero is one of the hero's legendaries, so an imported build keeps
    /// it.
    /// </summary>
    [Fact]
    public void Every_Legendary_Item_Of_An_Engine_Hero_Is_Its_Legendary()
    {
        foreach (string key in GameIcons.LegendaryItems.Keys)
        {
            string hero = key.Split(':')[0];
            if (!Catalog.HasHero(hero)) continue;
            Assert.True(Catalog.CreateLegendary(hero, int.Parse(key.Split(':')[1])) != null, key);
        }
    }

    /// <summary>Every hero has the game's three legendaries, head, chest and legs, each with its legendary item's icon.</summary>
    [Fact]
    public void Every_Hero_Has_Its_Legendaries_With_Item_Icons()
    {
        foreach (string hero in BuildHeroes.Names)
        {
            var legendaries = BuildCatalog.For(hero).Legendaries;
            // Head, chest, legs.
            Assert.Equal([5899, 5898, 5900], legendaries.Select(l => l.Id));
            Assert.All(legendaries, l => Assert.Equal(GameIcons.LegendaryItems[$"{hero}:{l.Id}"], l.Icon));
        }
        Assert.Contains(BuildCatalog.For("Ardeos").Legendaries, l => l.Name == "Fire Toad" && l.Modeled);
        Assert.Contains(BuildCatalog.For("Gunde").Legendaries, l => l.Name == "Carrion Onslaught" && l.Modeled);
        Assert.All(BuildCatalog.For("Aeona").Legendaries, l => Assert.False(l.Modeled));
    }

    [Fact]
    public void A_Legendary_The_Engine_Does_Not_Model_Stays_In_The_Build()
    {
        var build = new CharacterBuild { Hero = "Sylvie", Name = "Bloom", PrimaryStat = 161, LegendaryItemIds = [5898] };

        string profile = new SimcProfile(build, "").Format();
        Assert.Contains("legendary=unyielding_bloom,id=5898", profile);
        Assert.Equal([5898], SimcProfile.Parse(profile).Build.LegendaryItemIds);
        Assert.Empty(BuildEditor.RemoveUnknown(build));
        Assert.Equal(5898, BuildCatalog.For("Sylvie").LegendaryOption(5898));
    }

    /// <summary>
    /// A legendary worn on any of its items shows as picked: Lil Angst's Vexira's Venom is item 5900 (mfcJd8T6Mvw4zQqB fight
    /// 37), its option; a build saved with the old wrist item 5225 shows the same option.
    /// </summary>
    [Fact]
    public void A_Legendary_On_Any_Of_Its_Items_Is_Its_Option()
    {
        var catalog = BuildCatalog.For("Mara");
        var vexiras = Assert.Single(catalog.Legendaries, l => l.Name == "Vexira's Venom");

        Assert.Equal(5900, vexiras.Id);
        Assert.Equal(vexiras.Id, catalog.LegendaryOption(5900));
        Assert.Equal(vexiras.Id, catalog.LegendaryOption(5225));
        Assert.Null(catalog.LegendaryOption(null));
        Assert.Null(catalog.LegendaryOption(1));
    }

    [Fact]
    public void Talents_Cost_Their_Rows_Points_And_Stop_At_The_Budget()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        var talents = editor.Catalog.Talents;
        TalentOption Of(int id) => talents.Single(t => t.Slot.TalentId == id);

        Assert.True(editor.ToggleTalent(Of(IcyTalons.TalentId)));
        Assert.Equal(Hero.TalentPoints - 3, editor.PointsLeft);
        Assert.True(editor.ToggleTalent(Of(IcyTalons.TalentId)));
        Assert.Equal(Hero.TalentPoints, editor.PointsLeft);

        // Every talent of row 5 and two of row 1 use up all but one point: the last talent of row 1 is refused.
        foreach (var t in talents.Where(t => t.Slot.Row == 5).Concat(talents.Where(t => t.Slot.Row == 1).Take(2))) Assert.True(editor.ToggleTalent(t));
        Assert.Equal(1, editor.PointsLeft);
        Assert.False(editor.ToggleTalent(talents.Where(t => t.Slot.Row == 1).Last()));
    }

    [Fact]
    public void Editing_A_Stat_Clears_An_Old_Profiles_Mark()
    {
        var build = BuildEditor.NewBuild("Rime");
        build.StatsFromOldProfile = true;
        var editor = new BuildEditor(build);
        Assert.True(editor.StatsFromOldProfile);

        editor.SetRating(RatedStat.Crit, 500);
        Assert.False(editor.StatsFromOldProfile);
        Assert.Equal(StatBlock.BaseCritChance + StatDiminishingReturns.FromRating(500), editor.Percent(RatedStat.Crit));
    }

    [Fact]
    public void Gem_Nodes_Add_Their_Bonus_To_The_Sheet_Percentage_As_The_Sim_Does()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        double crit = editor.Percent(RatedStat.Crit);

        // Killer Instinct adds crit chance and Berserker's Zeal adds crit rating.
        editor.SetGem(GemColor.Amethyst, 300);
        Assert.Equal(crit + 0.02 + StatDiminishingReturns.FromRating(8), editor.Percent(RatedStat.Crit), 6);
        Assert.Equal(8, editor.TotalRating(RatedStat.Crit));
        editor.SetGem(GemColor.Amethyst, 1250);
        // The sim reads the build through its profile.
        var simmed = HeroImporter.Import(SimcProfile.Parse(new SimcProfile(editor.Build, "").Format()).Build);
        Assert.Equal(simmed.Stats.CritPercent, editor.Percent(RatedStat.Crit), 9);
    }

    [Fact]
    public void A_Build_Made_In_The_Editor_Writes_And_Reads_Back_As_A_Profile()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        var catalog = editor.Catalog;
        editor.ToggleTalent(catalog.Talents.Single(t => t.Slot.TalentId == IcyTalons.TalentId));
        editor.SetRating(RatedStat.Haste, 300);
        editor.Weapon = catalog.Weapons[0].Id;
        editor.Legendary = catalog.Legendaries.First(l => l.Modeled).Id;
        editor.Relic1 = catalog.Relics[0].ItemId;
        editor.Relic2 = catalog.Relics[1].ItemId;
        editor.SetGem(GemColor.Sapphire, 1500);
        editor.SetAugmentationRank(catalog.Aspects[0], 9);
        editor.SetGlyphRank(catalog.Glyphs[0], 2);

        var read = SimcProfile.Parse(new SimcProfile(editor.Build, "actions=frost_bolt").Format());
        var back = new BuildEditor(read.Build);
        Assert.Equal([IcyTalons.TalentId], read.Build.Talents);
        Assert.Equal(editor.Percent(RatedStat.Haste), back.Percent(RatedStat.Haste), 4);
        Assert.Equal(editor.Weapon, back.Weapon);
        Assert.Equal(editor.Legendary, back.Legendary);
        Assert.Equal(editor.Relic1, back.Relic1);
        Assert.Equal(editor.Relic2, back.Relic2);
        Assert.Equal(1500, back.Gem(GemColor.Sapphire));
        Assert.Equal(catalog.Aspects[0].MaxRank, back.AugmentationRank(catalog.Aspects[0].Id));
        Assert.Equal(2, back.GlyphRank(catalog.Glyphs[0].Id));
        Assert.Equal("actions=frost_bolt", read.ActionList.Trim());
    }

    [Fact]
    public void Parts_Of_An_Imported_Build_The_Engine_Lacks_Are_Left_Out()
    {
        var build = BuildEditor.NewBuild("Rime");
        build.Talents.AddRange([IcyTalons.TalentId, 999_999]);
        build.Augmentations.Add(new BuildEntry(999_999, 1, "Made-up augmentation"));
        build.WeaponAbilityId = 999_999;

        var removed = BuildEditor.RemoveUnknown(build);
        Assert.Equal(["talent 999999", "augmentation Made-up augmentation", "weapon with ability 999999"], removed);
        Assert.Equal([IcyTalons.TalentId], build.Talents);
        new SimcProfile(build, "").Format();
    }

    [Fact]
    public async Task Saved_Builds_Belong_To_Their_Visitor()
    {
        var profiles = new ProfileStore(new DiskBlobStore(_data));
        var a = new Visitor("a1b2", OwnsDataFolder: false);
        var b = new Visitor("c3d4", OwnsDataFolder: false);
        await profiles.SaveAsync(a, new ProfileEntry("Rime", "talons"), "rime=\"talons\"");

        Assert.Equal([new ProfileEntry("Rime", "talons")], await profiles.ListAsync(a, ["Rime"]));
        Assert.Empty(await profiles.ListAsync(b, ["Rime"]));
        Assert.Equal("rime=\"talons\"", await profiles.ReadAsync(a, new ProfileEntry("Rime", "talons")));
        await Assert.ThrowsAsync<ArgumentException>(() => profiles.SaveAsync(a, new ProfileEntry("Rime", "../x"), ""));

        await profiles.DeleteAsync(a, new ProfileEntry("Rime", "talons"));
        Assert.Empty(await profiles.ListAsync(a, ["Rime"]));
    }

    public void Dispose()
    {
        if (Directory.Exists(_data)) Directory.Delete(_data, recursive: true);
    }
}
