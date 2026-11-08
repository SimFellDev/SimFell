using System.Globalization;
using System.Text.Json;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Domain;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// Reads the dungeon of a fight with one player's damage events, as <c>FellowshipLogsReports.GetDungeonFightAsync</c>
/// returns it, against the game data placements of Cithrel's Fall: a pack met at its placements, Ancient Koros, and spawns of a
/// creature with no placement.
/// </summary>
public class DungeonReaderTests
{
    private const int CithrelsFall = CithrelsFallGameDungeon.DungeonId;
    // Game IDs: Ancient Koros, and Anomalous Orb, a creature Cithrel's Fall places nowhere.
    private const int Koros = 169, Orb = 286;
    // Actors: 10 is the pack's creature, 12 Koros, 13 the orbs, 14 an NPC of no creature, 20 an NPC of the party's.
    private const int PackActor = 10, KorosActor = 12, OrbActor = 13, MysteryActor = 14;

    private static readonly Func<GameCreature> NewKoros = () => new AncientKorosGameCreature(), NewOrb = () => new AnomalousOrbGameCreature();

    private static readonly GameDungeon Dungeon =GameDungeon.ById(CithrelsFall)!;

    // A pack without a patrol with two placements of one creature, and that creature.
    private static readonly (CodexPlacement[] Placements, int Creature) Pack = FindPack();

    private static (CodexPlacement[], int) FindPack()
    {
        var packOf = DungeonPacks.Of(Dungeon.Placements);
        var pack = Dungeon.Placements.Select((p, i) => (p, Pack: packOf[i]))
            .Where(x => x.p.Patrol == null && GameCreatures.ById(x.p.CreatureId)?.Rank != CreatureRank.Boss)
            .GroupBy(x => (x.Pack, x.p.CreatureId))
            .First(g => g.Count() >= 2);
        return (pack.Select(x => x.p).Take(2).ToArray(), pack.Key.CreatureId);
    }

    private static CodexPlacement KorosPlacement => Dungeon.Placements.Single(p => p.CreatureId == Koros);

    // A contact at a placement: the log's x and y are the placement's y and x.
    private static string Contact(int actor, int instance, double firstMs, CodexPlacement at, double lastMs, double offset = 0) =>
        Contact(actor, instance, firstMs, at.Y + offset, at.X, lastMs);

    private static string Contact(int actor, int instance, double firstMs, double x, double y, double lastMs) =>
        string.Create(CultureInfo.InvariantCulture,
            $$"""{ "id": {{actor}}, "instance": {{instance}}, "first": {{firstMs}}, "x": {{x}}, "y": {{y}}, "last": {{lastMs}} }""");

    private static string Death(int actor, int instance, int gameId, string name, double ms) => string.Create(CultureInfo.InvariantCulture,
        $$"""{ "id": {{actor}}, "instance": {{instance}}, "gameID": {{gameId}}, "name": "{{name}}", "timestamp": {{ms}} }""");

    // The fight runs from 100 s to 400 s. The pack is met at 110 s and dies by 150 s; Koros is met at 180 s and dies at
    // 300 s; two orbs spawn at 200 s and 201.5 s, in combat with Koros, and die at 230 s; the NPC of no creature dies at
    // 149 s, and the party's NPC at 141 s.
    private static string Fight(string contacts, string deaths) => $$"""
        {
          "report_code": "abc",
          "fight": {
            "id": 7, "name": "Cithrel's Fall", "startTime": 100000, "endTime": 400000, "difficulty": 10, "difficultyLevel": 50,
            "gameZone": { "id": {{CithrelsFall}}, "name": "Cithrel's Fall" },
            "enemyNPCs": [ { "id": {{PackActor}}, "gameID": {{Pack.Creature}} }, { "id": {{KorosActor}}, "gameID": {{Koros}} },
                           { "id": {{OrbActor}}, "gameID": {{Orb}} }, { "id": {{MysteryActor}}, "gameID": 999999 } ]
          },
          "contact_player": { "id": 3, "name": "Arck", "hero": "Xavian" },
          "enemy_contacts": [ {{contacts}} ],
          "enemy_deaths": [ {{deaths}} ]
        }
        """;

    private static readonly string Contacts = string.Join(", ",
        Contact(PackActor, 1, 110000, Pack.Placements[0], 140000),
        Contact(PackActor, 2, 111000, Pack.Placements[1], 150000),
        Contact(KorosActor, 1, 180000, KorosPlacement, 300000),
        Contact(OrbActor, 1, 200000, 0, 0, 230000),
        Contact(OrbActor, 2, 201500, 0, 0, 230000),
        Contact(MysteryActor, 1, 145000, 0, 0, 149000));

    private static readonly string Deaths = string.Join(", ",
        Death(PackActor, 1, Pack.Creature, "Pack 1", 140000),
        Death(PackActor, 2, Pack.Creature, "Pack 2", 150000),
        Death(MysteryActor, 1, 999999, "Mystery", 149000),
        Death(20, 1, Pack.Creature, "Sibling of Earth 1", 141000),
        Death(OrbActor, 1, Orb, "Anomalous Orb 1", 230000),
        Death(OrbActor, 2, Orb, "Anomalous Orb 2", 230000),
        Death(KorosActor, 1, Koros, "Ancient Koros", 300000));

