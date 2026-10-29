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
        Assert.Equal(editor.Percent(RatedStat.Crit), new BuildEditor(read).Percent(RatedStat.Crit), 9);
    }

    [Fact]
    public void A_Logs_Enchants_Become_Stat_Enchants_And_Its_Ratings_Are_The_Gears()
    {
        // As a log has it: two items enchanted with Banshee's Cunning (enchant 67, Crit + Spirit), 12 and 6 rating.
        var combatant = JsonNode.Parse("""
            {
              "intellect": 300, "stamina": 400, "crit": 150, "haste": 100, "expertise": 80, "spirit": 90,
              "gear": [
                { "id": 1, "attributes": [ { "name": "Intellect", "value": 120 }, { "name": "Critical Strike", "value": 70 } ],
                  "traits": [ { "id": 3000067, "rank": 12, "name": "Banshee's Cunning" } ] },
                { "id": 2, "attributes": [ { "name": "Stamina", "value": 200 }, { "name": "Spirit", "value": 40 }, { "name": "Haste", "value": 60 } ],
                  "traits": [ { "id": 3000067, "rank": 6, "name": "Banshee's Cunning" } ] }
              ],
              "weaponTraits": [ { "id": 3000067, "points": 1 }, { "id": 3000065, "points": 1 }, { "id": 3000069, "points": 0 } ]
            }
            """)!.AsObject();

        // The weapon tree's enchant nodes add 6 rating (one slot) per point.
        var build = CharacterReader.FromCombatantInfo(combatant, "player", "Rime");
        Assert.Equal([new BuildEntry(67, 24, "Banshee's Cunning"), new BuildEntry(65, 6)], build.StatEnchants);
        Assert.DoesNotContain(build.Augmentations, a => a.Id is 65 or 67 or 69);
        // The stats are the gear's own (its items' attributes), not the log's totals.
        Assert.Equal(120, build.PrimaryStat);
        Assert.Equal(200, build.Stamina);
        Assert.Equal(70, build.CritRating);
        Assert.Equal(40, build.SpiritRating);
        Assert.Equal(0, build.ExpertiseRating);
        Assert.Equal(60, build.HasteRating);
        Assert.False(build.StatsFromOldProfile);
    }
}
