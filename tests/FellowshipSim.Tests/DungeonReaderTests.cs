using System.Text.Json;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Enemies;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// Reads the dungeon of a fight without events, as <c>FellowshipLogsReports.GetDungeonFightAsync</c> returns it: three
/// FellowshipLogs pulls, the third starting before the second ends, and the enemy deaths of the fight.
/// </summary>
public class DungeonReaderTests
{
    // Game IDs of the Codex's data export: Coldbrine Fiend, Glacial Colossus, Ancient Koros, Orb of Bael'Aurum.
    private const int Fiend = 15, Colossus = 154, Koros = 169, Orb = 122;

    // Actor 10 is the fiends, 11 the colossus, 12 Koros, 13 the orb, 14 an NPC of no creature, and 20 an NPC of the party's.
    private const string Fight = """
        {
          "report_code": "abc",
          "fight": {
            "id": 7, "name": "Cithrel's Fall", "startTime": 100000, "endTime": 400000, "difficulty": 10,
            "gameZone": { "name": "Cithrel's Fall" },
            "enemyNPCs": [ { "id": 10 }, { "id": 11 }, { "id": 12 }, { "id": 13 }, { "id": 14 } ],
            "dungeonPulls": [
              { "id": 1, "name": "Pack", "startTime": 110000, "endTime": 150000,
                "enemyNPCs": [ { "id": 10, "gameID": 15, "minimumInstanceID": 1, "maximumInstanceID": 3 }, { "id": 14, "gameID": 999999, "minimumInstanceID": 1, "maximumInstanceID": 1 } ] },
              { "id": 2, "name": "Big one", "startTime": 180000, "endTime": 230000,
                "enemyNPCs": [ { "id": 11, "gameID": 154, "minimumInstanceID": 1, "maximumInstanceID": 1 } ] },
              { "id": 3, "name": "Boss", "startTime": 220000, "endTime": 320000,
                "enemyNPCs": [ { "id": 12, "gameID": 169, "minimumInstanceID": 1, "maximumInstanceID": 1 }, { "id": 13, "gameID": 122, "minimumInstanceID": 1, "maximumInstanceID": 1 } ] }
            ]
          },
          "enemy_deaths": [
            { "id": 10, "instance": 1, "gameID": 15, "name": "Coldbrine Fiend 1", "timestamp": 140000 },
            { "id": 10, "instance": 2, "gameID": 15, "name": "Coldbrine Fiend 2", "timestamp": 145000 },
            { "id": 10, "instance": 2, "gameID": 15, "name": "Coldbrine Fiend 2", "timestamp": 145500 },
            { "id": 10, "instance": 3, "gameID": 15, "name": "Coldbrine Fiend 3", "timestamp": 152000 },
            { "id": 14, "instance": 1, "gameID": 999999, "name": "Mystery", "timestamp": 149000 },
            { "id": 20, "instance": 1, "gameID": 15, "name": "Sibling of Earth 1", "timestamp": 141000 },
            { "id": 11, "instance": 1, "gameID": 154, "name": "Glacial Colossus", "timestamp": 228000 },
            { "id": 12, "instance": 1, "gameID": 169, "name": "Ancient Koros", "timestamp": 319000 }
          ]
        }
        """;

    private static DungeonProfile Read()
    {
        using var document = JsonDocument.Parse(Fight);
        return DungeonReader.FromFight(document.RootElement);
    }

    [Fact]
    public void A_Fight_Reads_As_Its_Pulls_With_Their_Durations()
    {
        var dungeon = Read();
        Assert.Null(dungeon.Problem());
        Assert.Equal(dungeon.Format(), DungeonProfile.Parse(dungeon.Format()).Format());
        Assert.Equal("Cithrel's Fall", dungeon.Name);
        Assert.Equal(10, dungeon.Difficulty);
        Assert.Equal(["1", "2", "3"], dungeon.Pulls.Select(p => p.Name));
        Assert.Equal([40, 50, 100], dungeon.Pulls.Select(p => p.DurationSeconds));
        // 30 s from pull 1's end to pull 2's start; pull 3 starts 40 s into pull 2, before it ends.
        Assert.Equal(30, dungeon.Pull("2")!.DowntimeSeconds);
        Assert.Equal(new PullJoin("3", OffsetMs: 40_000), dungeon.Pull("2")!.Join);
        Assert.True(dungeon.Pull("3")!.Final);
        Assert.Equal("Ancient Koros, 2:00 in the log", dungeon.Pull("3")!.Comment);
    }

    [Fact]
    public void Each_Enemy_Death_Is_One_Enemy_Of_Its_Creature()
    {
        var dungeon = Read();
        // The fiend dying twice counts once, the one dying 2 s after pull 1 ends is pull 1's, and the party's NPC is none.
        var fiends = Assert.Single(dungeon.Pull("1")!.Groups);
        var fiend = GameCreatures.ById(Fiend)!;
        Assert.Equal(new EnemyGroup(3, fiend.Name, fiend.SpiritPoints, HitPoints: Math.Round(fiend.BaseHealth, 2)), fiends);
        Assert.Equal(GameCreatures.ById(Colossus)!.Name, Assert.Single(dungeon.Pull("2")!.Groups).Name);
        var boss = Assert.Single(dungeon.Pull("3")!.Groups);
        Assert.True(boss.Boss);
        Assert.Equal(Math.Round(GameCreatures.ById(Koros)!.BaseHealth, 2), boss.HitPoints);
        // The orb never dies, and the NPC of no creature is named in the comment.
        Assert.DoesNotContain(dungeon.Pulls.SelectMany(p => p.Groups), g => g.Name == GameCreatures.ById(Orb)!.Name);
        Assert.Equal("From FellowshipLogs report abc, fight 7 (5:00).\nEnemies without game data: Mystery.", dungeon.Comment);
    }

    [Fact]
    public void A_Fight_Without_Deaths_Has_No_Dungeon()
    {
        using var document = JsonDocument.Parse(Fight.Replace("\"enemy_deaths\": [", "\"enemy_deaths\": [], \"unused\": ["));
        Assert.Throws<ArgumentException>(() => DungeonReader.FromFight(document.RootElement));
    }

    [Theory]
    [InlineData("Cithrel's Fall", "Cithrels Fall")]
    [InlineData("'?'", "Imported dungeon")]
    public void An_Imported_Dungeon_Name_Is_A_File_Name(string name, string expected) =>
        Assert.Equal(expected, FellowshipSim.UI.Services.ActionListStore.ValidName(name, "Imported dungeon"));
}
