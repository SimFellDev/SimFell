using System.Text.Json.Nodes;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;
using FellowshipSim.UI.Build;

namespace FellowshipSim.Tests;

/// <summary>The items a build wears with their tempers: from a log, in a profile and a share link, and picked in the editor.</summary>
public class GearTests
{
    // Rime's gear as FellowshipLogs gives it: each item's ID, item level and tempers ("upgrades"); a legendary takes none.
    private const string Combatant = """
        {
          "gear": [
            { "id": 5885, "quality": 5, "itemLevel": 315, "upgrades": 6, "maxUpgrades": 8 },
            { "id": 5898, "quality": 6, "itemLevel": 360, "upgrades": 0, "maxUpgrades": 0 },
            { "id": 5932, "quality": 5, "itemLevel": 315, "upgrades": 8, "maxUpgrades": 8 },
            { "id": 5942, "quality": 5, "itemLevel": 315, "upgrades": 8, "maxUpgrades": 8 },
            { "id": 5926, "quality": 5, "itemLevel": 315, "upgrades": 8, "maxUpgrades": 8 },
            { "id": 5924, "quality": 5, "itemLevel": 315, "upgrades": 3, "maxUpgrades": 8 },
            { "id": 5984, "quality": 5, "itemLevel": 315, "upgrades": 8, "maxUpgrades": 8 },
            { "id": 5815, "quality": 0, "itemLevel": 0, "upgrades": 0, "maxUpgrades": 0 }
          ]
        }
        """;

    private static CharacterBuild Read() => CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "player", "Rime");

    [Fact]
    public void A_Log_Gives_Each_Item_In_Its_Slot_With_Its_Tempers()
    {
        var gear = Read().Gear.ToDictionary(p => p.Slot);

        Assert.Equal(new GearPiece(GearSlot.Head, 5885, 6), gear[GearSlot.Head]);
        Assert.Equal(new GearPiece(GearSlot.Chest, 5898, 0), gear[GearSlot.Chest]);
        // Rings and relics fill their two slots in gear order.
        Assert.Equal((5932, 5942), (gear[GearSlot.Ring1].ItemId, gear[GearSlot.Ring2].ItemId));
        Assert.Equal((5926, 5924, 3), (gear[GearSlot.Relic1].ItemId, gear[GearSlot.Relic2].ItemId, gear[GearSlot.Relic2].Tempers));
        Assert.Equal(new GearPiece(GearSlot.Weapon, 5984, 8), gear[GearSlot.Weapon]);
        // An item game data has no gear of (an active relic, item level 0) is no piece.
        Assert.Equal(7, gear.Count);
    }

    [Fact]
    public void A_Temper_Adds_One_To_Each_Of_An_Items_Stats()
    {
        // Game data: Bonebleached Headwrap at item level 315, untempered and at full tempers.
        var headwrap = GearItem.Find("Rime", 5885)!;
        Assert.Equal(new GearStats(22, 7, 11, 0, 0, 5), headwrap.StatsAt(0));
        Assert.Equal(new GearStats(30, 15, 19, 0, 0, 13), headwrap.StatsAt(8));
        Assert.Equal(headwrap.StatsAt(8), headwrap.StatsAt(12));
        // A legendary takes no tempers.
        var helm = GearItem.Find("Rime", 5899)!;
        Assert.Equal(helm.StatsAt(0), helm.StatsAt(8));
    }

    [Fact]
    public void A_Profile_Keeps_The_Gear_And_Its_Tempers()
    {
        var build = Read();
        string text = new SimcProfile(build, "").Format();
        Assert.Contains("head=bonebleached_headwrap,id=5885,tempers=6", text);
        Assert.Contains("relic2=orb_of_gathering_storms,id=5924,tempers=3", text);
        Assert.Contains("weapon=ashas_chronoshift_spire,id=5984,tempers=8", text);

        var read = SimcProfile.Parse(text).Build;
        Assert.Equal(build.Gear.OrderBy(p => p.Slot), read.Gear.OrderBy(p => p.Slot));
    }

    [Fact]
    public void A_Profile_Line_Names_Its_Item()
    {
        string text = "rime=\"x\"\nhead=icicled_circlet,id=5885,tempers=8\n";
        Assert.Contains("is bonebleached_headwrap", Assert.Throws<FormatException>(() => SimcProfile.Parse(text)).Message);
    }

    [Fact]
    public void A_Share_Link_Keeps_The_Gear_And_Its_Tempers()
    {
        var build = Read();
        var read = BuildShareLink.TryRead(BuildShareLink.Create("https://simfelldev.github.io/", build))!;
        Assert.Equal(build.Gear.OrderBy(p => p.Slot), read.Gear);
    }

    [Fact]
    public void Picking_Gear_Makes_The_Stats_The_Items_Summed()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        editor.Wear(GearSlot.Head, 5885);
        editor.Wear(GearSlot.Relic1, 5926);
        editor.Wear(GearSlot.Weapon, 5984);

        // Every piece starts at 8 tempers.
        var expected = GearItem.Find("Rime", 5885)!.StatsAt(8) + GearItem.Find("Rime", 5926)!.StatsAt(8) + GearItem.Find("Rime", 5984)!.StatsAt(8);
        Assert.Equal(expected, Stats(editor.Build));
        Assert.Equal(5926, editor.Build.Relic1ItemId);
        Assert.Equal(GearItem.Find("Rime", 5984)!.WeaponAbilityId, editor.Build.WeaponAbilityId);

        // Two fewer tempers on the head take 2 from each of its stats.
        editor.SetTempers(GearSlot.Head, 6);
        Assert.Equal(expected + new GearStats(-2, -2, -2, 0, 0, -2), Stats(editor.Build));

        // A legendary in its slot is the build's legendary; emptying a slot takes its item off.
        editor.Wear(GearSlot.Chest, 5898);
        Assert.Equal([5898], editor.Build.LegendaryItemIds);
        editor.Wear(GearSlot.Weapon, null);
        Assert.Equal(0, editor.Build.WeaponAbilityId);
        Assert.Null(editor.Item(GearSlot.Weapon));
    }

    private static GearStats Stats(CharacterBuild b) =>
        new((int)b.Stamina, (int)b.PrimaryStat, (int)b.CritRating, (int)b.ExpertiseRating, (int)b.HasteRating, (int)b.SpiritRating);
}
