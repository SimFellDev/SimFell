using System.Text.Json.Nodes;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;
using FellowshipSim.UI.Build;

namespace FellowshipSim.Tests;

/// <summary>Gem power from the gear's sockets and bonuses, times the relics' multiplier (<see cref="GemItem"/>).</summary>
public class GemSourcesTests
{
    // The gems and gem power bonuses of a logged player's gear (Cf7npHNzRmALFJQy fight 1, neteX), with Covetous Greed's
    // Orb of Hidden Riches (5921), and the gem power the log gives for each colour.
    private const string Combatant = """
        {
          "intellect": 300,
          "sapphire": 750, "amethyst": 1500, "emerald": 687, "topaz": 687, "ruby": 0, "diamond": 0,
          "gear": [
            { "id": 5906, "quality": 5, "gem": { "id": 24 }, "gemPowers": [ { "name": "Sapphire" }, { "name": "Sapphire" } ] },
            { "id": 5952, "quality": 5, "gemPowers": [ { "name": "Amethyst" } ] },
            { "id": 5807, "quality": 5, "gemPowers": [ { "name": "Emerald" }, { "name": "Topaz" } ] },
            { "id": 5836, "quality": 5, "gemPowers": [ { "name": "Sapphire" }, { "name": "Amethyst" } ] },
            { "id": 5996, "quality": 5, "gem": { "id": 24 }, "gemPowers": [ { "name": "Topaz" }, { "name": "Topaz" } ] },
            { "id": 5881, "quality": 5, "gemPowers": [ { "name": "Amethyst" } ] },
            { "id": 5848, "quality": 5, "gemPowers": [ { "name": "Amethyst" } ] },
            { "id": 5942, "quality": 5, "gem": { "id": 33 }, "gemPowers": [ { "name": "Sapphire" }, { "name": "Sapphire" } ] },
            { "id": 5936, "quality": 5, "gem": { "id": 49 }, "gemPowers": [ { "name": "Emerald" } ] },
            { "id": 5924, "quality": 5, "gemPowers": [ { "name": "Sapphire" } ] },
            { "id": 5921, "quality": 5, "gemPowers": [ { "name": "Amethyst" } ] },
            { "id": 5980, "quality": 5, "gemPowers": [ { "name": "Emerald" } ] }
          ]
        }
        """;

    private static CharacterBuild Read() => CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "neteX", "Rime");

    [Fact]
    public void The_Log_Reader_Reads_The_Sockets_And_Gear_Bonuses()
    {
        var build = Read();
        Assert.Equal([24, 24, 33, 49], build.SocketedGemIds.Order());
        Assert.Equal(6, build.GearGemBonuses[GemColor.Sapphire]);
        Assert.Equal(5, build.GearGemBonuses[GemColor.Amethyst]);
        Assert.Equal(3, build.GearGemBonuses[GemColor.Emerald]);
        Assert.Equal(3, build.GearGemBonuses[GemColor.Topaz]);
    }

    [Fact]
    public void Sockets_And_Gear_Bonuses_Times_Covetous_Greed_Give_The_Logged_Gem_Power()
    {
        var build = Read();
        var logged = new Dictionary<GemColor, double>(build.GemPower);
        var editor = new BuildEditor(build);

        // Any change recounts the totals from the sources.
        editor.SetGearBonuses(GemColor.Sapphire, editor.GearBonuses(GemColor.Sapphire));
        Assert.Equal(logged, build.GemPower);

        // Without the relic, no 25%.
        int relic = build.Relic1ItemId == 5921 ? 1 : 2;
        if (relic == 1) editor.Relic1 = null; else editor.Relic2 = null;
        Assert.Equal(1200, editor.Gem(GemColor.Amethyst));
    }

    [Fact]
    public void A_Socket_Or_Gear_Bonus_Changes_Its_Colours_Power()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        editor.SetSocket(0, 25);
        editor.SetSocket(1, 46);
        editor.SetGearBonuses(GemColor.Amethyst, 3);
        Assert.Equal(GemItem.FlawlessPower + 3 * GemItem.GearBonusPower, editor.Gem(GemColor.Amethyst));
        Assert.Equal(GemItem.FlawlessPower, editor.Gem(GemColor.Sapphire));

        // Which socket a gem is in does not matter: the third goes in the next one free.
        editor.SetSocket(3, 46);
        Assert.Equal(2 * GemItem.FlawlessPower, editor.Gem(GemColor.Sapphire));
        Assert.Equal(46, editor.Socket(2)?.Id);
        editor.SetSocket(0, null);
        Assert.Equal(3 * GemItem.GearBonusPower, editor.Gem(GemColor.Amethyst));
        editor.SetGearBonuses(GemColor.Amethyst, 0);
        Assert.Equal(0, editor.Gem(GemColor.Amethyst));
    }

    [Fact]
    public void A_Picked_Gem_Fills_The_Next_Socket_Until_All_Four_Are_Filled()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        for (int i = 0; i < GemItem.Sockets; i++) Assert.True(editor.AddGem(24));
        Assert.False(editor.AddGem(46));
        Assert.Equal(GemItem.Sockets, editor.SocketedCount(24));
        Assert.Equal(GemItem.Sockets * GemItem.SplendidPower, editor.Gem(GemColor.Amethyst));
    }

    [Fact]
    public void Relics_Fill_The_Empty_Slot_And_Two_Is_The_Most()
    {
        var editor = new BuildEditor(BuildEditor.NewBuild("Rime"));
        var relics = editor.Catalog.Relics;
        Assert.True(editor.ToggleRelic(relics[0].ItemId));
        Assert.True(editor.ToggleRelic(relics[1].ItemId));
        Assert.False(editor.ToggleRelic(relics[2].ItemId));
        Assert.Equal((relics[0].ItemId, relics[1].ItemId), (editor.Relic1, editor.Relic2));

        Assert.True(editor.ToggleRelic(relics[0].ItemId));
        Assert.Null(editor.Relic1);
        Assert.True(editor.ToggleRelic(relics[2].ItemId));
        Assert.Equal(relics[2].ItemId, editor.Relic1);
    }

    [Fact]
    public void A_Profile_Keeps_The_Sockets_And_Gear_Bonuses()
    {
        var build = Read();
        string text = new SimcProfile(build, "").Format();
        Assert.Contains("gem_sockets=", text);
        Assert.Contains("gear_gem_bonuses=", text);
        var read = SimcProfile.Parse(text).Build;
        Assert.Equal(build.SocketedGemIds, read.SocketedGemIds);
        Assert.Equal(build.GearGemBonuses, read.GearGemBonuses);
        Assert.Equal(build.GemPower, read.GemPower);
    }
}
