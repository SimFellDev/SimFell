using System.Text.Json.Nodes;
using FellowshipSim.Core.Customization.ProtectiveEnchants;
using FellowshipSim.Core.Customization.StatEnchants;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;
using FellowshipSim.UI.Build;

namespace FellowshipSim.Tests.Services;

/// <summary>
/// The build editor's augmentation kinds and stat enchants: a build's ratings are without its enchants, the enchants add
/// their rating to both their stats, and a log's character sheet already counts them.
/// </summary>
public class StatEnchantTests
{
    private static StatEnchant CritSpirit => StatEnchant.Find(67)!;

    [Fact]
    public void Augmentations_Are_Split_Into_Aspects_Sigils_And_Protective_Enchants()
    {
        var catalog = BuildCatalog.For("Rime");
        Assert.Contains(catalog.Aspects, a => a.Name == "Amethyst Splinters");
        Assert.Contains(catalog.Sigils, s => s.Name == "Hidden Power");
        Assert.Contains(catalog.ProtectiveEnchants, p => p.Name == "The Oak");
        var ids = catalog.Aspects.Concat(catalog.Sigils).Concat(catalog.ProtectiveEnchants).Select(o => o.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void A_Build_Takes_At_Most_Three_Protective_Enchants()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        var enchants = editor.Catalog.ProtectiveEnchants;
        for (int i = 0; i < ProtectiveEnchant.MaxPerBuild; i++) Assert.True(editor.ToggleProtectiveEnchant(enchants[i]));

        Assert.False(editor.ToggleProtectiveEnchant(enchants[ProtectiveEnchant.MaxPerBuild]));
        Assert.Equal(ProtectiveEnchant.MaxPerBuild, editor.ProtectiveEnchantCount);
        Assert.True(editor.ToggleProtectiveEnchant(enchants[0]));
        Assert.Equal(ProtectiveEnchant.MaxPerBuild - 1, editor.ProtectiveEnchantCount);
    }

    [Fact]
    public void Rimes_Stat_Enchants_Have_Their_Intellect_Names()
    {
        var names = BuildCatalog.For("Rime").StatEnchants.Select(e => e.Name).ToList();
        Assert.Equal(["Chimera's Cunning", "Salamander's Cunning", "Banshee's Cunning", "Undine's Cunning", "Sphinx's Cunning", "Unicorn's Cunning"], names);
    }

    [Fact]
    public void An_Enchant_Adds_Its_Rating_To_Both_Stats_And_Survives_A_Profile()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        editor.SetRating(RatedStat.Crit, 200);
        editor.SetStatEnchantRating(CritSpirit, 60);

        Assert.Equal(StatBlock.BaseCritChance + StatDiminishingReturns.FromRating(260), editor.Percent(RatedStat.Crit), 9);
        Assert.Equal(StatBlock.BaseSpirit + StatDiminishingReturns.FromRating(60), editor.Percent(RatedStat.Spirit), 9);

        var read = SimcProfile.Parse(new SimcProfile(editor.Build, "").Format()).Build;
        Assert.Equal([new BuildEntry(67, 60)], read.StatEnchants);
        Assert.Equal(200, read.CritRating, 0);
        Assert.Equal(0, read.SpiritRating, 0);
        Assert.Equal(editor.Percent(RatedStat.Crit), read.SheetPercents["crit"], 4);
    }

    [Fact]
    public void A_Sheet_Counts_Its_Enchants_Once()
    {
        var build = BuildEditor.NewBuild("Rime");
        build.StatEnchants.Add(new BuildEntry(67, 60));
        build.CritRating = 200;
        build.SheetPercents["crit"] = 0.30;

        // The importer adds an enchant only to stats that are not from a sheet.
        var fromSheet = HeroImporter.Import(build);
        Assert.Equal(200, fromSheet.Stats.CritRating);
        build.SheetPercents.Clear();
        Assert.Equal(260, HeroImporter.Import(build).Stats.CritRating);

        // Changing an enchant moves its two stats off the sheet, to ratings plus enchants.
        build.SheetPercents["crit"] = 0.30;
        build.SheetPercents["haste"] = 0.20;
        var editor = new BuildEditor(build);
        editor.SetStatEnchantRating(CritSpirit, 30);
        Assert.False(editor.PercentFromLog(RatedStat.Crit));
        Assert.True(editor.PercentFromLog(RatedStat.Haste));
        Assert.Equal(StatBlock.BaseCritChance + StatDiminishingReturns.FromRating(230), editor.Percent(RatedStat.Crit), 9);
    }

    [Fact]
    public void A_Logs_Enchants_Become_Stat_Enchants_And_Leave_Its_Ratings()
    {
        // As a log has it: two items enchanted with Banshee's Cunning (enchant 67, Crit + Spirit), 12 and 6 rating.
        var combatant = JsonNode.Parse("""
            {
              "intellect": 300, "stamina": 400, "crit": 150, "haste": 100, "expertise": 80, "spirit": 90,
              "gear": [
                { "id": 1, "traits": [ { "id": 3000067, "rank": 12, "name": "Banshee's Cunning" } ] },
                { "id": 2, "traits": [ { "id": 3000067, "rank": 6, "name": "Banshee's Cunning" } ] }
              ],
              "weaponTraits": [ { "id": 3000067, "points": 1 }, { "id": 3000065, "points": 1 }, { "id": 3000069, "points": 0 } ]
            }
            """)!.AsObject();

        // The weapon tree's enchant nodes add 1 per point.
        var build = CharacterReader.FromCombatantInfo(combatant, "player", "Rime");
        Assert.Equal([new BuildEntry(67, 19, "Banshee's Cunning"), new BuildEntry(65, 1)], build.StatEnchants);
        Assert.DoesNotContain(build.Augmentations, a => a.Id is 65 or 67 or 69);
        Assert.Equal(130, build.CritRating);
        Assert.Equal(71, build.SpiritRating);
        Assert.Equal(79, build.ExpertiseRating);
        Assert.Equal(100, build.HasteRating);
    }
}
