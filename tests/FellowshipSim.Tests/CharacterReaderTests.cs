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
    public void Each_Relic_Gives_Its_Passive()
    {
        var build = CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "player", "Rime");
        Assert.Equal([AccursedTrophy.RelicPassiveId, UndyingHeart.RelicPassiveId], build.RelicPassiveIds);

        var hero = HeroImporter.Import(build);
        Assert.Collection(hero.RelicPassives, r => Assert.IsType<AccursedTrophy>(r), r => Assert.IsType<UndyingHeart>(r));
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Relic"));
    }

    [Fact]
    public void A_Profile_Writes_The_Relics_It_Read()
    {
        var build = CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "player", "Rime");
        string text = new SimcProfile(build, "").Format();
        Assert.Contains("relic=accursed_trophy", text);
        Assert.Contains("relic=undying_heart", text);
        Assert.Equal(build.RelicPassiveIds, SimcProfile.Parse(text).Build.RelicPassiveIds);
    }
}
