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
    public void The_Catalog_Has_Rimes_Tree_With_Icons_And_Every_Weapon()
    {
        var catalog = BuildCatalog.For("rime");
        Assert.Equal(18, catalog.Talents.Count);
        Assert.All(catalog.Talents, t => Assert.True(t.Talent.Modeled && t.Talent.Icon != null, t.Talent.Name));
        Assert.Equal(Catalog.AllWeapons.Count(), catalog.Weapons.Count);
        Assert.Contains(catalog.Legendaries, l => l.Name == "Frostwyrm's Spite");
        Assert.Contains(catalog.Relics, r => r.Name == AccursedTrophy.OrbItemName && r.PassiveName == AccursedTrophy.RelicPassiveName);
        Assert.All(catalog.Glyphs, g => Assert.Equal(4, g.MaxRank));
        Assert.NotNull(BuildCatalog.PortraitUrl("Rime"));
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

        // Every talent of rows 5 (3 points each) and 1 (2 each) is 15 points: the last one is refused.
        foreach (var t in talents.Where(t => t.Slot.Row == 5).Concat(talents.Where(t => t.Slot.Row == 1).Take(2))) Assert.True(editor.ToggleTalent(t));
        Assert.Equal(1, editor.PointsLeft);
        Assert.False(editor.ToggleTalent(talents.Where(t => t.Slot.Row == 1).Last()));
    }

    [Fact]
    public void A_Rating_Set_By_Hand_Replaces_The_Logs_Sheet_Percentage()
    {
        var build = BuildEditor.NewBuild("Rime");
        build.SheetPercents["crit"] = 0.30;
        var editor = new BuildEditor(build);
        Assert.Equal(0.30, editor.Percent(RatedStat.Crit));
        Assert.True(editor.PercentFromLog(RatedStat.Crit));

        editor.SetRating(RatedStat.Crit, 500);
        Assert.False(editor.PercentFromLog(RatedStat.Crit));
        Assert.Equal(StatBlock.BaseCritChance + StatDiminishingReturns.FromRating(500), editor.Percent(RatedStat.Crit));
    }

    [Fact]
    public void Gem_Nodes_Add_Their_Bonus_To_The_Sheet_Percentage_As_The_Sim_Does()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        double crit = editor.Percent(RatedStat.Crit);

        editor.SetGem(GemColor.Amethyst, 300);
        Assert.Equal(crit + 0.02, editor.Percent(RatedStat.Crit), 6);
        editor.SetGem(GemColor.Amethyst, 1250);
        Assert.Equal(crit + 0.06, editor.Percent(RatedStat.Crit), 6);
        // The sim reads the build through its profile.
        var simmed = HeroImporter.Import(SimcProfile.Parse(new SimcProfile(editor.Build, "").Format()).Build);
        Assert.Equal(simmed.Stats.CritPercent, editor.Percent(RatedStat.Crit), 4);
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
