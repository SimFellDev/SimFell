using System.Text.Json.Nodes;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>The relics in a combatantinfo event's gear (<see cref="CharacterReader"/>).</summary>
public class CharacterReaderTests
{
    // Gear as FellowshipLogs gives it from build 0.5.0 on: two relics with no "set" field, each item ID naming a passive.
    private const string Combatant = """
        {
          "strength": 300,
          "gear": [
            { "id": 5946, "quality": 5 },
            { "id": 5926, "quality": 5, "name": "Orb of Inner Seas" },
            { "id": 5924, "quality": 5, "name": "Orb of Gathering Storms" }
          ]
        }
        """;

    [Fact]
    public void The_Relics_Fill_Their_Slots_In_Gear_Order()
    {
        var build = CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "player", "Rime");
        Assert.Equal(AccursedTrophy.OrbItemId, build.Relic1ItemId);
        Assert.Equal(UndyingHeart.OrbItemId, build.Relic2ItemId);

        var hero = HeroImporter.Import(build);
        Assert.IsType<AccursedTrophy>(hero.Relic1);
        Assert.IsType<UndyingHeart>(hero.Relic2);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Relic"));
    }

    [Fact]
    public void A_Profile_Writes_The_Relics_It_Read()
    {
        var build = CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "player", "Rime");
        string text = new SimcProfile(build, "").Format();
        Assert.Contains("relic1=orb_of_inner_seas,id=5926", text);
        Assert.Contains("relic2=orb_of_gathering_storms,id=5924", text);
        var read = SimcProfile.Parse(text).Build;
        Assert.Equal((build.Relic1ItemId, build.Relic2ItemId), (read.Relic1ItemId, read.Relic2ItemId));
    }
}
