using System.Text.Json.Nodes;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// Reads the dungeon of a Cithrel's Fall run (report vTVX8j4dbnz2Ag3P, fight 23) with the hero share of its Rime (player 92).
/// With the fight not in data/fights it does nothing.
/// </summary>
public class DungeonReaderTests(ITestOutputHelper output)
{
    private const string ReportCode = "vTVX8j4dbnz2Ag3P";
    private const int FightId = 23, RimeId = 92;

    [Fact]
    public async Task Cithrels_Fall_Run_Reads_As_A_Valid_Dungeon()
    {
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        if (!library.HasRawFight(ReportCode, FightId))
        {
            output.WriteLine($"Fight {FightId} of {ReportCode} is not in {library.FightsDirectory}.");
            return;
        }
        var raw = await library.GetRawFightAsync(null, ReportCode, FightId);
        var dungeon = DungeonReader.FromRawFight(raw, RimeId);
        string text = dungeon.Format();
        output.WriteLine(text);
        double heroDamage = dungeon.HeroShare / 100 * dungeon.Pulls.SelectMany(p => p.Groups).Sum(g => g.Count * g.HitPoints!.Value) * dungeon.HealthScale;
        output.WriteLine($"Hero damage to the enemies: {heroDamage:N0}");

        Assert.Equal("Cithrel's Fall", dungeon.Name);
        Assert.Equal(10, dungeon.Difficulty);
        Assert.Null(dungeon.Problem());
        Assert.Equal(text, DungeonProfile.Parse(text).Format());

        var bosses = dungeon.Pulls.SelectMany(p => p.Groups).Where(g => g.Boss).Select(g => g.Name).ToList();
        Assert.Equal(["Noor, The Betrayer", "Ancient Koros", "Cithrel"], bosses);
        Assert.True(dungeon.Pulls.Single(p => p.Final).Groups.Single(g => g.Boss).Name == "Cithrel");

        // Ancient Koros's log health at difficulty 10, to the half health scale lost in rounding hit points to a whole number.
        var koros = dungeon.Pulls.SelectMany(p => p.Groups).Single(g => g.Name == "Ancient Koros");
        Assert.Equal(8547232, koros.HitPoints!.Value * dungeon.HealthScale, dungeon.HealthScale / 2);

        // FellowshipLogs' pulls by their number in its list. No enemy died in its pulls 5 and 6.
        Assert.Equal(["1", "2", "3", "4", "7", "8", "9", "10", "11", "12", "13", "14", "15"],
            dungeon.Pulls.Select(p => p.Name).Where(n => !n.Contains('_')));
        Assert.Equal("Elder Glacial Bear, 0:08 in the log", dungeon.Pulls[0].Comment);
        Assert.Equal("Cithrel, 22:58 in the log", dungeon.Pulls.Single(p => p.Final).Comment);
        // A pull's later waves (pull_n) join the wave before them; no FellowshipLogs pull starts in combat.
        for (int i = 0; i < dungeon.Pulls.Count; i++)
        {
            string? next = i + 1 < dungeon.Pulls.Count && dungeon.Pulls[i + 1].Name.Contains('_') ? dungeon.Pulls[i + 1].Name : null;
            Assert.Equal(next, dungeon.Pulls[i].Join?.Pull);
        }
        Assert.Contains(dungeon.Pulls, p => p.Name == "1_2");

        Assert.Equal(Deaths(raw), dungeon.Pulls.Sum(p => p.EnemyCount));
        Assert.InRange(dungeon.HeroShare, 1, 100);
    }

    [Theory]
    [InlineData("Cithrel's Fall", "Cithrels Fall")]
    [InlineData("'?'", "Imported dungeon")]
    public void An_Imported_Dungeon_Name_Is_A_File_Name(string name, string expected) =>
        Assert.Equal(expected, FellowshipSim.UI.Services.ActionListStore.ValidName(name, "Imported dungeon"));

    // The enemies the party killed in the fight.
    private static int Deaths(JsonObject raw)
    {
        var enemies = raw["fight"]!["enemyNPCs"]!.AsArray().Select(n => (int)n!["id"]!).ToHashSet();
        return raw["events"]!.AsArray()
            .Where(e => (string)e!["type"]! == "death" && enemies.Contains((int)e["targetID"]!))
            .Select(e => ((int)e!["targetID"]!, (int?)e["targetInstance"] ?? 1)).Distinct().Count();
    }
}