    private static DungeonProfile Read(string contacts, string deaths)
    {
        using var document = JsonDocument.Parse(Fight(contacts, deaths));
        return DungeonReader.FromFight(document.RootElement);
    }

    [Fact]
    public void A_Pack_Met_At_Its_Placements_Is_One_Pull_Until_Its_Last_Death()
    {
        var dungeon = Read(Contacts, Deaths);
        Assert.Null(dungeon.Problem());
        Assert.Equal(dungeon.Format(), DungeonProfile.Parse(dungeon.Format()).Format());
        Assert.Equal("Cithrel's Fall", dungeon.Name);
        // The fight's difficultyLevel 50 is Eternal 30.
        Assert.Equal(30, dungeon.Difficulty);
        var creature = GameCreatures.ById(Pack.Creature)!;
        var pack = dungeon.Pull("1")!;
        Assert.Equal([new EnemyGroup(2, creature.Name, creature.SpiritPoints, HitPoints: Math.Round(creature.BaseHealth, 2))], pack.Groups);
        Assert.Equal(40, pack.DurationSeconds);
        // Koros is met 30 s after the pack's last death.
        Assert.Equal(30, dungeon.Pull("2")!.DowntimeSeconds);
        Assert.Equal(120, dungeon.Pull("2")!.DurationSeconds);
        Assert.True(dungeon.Pull("2")!.Final);
        Assert.Equal("Ancient Koros, 1:20 in the log", dungeon.Pull("2")!.Comment);
        Assert.Equal("From FellowshipLogs report abc, fight 7 (5:00), with Arck's damage events.\nEnemies without game data: Mystery.", dungeon.Comment);
    }

    [Fact]
    public void Spawns_Of_A_Creature_Met_Together_Are_One_Pull_Taken_In_At_Their_Offset()
    {
        var dungeon = Read(Contacts, Deaths);
        Assert.Equal(["1", "2", "3"], dungeon.Pulls.Select(p => p.Name));
        var orbs = dungeon.Pull("3")!;
        Assert.Equal(2, Assert.Single(orbs.Groups).Count);
        Assert.Equal(GameCreatures.ById(Orb)!.Name, orbs.Groups[0].Name);
        Assert.Equal(30, orbs.DurationSeconds);
        // The orbs come 20 s into Koros.
        Assert.Equal(new PullJoin("3", OffsetMs: 20_000), dungeon.Pull("2")!.Join);
    }

    [Fact]
    public void An_Enemy_Met_Away_From_Its_Placement_Joins_The_Pack_Opened_Before_It()
    {
        // The second enemy is met 10 s later and 6,000 units from its placement, dragged by the first's pull.
        string contacts = string.Join(", ",
            Contact(PackActor, 1, 110000, Pack.Placements[0], 140000),
            Contact(PackActor, 2, 120000, Pack.Placements[1], 150000, offset: 6000),
            Contact(KorosActor, 1, 180000, KorosPlacement, 300000));
        var dungeon = Read(contacts, Deaths);
        Assert.Equal(2, dungeon.Pull("1")!.EnemyCount);
        Assert.Equal(2, dungeon.Pulls.Count);
    }

    [Fact]
    public void An_Enemy_Dying_Without_Contact_Is_In_The_Pull_In_Combat_At_Its_Death()
    {
        // The second enemy of the pack never meets the player, and dies at 150 s.
        string contacts = string.Join(", ",
            Contact(PackActor, 1, 110000, Pack.Placements[0], 140000),
            Contact(KorosActor, 1, 180000, KorosPlacement, 300000));
        var dungeon = Read(contacts, Deaths);
        Assert.Equal(2, dungeon.Pull("1")!.EnemyCount);
        Assert.Equal(40, dungeon.Pull("1")!.DurationSeconds);
    }

    [Fact]
    public void A_Fight_Without_Contacts_Has_No_Dungeon()
    {
        using var document = JsonDocument.Parse(Fight("", Deaths));
        Assert.Throws<ArgumentException>(() => DungeonReader.FromFight(document.RootElement));
    }

    [Fact]
    public void A_Patrol_Is_One_Pack_And_Placements_Apart_Are_Two()
    {
        CodexPlacement[] placements =
        [
            new(Koros, NewKoros, 0, 0, null), new(Koros, NewKoros, DungeonPacks.Spacing, 0, null),
            new(Koros, NewKoros, 3 * DungeonPacks.Spacing, 0, null),
            new(Orb, NewOrb, 10_000, 0, 4), new(Orb, NewOrb, 20_000, 0, 4),
        ];
        Assert.Equal([0, 0, 1, 2, 2], DungeonPacks.Of(placements));
    }

    [Theory]
    [InlineData("Cithrel's Fall", "Cithrels Fall")]
    [InlineData("'?'", "Imported dungeon")]
    public void An_Imported_Dungeon_Name_Is_A_File_Name(string name, string expected) =>
        Assert.Equal(expected, FellowshipSim.UI.Services.ActionListStore.ValidName(name, "Imported dungeon"));
}
