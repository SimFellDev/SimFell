using System.Text.Json.Nodes;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>The relic in a combatantinfo event's gear (<see cref="CharacterReader"/>).</summary>
public class CharacterReaderTests
{
    // Gear as FellowshipLogs gives it from 2026-10-23 on: a relic has no "set" field, and its item ID names the passive.
    private const string Combatant = """
        {
          "strength": 300,
          "gear": [
            { "id": 5946, "quality": 5 },
            { "id": 5926, "quality": 5, "name": "Orb of Inner Seas" }
          ]
        }
        """;

    [Fact]
    public void One_Relic_Gives_Its_Passive()
    {
        var build = CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "player", "Rime");
        Assert.Equal(AccursedTrophy.RelicPassiveId, build.RelicPassiveId);

        var hero = HeroImporter.Import(build);
        Assert.IsType<AccursedTrophy>(hero.RelicPassive);
        Assert.DoesNotContain(hero.UnmodeledFeatures, f => f.StartsWith("Relic"));
    }

    [Fact]
    public void A_Profile_Writes_The_Relic_It_Read()
    {
        var build = CharacterReader.FromCombatantInfo(JsonNode.Parse(Combatant)!.AsObject(), "player", "Rime");
        string text = new SimcProfile(build, "").Format();
        Assert.Contains("relic=accursed_trophy", text);
        Assert.Equal(build.RelicPassiveId, SimcProfile.Parse(text).Build.RelicPassiveId);
    }
}
