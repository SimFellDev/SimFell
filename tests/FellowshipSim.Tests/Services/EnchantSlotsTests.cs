using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.StatEnchants;
using System.Text.Json.Nodes;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;
using FellowshipSim.UI.Build;

namespace FellowshipSim.Tests.Services;

/// <summary>
/// The enchant limits: 26 stat enchants and gem power bonuses, 12 ranks of aspects, sigils and glyphs, 39 of them in all
/// (a weapon adds 5), and 3 protective enchants outside the 39.
/// </summary>
public class EnchantSlotsTests
{
    private static BuildEditor NewEditor() => new(BuildEditor.NewBuild("Rime"));

    [Fact]
    public void Each_Kind_Stops_At_Its_Limit()
    {
        var editor = NewEditor();
        foreach (var glyph in editor.Catalog.Glyphs) editor.SetGlyphRank(glyph, glyph.MaxRank);
        Assert.Equal(12, editor.Used(EnchantSlot.Glyphs));

        foreach (var color in BuildCatalog.GemColors) editor.SetGearBonuses(color, 10);
        Assert.Equal(26, editor.Used(EnchantSlot.Gems));

        // 26 + 12 = 38 of 39: one more of anything, then nothing.
        var aspect = editor.Catalog.Aspects[0];
        editor.SetAugmentationRank(aspect, aspect.MaxRank);
        Assert.Equal(1, editor.AugmentationRank(aspect.Id));
        Assert.Equal(39, editor.UsedTotal);
        Assert.Equal(0, editor.Room(EnchantSlot.Sigils));
        // Protective enchants are outside the 39: all three still fit, and a fourth does not.
        foreach (var p in editor.Catalog.ProtectiveEnchants.Take(3)) Assert.True(editor.ToggleProtectiveEnchant(p));
        Assert.False(editor.ToggleProtectiveEnchant(editor.Catalog.ProtectiveEnchants[3]));
        Assert.Equal(39, editor.UsedTotal);
    }

    [Fact]
    public void A_Weapon_Adds_Two_Sigils_Two_Stat_Enchants_And_An_Aspect()
    {
        var editor = NewEditor();
        Assert.Equal((26, 12, 12, 39), (editor.Limit(EnchantSlot.StatEnchants), editor.Limit(EnchantSlot.Aspects), editor.Limit(EnchantSlot.Sigils), editor.TotalLimit));
        editor.Weapon = editor.Catalog.Weapons[0].Id;
        Assert.Equal((28, 13, 14, 44), (editor.Limit(EnchantSlot.StatEnchants), editor.Limit(EnchantSlot.Aspects), editor.Limit(EnchantSlot.Sigils), editor.TotalLimit));
        Assert.Equal(12, editor.Limit(EnchantSlot.Glyphs));
        Assert.Equal(26, editor.Limit(EnchantSlot.Gems));
    }

    [Fact]
    public void Stat_Enchants_Count_Six_Rating_Each_And_Keep_A_Logs_Rating()
    {
        var editor = NewEditor();
        var enchant = StatEnchant.All[0];
        editor.SetStatEnchantCount(enchant, 3);
        Assert.Equal(18, editor.StatEnchantRating(enchant.Id));
        Assert.Equal(3, editor.Used(EnchantSlot.StatEnchants));

        // A log's 29 rating is 5 enchants (rounded up), and keeps its 29 until the count changes.
        editor.SetStatEnchantRating(enchant, 29);
        Assert.Equal(5, editor.StatEnchantCount(enchant.Id));
        editor.SetStatEnchantCount(enchant, 5);
        Assert.Equal(29, editor.StatEnchantRating(enchant.Id));

        // No more than 26 in all.
        editor.SetStatEnchantCount(StatEnchant.All[1], 40);
        Assert.Equal(26, editor.Used(EnchantSlot.StatEnchants));
    }

    /// <summary>
    /// neteX's Banshee's Cunning (Cf7npHNzRmALFJQy fight 1): 6 + 6 + 10 rating on three items and one weapon tree point,
    /// 23 rating. On its item a stat enchant fills a slot per 6 rating, rounded up: 1 + 1 + 2 + 1 = 5 (the summed rating
    /// over 6 would give 4). A 12-rating item fills two.
    /// </summary>
    private const string Combatant = """
        {
          "intellect": 300,
          "gear": [
            { "id": 5906, "quality": 5, "traits": [ { "id": 3000067, "rank": 6, "name": "Banshee's Cunning" } ] },
            { "id": 5965, "quality": 5, "traits": [ { "id": 3000067, "rank": 6, "name": "Banshee's Cunning" }, { "id": 3000068, "rank": 12, "name": "Sphinx's Cunning" } ] },
            { "id": 5974, "quality": 5, "traits": [ { "id": 3000067, "rank": 10, "name": "Banshee's Cunning" } ] }
          ],
          "weaponTraits": [ { "id": 3000067, "level": 1, "points": 1 } ]
        }
        """;

    [Fact]
    public void A_Logs_Stat_Enchants_Count_As_The_Log_Has_Them_And_Keep_That_In_A_Profile()
    {
        var build = CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "neteX", "Rime");
        var editor = new BuildEditor(build);
        var banshee = StatEnchant.Find(67)!;
        var sphinx = StatEnchant.Find(68)!;
        Assert.Equal(23, editor.StatEnchantRating(banshee.Id));
        Assert.Equal(5, editor.StatEnchantCount(banshee.Id));
        Assert.Equal(12, editor.StatEnchantRating(sphinx.Id));
        Assert.Equal(2, editor.StatEnchantCount(sphinx.Id));
        Assert.Equal(7, editor.Used(EnchantSlot.StatEnchants));

        // A saved profile keeps the counts.
        var read = new BuildEditor(SimcProfile.Parse(new SimcProfile(build, "").Format()).Build);
        Assert.Equal(5, read.StatEnchantCount(banshee.Id));
        Assert.Equal(2, read.StatEnchantCount(sphinx.Id));

        // A count set in the editor is kept with its rating; a rating set by hand counts as its rating over 6.
        editor.SetStatEnchantCount(banshee, 2);
        Assert.Equal((12, 2), (editor.StatEnchantRating(banshee.Id), editor.StatEnchantCount(banshee.Id)));
        editor.SetStatEnchantRating(sphinx, 13);
        Assert.Equal(3, editor.StatEnchantCount(sphinx.Id));
    }

    [Fact]
    public void Logged_Players_Fit_The_Limits()
    {
        // neteX (Cf7npHNzRmALFJQy fight 1): 78 rating of stat enchants in 4 kinds, 17 gem power bonuses, 4 ranks of each
        // of aspects, sigils and glyphs, 3 protective enchants, with a weapon.
        var build = BuildEditor.NewBuild("Rime");
        var editor = new BuildEditor(build) { Weapon = BuildCatalog.For("Rime").Weapons[0].Id };
        foreach (var (enchant, rating) in new[] { (StatEnchant.All[0], 18.0), (StatEnchant.All[2], 29.0), (StatEnchant.All[3], 25.0), (StatEnchant.All[4], 6.0) })
            editor.SetStatEnchantRating(enchant, rating);
        build.GearGemBonuses[GemColor.Sapphire] = 17;
        Assert.Equal(14, editor.Used(EnchantSlot.StatEnchants));
        Assert.InRange(editor.UsedTotal, 0, editor.TotalLimit);
    }
}
