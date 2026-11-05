using System.Globalization;
using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Dungeon fights: the dungeon profile's text and its problems, the pulls in turn with their enemies, joins, chains and
/// downtime, enemies dying at their pull durations, the party's damage, enemy casts and interrupts, the dungeon_event and target
/// conditions that read them, and old action-list names in the new ones.
/// </summary>
public class DungeonTests
{
    private static CharacterBuild Build(string hero = "Rime") => JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{hero.ToLowerInvariant()}_character.json")))!;

    private static DungeonProfile Dungeon(params DungeonPull[] pulls) => new("Test", 600, pulls, DifficultyScaling.Base.Eternal);

    private const double TrashHitPoints = 10_000, BossHitPoints = 60_000;

    // A pull of trash or of a boss, 'seconds' long.
    private static DungeonPull Trash(int number, int count, double seconds) =>
        new(number.ToString(CultureInfo.InvariantCulture), [new EnemyGroup(count, "Trash Mob", 3, HitPoints: TrashHitPoints)], seconds);

    private static DungeonPull Boss(int number, double seconds, bool final = false) =>
        new(number.ToString(CultureInfo.InvariantCulture), [new EnemyGroup(1, "Boss", 45, HitPoints: BossHitPoints, Boss: true)], seconds, Final: final);

    // A cast is reported as it completes, so one begun before a pull ends can complete up to a cast time after it.
    private const double CastTime = 2.5;

    /// <summary>
    /// A hit of a fight: its time, enemy and amount, the enemies in combat, the enemy's health % after it, the fight's boss,
    /// trash and final boss state, and the estimated start of the next boss.
    /// </summary>
    private sealed record Hit(double Time, EnemyTarget Enemy, double Amount, int Active, double HealthPct, bool Boss, bool Trash, bool Final, SimTime NextBossAt);

    /// <summary>What one fight did: each enemy's death and each hit.</summary>
    private sealed record FightLog(MonteCarloResult Result, List<(double Time, EnemyTarget Enemy)> Deaths, List<Hit> Hits)
    {
        public double DeathOf(int pull) => Deaths.Where(d => d.Enemy.Id == 1000 + pull).Max(d => d.Time);
        public double FirstHitOn(int pull) => Hits.Where(h => h.Enemy.Id == 1000 + pull).Min(h => h.Time);
    }

    // One fight of the dungeon with the action list, its deaths and hits recorded.
    private static FightLog Fight(DungeonProfile dungeon, string actions, string hero = "Rime", int seed = 1)
    {
        var deaths = new List<(double, EnemyTarget)>();
        var hits = new List<Hit>();
        var build = Build(hero);
        var simulator = new MonteCarloSimulator(() =>
        {
            var h = HeroImporter.Import(build);
            h.OnEnemyDefeated += (ctx, enemy) => deaths.Add((ctx.CurrentTime.TotalSeconds, enemy));
            h.OnDamageDealt += (in DamageContext hit) =>
            {
                if (hit is { Target: { } target, SimContext: { } c })
                {
                    hits.Add(new Hit(hit.Timestamp.TotalSeconds, target, hit.Amount, c.ActiveEnemies.Count, target.HealthPercent, c.Boss, c.Trash,
                        c.FinalBoss, c.NextBossAt));
                }
            };
            return h;
        }, AplParser.ParseScript(actions));
        deaths.Clear();
        hits.Clear();
        var result = simulator.Run(SimTime.FromSeconds(dungeon.TimeLimitSeconds), iterations: 1, firstSeed: seed, degreeOfParallelism: 1,
            fight: new FightOptions(Dungeon: dungeon));
        return new FightLog(result, deaths, hits);
    }

    private const string FrostBolts = "actions+=/frost_bolt";

    // The casts of the typical fight of a dungeon, with an action list of one frost bolt under the condition.
    private static IReadOnlyList<CastReport> Casts(DungeonProfile dungeon, string condition) =>
        new MonteCarloSimulator(Build(), AplParser.ParseScript($"actions+=/frost_bolt,if={condition}"))
            .Run(SimTime.FromSeconds(dungeon.TimeLimitSeconds), iterations: 1, degreeOfParallelism: 1, fight: new FightOptions(Dungeon: dungeon))
            .Casts.Where(c => c.Name == "Frost Bolt").ToList();

    [Fact]
    public void The_Default_Dungeon_Reads_Back_From_Its_Text()
    {
        var dungeon = DefaultDungeons.Default;
        Assert.Equal(DefaultDungeons.DefaultName, dungeon.Name);
        Assert.Null(dungeon.Problem());
        Assert.Equal(dungeon.Format(), DungeonProfile.Parse(dungeon.Format()).Format());
        Assert.Contains(dungeon.Pulls, p => p.IsBoss && p.Final);
        Assert.All(dungeon.Pulls, p => Assert.True(p.DurationSeconds > 0));
    }

    [Fact]
    public void Named_Pulls_Run_In_The_Order_Of_The_Text()
    {
        const string text = """
            dungeon=X
            pull.first=/duration=30,count=2,name=Imp,hit_points=900000
            pull.last_boss=/duration=90,downtime=25,final=0
            pull.last_boss+=/boss.1,name=Koros,hit_points=4000000
            pull.2=/duration=20,count=1,name=Bat,hit_points=600000,chain_with=first,pull_with=adds_1,after_deaths=1
            pull.adds_1=/duration=15,count=2,name=Wisp,hit_points=500000
            """;
        var dungeon = DungeonProfile.Parse(text);
        Assert.Equal(["first", "last_boss", "2", "adds_1"], dungeon.Pulls.Select(p => p.Name));
        Assert.Equal(["first", "2", "last_boss"], dungeon.RunOrder()!.Select(p => p.Name));
        Assert.Equal(new PullJoin("adds_1", AfterDeaths: new DeathCount(1)), dungeon.Pull("2")!.Join);
        Assert.Equal(25, dungeon.Pull("last_boss")!.DowntimeSeconds);
        Assert.Equal(dungeon.Format(), DungeonProfile.Parse(dungeon.Format()).Format());
        Assert.Equal("Pull first: two pulls have this name.", (dungeon with { Pulls = [.. dungeon.Pulls, dungeon.Pull("first")!] }).Problem());
        Assert.Equal("Pull bad-name: the pull name has letters, digits and '_' only.",
            (dungeon with { Pulls = [dungeon.Pull("first")! with { Name = "bad-name" }] }).Problem());
    }

    [Fact]
    public void A_Pull_Reads_Back_From_Its_Text()
    {
        const string text = """
            dungeon=X
            time_limit=900
            difficulty=12
            pull.3=/count=4,name=Ghoul,hit_points=5000,duration=45
            pull.3+=/downtime=20
            pull.1=/duration=60,count=5,name=Imp,spirit_points=3,hit_points=900000
            pull.1+=/count=1,name="Ogre, Captain",spirit_points=10,priority=1,hit_points=3000000,interrupt_ability="Big Cast",cooldown=20
            pull.1+=/pull_with=4,offset_pct=50
            pull.4=/boss.1,name="Ancient Koros",spirit_points=45,hit_points=40000000,final=1,duration=120
            pull.2=/duration=30,count=3,name=Bat,hit_points=600000,chain_with=1,downtime=5
            pull.5=/duration=10,count=1,name=Wisp,hit_points=1
            pull.5+=/pull_with=3,after_deaths=50%
            pull.6=/duration=10,count=1,name=Wisp,hit_points=1
            pull.6+=/pull_with=5,after_deaths_total=2
            """;
        var dungeon = DungeonProfile.Parse(text);
        Assert.Equal(["3", "1", "4", "2", "5", "6"], dungeon.Pulls.Select(p => p.Name));
        Assert.Equal([45, 60, 120, 30, 10, 10], dungeon.Pulls.Select(p => p.DurationSeconds));
        Assert.Equal(900, dungeon.TimeLimitSeconds);
        Assert.Equal(12, dungeon.Difficulty);
        var first = dungeon.Pull("1")!;
        Assert.Equal(new PullJoin("4", OffsetPct: 50), first.Join);
        Assert.Equal(new EnemyGroup(1, "Ogre, Captain", 10, true, 3_000_000, false, new EnemyCast("Big Cast", 20, EnemyCast.DefaultCastSeconds)), first.Groups[1]);
        Assert.True(dungeon.Pull("4")!.Final);
        Assert.Equal("1", dungeon.Pull("2")!.ChainWith);
        Assert.Equal(new DeathCount(50, Percent: true), dungeon.Pull("5")!.Join!.AfterDeaths);
        Assert.Equal(new DeathCount(2), dungeon.Pull("6")!.Join!.AfterDeathsTotal);
        Assert.Equal(dungeon.Format(), DungeonProfile.Parse(dungeon.Format()).Format());
        Assert.Contains("name=\"Ogre, Captain\"", dungeon.Format());
    }

    [Fact]
    public void Comments_Read_Back_With_Their_Group_Pull_Or_Dungeon()
    {
        const string text = """
            # Line one of the dungeon.
            dungeon=X
            # Line two of the dungeon.
            time_limit=1800
            # The imps first.
            #
            pull.1=/count=5,name=Imp,hit_points=900000
            # The ogre casts.
            pull.1+=/count=1,name=Ogre,hit_points=900000
            # Pull 1's options.
            pull.1+=/duration=40,downtime=5
            pull.2=/duration=20
            # The bats.
            pull.2+=/count=3,name=Bat,hit_points=600000
            # After the last pull.
            """;
        var dungeon = DungeonProfile.Parse(text);
        Assert.Equal("Line one of the dungeon.\nLine two of the dungeon.", dungeon.Comment);
        Assert.Equal("The imps first.\n\nPull 1's options.", dungeon.Pull("1")!.Comment);
        Assert.Null(dungeon.Pull("1")!.Groups[0].Comment);
        Assert.Equal("The ogre casts.", dungeon.Pull("1")!.Groups[1].Comment);
        Assert.Equal("After the last pull.", dungeon.Pull("2")!.Comment);
        Assert.Equal("The bats.", dungeon.Pull("2")!.Groups[0].Comment);
        Assert.Equal("""
            # Line one of the dungeon.
            # Line two of the dungeon.
            dungeon=X
            difficulty=10
            time_limit=1800
            # The imps first.
            # Pull 1's options.
            pull.1=/duration=40,downtime=5
            pull.1+=/count=5,name=Imp,hit_points=900000
            # The ogre casts.
            pull.1+=/count=1,name=Ogre,hit_points=900000
            # After the last pull.
            pull.2=/duration=20
            # The bats.
            pull.2+=/count=3,name=Bat,hit_points=600000

            """.Replace("\r\n", "\n"), dungeon.Format());
        Assert.Equal(dungeon.Format(), DungeonProfile.Parse(dungeon.Format()).Format());
    }

    [Fact]
    public void A_Hero_Who_Never_Acts_Still_Ends_Each_Pull_At_Its_Duration()
    {
        // An action list whose one action never casts: the hero takes no action in the whole fight.
        var dungeon = Dungeon(Trash(1, 3, 20), Boss(2, 30, final: true) with { DowntimeSeconds = 10 });
        var log = Fight(dungeon, "actions+=/frost_bolt,if=0");
        Assert.Empty(log.Hits);
        Assert.Equal(60, log.Result.DurationSeconds, 3);
        Assert.Equal(20, log.DeathOf(1), 3);
        Assert.Equal(60, log.DeathOf(2), 3);
    }

    [Fact]
    public void A_Dungeon_Reports_The_Party_Dps_And_The_Heros_Share_Of_It()
    {
        var dungeon = Dungeon(Trash(1, 3, 20), Boss(2, 30, final: true) with { DowntimeSeconds = 10 });
        var result = Fight(dungeon, FrostBolts).Result;
        double health = (3 * TrashHitPoints + BossHitPoints) * DifficultyScaling.Base.Health;
        Assert.Equal(health / 60, result.PartyDps!.Value, 6);
        Assert.InRange(result.GroupDamageShare!.Value, 0.0001, 1.0);
        var party = result.Timeline!.PartyDps!;
        Assert.Equal(result.Timeline.Dps.Count, party.Count);
        // No enemy in the downtime from 20 to 30 s: the rolling party DPS falls to 0 by its end.
        Assert.Equal(0, party[29], 6);
        // Training dummies have no party.
        var dummies = new MonteCarloSimulator(Build(), AplParser.ParseScript(FrostBolts)).Run(SimTime.FromSeconds(30), iterations: 1, degreeOfParallelism: 1);
        Assert.Null(dummies.PartyDps);
        Assert.Null(dummies.GroupDamageShare);
        Assert.Null(dummies.Timeline!.PartyDps);
    }

    [Fact]
    public void The_Heros_Spirit_From_An_Enemy_Is_At_Most_Its_Worth()
    {
        var ctx = new SimulationContext(HeroImporter.Import(Build()));
        var hero = (Hero)ctx.Player;
        var enemy = ctx.AddEnemy(1001, 1, "Trash Mob", maxHealth: 1000);
        enemy.SpiritPointValue = 3;
        enemy.FollowTime(ctx, SimTime.Zero, SimTime.FromSeconds(100));
        double before = hero.ResourceAmount(Hero.SpiritPoints);
        // Three times the enemy's health: Spirit Points for its health alone.
        for (int i = 0; i < 6; i++) ctx.DealLoggedHit(hero, enemy, 1, "Hit", 500, false);
        Assert.Equal(3, hero.ResourceAmount(Hero.SpiritPoints) - before, 6);
        Assert.Equal(3000, enemy.HeroDamage);
    }

    [Fact]
    public void The_Readme_Dungeon_Text_Is_A_Valid_Dungeon()
    {
        string readme = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "README.md")).Replace("\r\n", "\n");
        int start = readme.IndexOf("\ndungeon=Default\n", StringComparison.Ordinal);
        start = readme.LastIndexOf("```\n", start, StringComparison.Ordinal) + 4;
        string text = readme[start..readme.IndexOf("```", start, StringComparison.Ordinal)];
        var dungeon = DungeonProfile.Parse(text);
        Assert.Null(dungeon.Problem());
        Assert.Equal(5, dungeon.Pulls.Count);
        Assert.NotNull(dungeon.Comment);
        Assert.NotNull(dungeon.Pull("1")!.Comment);
        Assert.NotNull(dungeon.Pull("1")!.Groups[1].Comment);
    }

    [Theory]
    [InlineData("pull.1=/count=1,name=A,hit_points=1", "no dungeon=<name> line")]
    [InlineData("dungeon=X", "no pull line")]
    [InlineData("dungeon=X\npull.1+=/count=1,name=A,hit_points=1", "pull 1 has no pull.1= line")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1\npull.1=/duration=1,count=1,name=A,hit_points=1", "pull 1 starts twice")]
    [InlineData("dungeon=X\npull.x-y=/count=1,name=A,hit_points=1", "'x-y' is not a pull name")]
    [InlineData("dungeon=X\npull.a=/duration=1,count=1,name=A,hit_points=1,chain_with=b c", "'b c' is not a pull name")]
    [InlineData("dungeon=X\npull.a=/duration=1,count=1,name=A,hit_points=1,pull_with=b", "Pull a: pull_with names pull b, and there is no pull b.")]
    [InlineData("dungeon=X\npull.1=/duration=1,name=A,hit_points=1", "an enemy group line starts with count= or boss.N")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,size=3", "'size' is not a pull option")]
    [InlineData("dungeon=X\npull.1=/boss.1,count=2,name=A,duration=1", "a boss line is one boss")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,cooldown=5", "cooldown and cast_time need interrupt_ability")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=0,name=A,hit_points=1", "Pull 1: A: the count is at least 1.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=0", "Pull 1: A: the hit points are more than 0.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,spirit_points=-1", "Pull 1: A: the Spirit Points are 0 or more.")]
    [InlineData("dungeon=X\npull.1=/count=1,name=Unknown,hit_points=1", "Pull 1 has no duration=<seconds>.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=Unknown", "Pull 1: Unknown: the enemy has hit_points, or the name of one creature of the game data.")]
    [InlineData("dungeon=X\npull.1=/duration=0,count=1,name=A,hit_points=1", "Pull 1: the duration is more than 0 seconds.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,downtime=-1", "Pull 1: the downtime is 0 seconds or more.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,final=1", "Pull 1: only a pull with a boss is the final boss.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,interrupt_ability=B,cooldown=2,cast_time=3", "Pull 1: A: the cast time is more than 0 seconds and at most the cooldown.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,interrupt_ability=B", "Pull 1: A: the cooldown is more than 0 seconds.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,offset_ms=5", "Pull 1: a trigger needs pull_with.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=2,offset_ms=5,offset_pct=50\npull.2=/duration=1,count=1,name=A,hit_points=1", "pull_with has one trigger at most")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=2,offset_pct=100\npull.2=/duration=1,count=1,name=A,hit_points=1", "offset_pct is more than 0 and less than 100")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=2,after_deaths=1.5\npull.2=/duration=1,count=1,name=A,hit_points=1", "after_deaths: a death count is a whole number of at least 1")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=2,after_deaths=2\npull.2=/duration=1,count=1,name=A,hit_points=1", "Pull 1: after_deaths is at most the pull's 1 enemies.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=2,after_deaths_total=0%\npull.2=/duration=1,count=1,name=A,hit_points=1", "after_deaths_total: a death percent is more than 0 and at most 100")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=3", "Pull 1: pull_with names pull 3, and there is no pull 3.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,chain_with=1", "Pull 1: chain_with names the pull itself.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=3\npull.2=/duration=1,count=1,name=A,hit_points=1,pull_with=3\npull.3=/duration=1,count=1,name=A,hit_points=1", "Pull 3: two pulls take it in with pull_with.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=3\npull.2=/duration=1,count=1,name=A,hit_points=1\npull.3=/duration=1,count=1,name=A,hit_points=1,chain_with=2", "Pull 3: a pull taken in with pull_with has no chain_with.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=4\npull.4=/duration=1,count=1,name=A,hit_points=1,chain_with=1", "Pull 4: a pull taken in with pull_with has no chain_with.")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=2\npull.2=/duration=1,count=1,name=A,hit_points=1,pull_with=1", "in a circle through pull_with and chain_with")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,chain_with=2\npull.2=/duration=1,count=1,name=A,hit_points=1,chain_with=1", "in a circle through pull_with and chain_with")]
    [InlineData("dungeon=X\npull.1=/duration=1,count=1,name=A,hit_points=1,pull_with=2,chain_with=3\npull.2=/duration=1,count=1,name=A,hit_points=1\npull.3=/duration=1,count=1,name=A,hit_points=1,chain_with=2", "in a circle through pull_with and chain_with")]
    [InlineData("dungeon=X\ntime_limit=0\npull.1=/duration=1,count=1,name=A,hit_points=1", "The time limit is more than 0 seconds.")]
    [InlineData("dungeon=X\ndifficulty=33\npull.1=/duration=1,count=1,name=A,hit_points=1", "The difficulty is an Eternal level from 0 (Base) to 32.")]
    public void A_Dungeon_Text_Names_What_Is_Wrong(string text, string problem)
    {
        var e = Assert.Throws<FormatException>(() => DungeonProfile.Parse(text));
        Assert.Contains(problem, e.Message);
    }

    [Fact]
    public void Pulls_Run_In_Order_With_Each_Pull_Taken_In_And_Each_Chain_After_Its_Pull()
    {
        var one = Trash(1, 1, 1) with { Join = new PullJoin("4") };
        var dungeon = Dungeon(one, Trash(2, 1, 1), Trash(3, 1, 1), Trash(4, 1, 1), Trash(5, 1, 1));
        Assert.Equal(["1", "2", "3", "5"], dungeon.RunOrder()!.Select(p => p.Name));
        Assert.Equal(["1", "4"], dungeon.RunMembers(dungeon.Pull("1")!).Select(p => p.Name));

        // Pull 2 chains to pull 4, inside pull 1's run.
        var chained = dungeon with { Pulls = [one, Trash(2, 1, 1) with { ChainWith = "4" }, Trash(3, 1, 1), Trash(4, 1, 1), Trash(5, 1, 1)] };
        Assert.Equal(["1", "2", "3", "5"], chained.RunOrder()!.Select(p => p.Name));
        // Pull 1 chains to a later pull.
        var later = Dungeon(Trash(1, 1, 1) with { ChainWith = "3" }, Trash(2, 1, 1), Trash(3, 1, 1));
        Assert.Equal(["2", "3", "1"], later.RunOrder()!.Select(p => p.Name));
    }

    [Theory]
    [InlineData("raid_event.adds.in", "dungeon_event.adds.in")]
    [InlineData("raid_event.boss.final", "dungeon_event.boss.final")]
    [InlineData("fight_remains", "dungeon_event.fight.remains")]
    [InlineData("dungeon_event.fight.remains", "dungeon_event.fight.remains")]
    public void Old_Names_Read_As_The_New_Ones(string old, string now)
    {
        Assert.Equal(now, AplPaths.Migrate(old));
        Assert.Equal(now, new PropertyExpression(old).PropertyPath);
    }

    [Fact]
    public void Old_Names_In_A_List_Text_Become_The_New_Ones()
    {
        const string text = "actions+=/frost_bolt,if=raid_event.adds.in>5&fight_remains<10|variable.fight_remains_x>my_raid_event.y\n";
        Assert.Equal("actions+=/frost_bolt,if=dungeon_event.adds.in>5&dungeon_event.fight.remains<10|variable.fight_remains_x>my_raid_event.y\n",
            AplPaths.MigrateText(text));
    }

    [Fact]
    public void The_Built_In_Lists_Use_The_New_Names()
    {
        foreach (string hero in Core.Catalog.Heroes.Keys)
        {
            foreach (string name in DefaultActionLists.Names(hero))
            {
                string text = DefaultActionLists.Read(hero, name)!;
                Assert.Equal(text, AplPaths.MigrateText(text));
            }
        }
    }

    [Fact]
    public void A_Chained_Pull_Starts_As_Its_Pull_Ends_And_A_Pull_After_Downtime_Waits()
    {
        double hp = 8;
        var log = Fight(Dungeon(Trash(1, 1, hp), Trash(2, 1, hp) with { ChainWith = "1" }, Trash(3, 1, hp) with { DowntimeSeconds = 20 }), FrostBolts);
        Assert.InRange(log.FirstHitOn(2) - log.DeathOf(1), 0, CastTime);
        Assert.InRange(log.FirstHitOn(3) - log.DeathOf(2), 20, 20 + CastTime);
        Assert.DoesNotContain(log.Hits, h => h.Time > log.DeathOf(2) + 0.5 && h.Time < log.DeathOf(2) + 20);
    }

    [Fact]
    public void The_Hero_Does_Not_Act_In_Downtime()
    {
        var dungeon = Dungeon(Trash(1, 3, 5), Boss(2, 10) with { DowntimeSeconds = 20 });
        var log = Fight(dungeon, FrostBolts);
        double end = log.DeathOf(1);
        var casts = log.Result.Casts.Where(c => c.Name == "Frost Bolt").ToList();
        Assert.Contains(casts, c => c.Time < end);
        Assert.Contains(casts, c => c.Time >= end + 20);
        Assert.DoesNotContain(casts, c => c.Time >= end + CastTime && c.Time < end + 20);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Pull_Joins_At_Its_Offset(bool boss)
    {
        var host = (boss ? Boss(1, 30) : Trash(1, 1, 30)) with { Join = new PullJoin("2", OffsetMs: 10_000) };
        var log = Fight(Dungeon(host, Trash(2, 1, 3)), FrostBolts);
        var hostHits = log.Hits.Where(h => h.Enemy.Id == 1001).ToList();
        Assert.All(hostHits.Where(h => h.Time < 10), h => Assert.Equal(1, h.Active));
        Assert.All(hostHits.Where(h => h.Time >= 10 && h.Time < 12), h => Assert.Equal(2, h.Active));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Pull_Joins_As_The_Hosts_Health_Falls_To_Its_Percent(bool boss)
    {
        var host = (boss ? Boss(1, 20) : Trash(1, 1, 20)) with { Join = new PullJoin("2", OffsetPct: 50) };
        var log = Fight(Dungeon(host, Trash(2, 1, 3)), FrostBolts);
        // The hit taking the host to 50% brings pull 2 in: the hero keeps the host as its target until it dies.
        var hostHits = log.Hits.Where(h => h.Enemy.Id == 1001).ToList();
        Assert.All(hostHits.Where(h => h.HealthPct > 50), h => Assert.Equal(1, h.Active));
        Assert.Equal(2, hostHits.First(h => h.HealthPct <= 50).Active);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Pull_Joins_After_Its_Deaths(bool boss)
    {
        var host = boss
            ? new DungeonPull("1", [new EnemyGroup(1, "Boss", HitPoints: BossHitPoints, Boss: true), new EnemyGroup(2, "Add", HitPoints: TrashHitPoints)], 10,
                Join: new PullJoin("2", AfterDeaths: new DeathCount(2)))
            : Trash(1, 3, 10) with { Join = new PullJoin("2", AfterDeaths: new DeathCount(2)) };
        // The host's three enemies die together at its duration, and the second death brings pull 2 in.
        var log = Fight(Dungeon(host, Trash(2, 1, 3)), FrostBolts);
        Assert.All(log.Deaths.Where(d => d.Enemy.Id == 1001), d => Assert.Equal(10, d.Time, 3));
        Assert.Equal(13, log.DeathOf(2), 3);
        Assert.All(log.Hits.Where(h => h.Time < 10), h => Assert.Equal(3, h.Active));
    }

    [Fact]
    public void A_Pull_Taken_In_By_A_Later_Pull_Runs_With_It()
    {
        var hp = 4;
        var dungeon = Dungeon(Trash(1, 1, hp) with { Join = new PullJoin("3") }, Trash(2, 1, hp), Trash(3, 1, hp));
        var log = Fight(dungeon, FrostBolts);
        // Pulls 1 and 3 are one pull, then pull 2.
        Assert.True(log.DeathOf(3) < log.FirstHitOn(2));
        Assert.True(log.DeathOf(1) < log.FirstHitOn(2));
    }

    [Fact]
    public void An_Enemy_Has_Its_Hit_Points_Times_The_Difficulty_Health_Scale()
    {
        // Deathless Katrine: game data health 29,075.305, logged with 6,581,643 health at Eternal 27.
        var dungeon = Dungeon(new DungeonPull("1", [new EnemyGroup(1, "Deathless Katrine", HitPoints: 29_075.305, Boss: true)], 60, Final: true))
            with { Difficulty = 27 };
        Assert.Equal(226.36, dungeon.HealthScale);
        Assert.Equal(175.45, dungeon.DamageScale);
        var log = Fight(dungeon, FrostBolts);
        Assert.Equal(6_581_643, log.Hits[0].Enemy.MaxHealth, 6_581_643 * 0.0001);
        Assert.Equal(1.31, (dungeon with { Difficulty = 0 }).HealthScale);
        Assert.Equal(["Base", "Eternal 1", "Eternal 32"], new[] { 0, 1, 32 }.Select(e => DifficultyScaling.At(e)!.Label));
    }

    [Fact]
    public void An_Enemy_Dies_At_Its_Pull_Duration_With_Its_Groups_Spirit_Points()
    {
        var dungeon = Dungeon(new DungeonPull("1", [new EnemyGroup(2, "Trash Mob", 3, HitPoints: TrashHitPoints)], 30), Boss(2, 5));
        var log = Fight(dungeon, FrostBolts);
        Assert.All(log.Deaths.Where(d => d.Enemy.Id == 1001), d => Assert.Equal(30, d.Time, 3));
        Assert.InRange(log.FirstHitOn(2), 30, 30 + 2 * CastTime);
        Assert.All(log.Deaths.Where(d => d.Enemy.Id == 1001), d => Assert.Equal(3, d.Enemy.SpiritPointValue));
    }

    [Fact]
    public void A_Group_Without_Values_Takes_Its_Creatures_Game_Data()
    {
        var group = new EnemyGroup(1, "Ancient Koros");
        Assert.Equal(AncientKorosGameCreature.BASE_HEALTH, group.Health);
        Assert.Equal(AncientKorosGameCreature.SPIRIT_POINTS, group.Spirit);
        Assert.Null(group.Problem());
        Assert.Null(new EnemyGroup(1, "No Such Creature").Health);
    }

    [Fact]
    public void A_Pull_Duration_Varies_With_The_Length_Variance()
    {
        var dungeon = Dungeon(new DungeonPull("1", [new EnemyGroup(1, "Trash Mob", HitPoints: TrashHitPoints)], 30));
        var fight = new FightOptions(LengthVariance: 0.2, Dungeon: dungeon);
        var result = new MonteCarloSimulator(Build(), AplParser.ParseScript(FrostBolts))
            .Run(SimTime.FromSeconds(600), iterations: 1, firstSeed: 3, degreeOfParallelism: 1, fight: fight);
        Assert.Equal(30 * fight.VarianceScale(3), result.DurationSeconds, 2);
    }

    [Fact]
    public void A_Join_Offset_Varies_With_The_Length_Variance()
    {
        // Pull 2 joins 28 s into pull 1's 30 s; in a fight 10% shorter both scale, so pull 2 still joins before pull 1 ends.
        var dungeon = Dungeon(Trash(1, 1, 30) with { Join = new PullJoin("2", OffsetMs: 28_000) }, Trash(2, 1, 10));
        var fight = new FightOptions(LengthVariance: 0.2, Dungeon: dungeon);
        int seed = Enumerable.Range(1, 100).First(s => fight.VarianceScale(s) < 0.9);
        var result = new MonteCarloSimulator(Build(), AplParser.ParseScript(FrostBolts))
            .Run(SimTime.FromSeconds(600), iterations: 1, firstSeed: seed, degreeOfParallelism: 1, fight: fight);
        Assert.Equal(38 * fight.VarianceScale(seed), result.DurationSeconds, 2);
    }

    [Fact]
    public void The_Fight_Ends_At_Its_Time_Limit()
    {
        var dungeon = Dungeon(Boss(1, 1000, final: true)) with { TimeLimitSeconds = 40 };
        var log = Fight(dungeon, FrostBolts);
        Assert.Equal(40, log.Result.DurationSeconds, 3);
        Assert.Empty(log.Deaths);
        Assert.Equal(40, log.Result.Timeline!.Damage.Count);
    }

    [Fact]
    public void An_Enemy_Casts_On_Its_Cooldown_With_No_Effect()
    {
        var hero = HeroImporter.Import(Build());
        var ctx = new SimulationContext(hero);
        var enemy = ctx.AddEnemy(1001, 1, "Caster", maxHealth: 1000);
        enemy.CastOnCooldown(ctx, new EnemyCast("Big Cast", 10, 2));
        ctx.RunUntil(SimTime.FromSeconds(9.9));
        Assert.False(enemy.IsCasting);
        ctx.RunUntil(SimTime.FromSeconds(10));
        Assert.True(enemy.IsCasting);
        Assert.Equal("Big Cast", enemy.CastAbility);
        Assert.Equal(SimTime.FromSeconds(12), enemy.CastEnd);
        ctx.RunUntil(SimTime.FromSeconds(12));
        Assert.False(enemy.IsCasting);
        ctx.RunUntil(SimTime.FromSeconds(20));
        Assert.True(enemy.IsCasting);
        ctx.RemoveEnemy(enemy, defeated: true);
        Assert.False(enemy.IsCasting);
        ctx.RunUntil(SimTime.FromSeconds(40));
        Assert.False(enemy.IsCasting);
    }

    [Theory]
    [InlineData("Rime", "brain_freeze", "Brain Freeze", "frost_bolt")]
    [InlineData("Gunde", "jawbreaker", "Jawbreaker", "attack")]
    [InlineData("Mara", "kick", "Kick", "backstab")]
    public void An_Interrupt_Ends_A_Casting_Target(string heroName, string interrupt, string interruptName, string filler)
    {
        var dungeon = Dungeon(new DungeonPull("1", [new EnemyGroup(1, "Caster", HitPoints: 1e12, Cast: new EnemyCast("Big Cast", 30, 2))], 1000))
            with { TimeLimitSeconds = 100 };
        int interrupts = 0;
        var casts = new List<double>();
        var build = Build(heroName);
        var simulator = new MonteCarloSimulator(() =>
        {
            var h = HeroImporter.Import(build);
            h.OnInterrupt += (ctx, enemy, ability) =>
            {
                Assert.Equal("Big Cast", ability);
                Assert.False(enemy.IsCasting);
                interrupts++;
            };
            h.OnCastCompleted += (ability, _, now) =>
            {
                if (ability.Name == interruptName) casts.Add(now.TotalSeconds);
            };
            return h;
        }, AplParser.ParseScript($"actions+=/{interrupt},if=target.casting&target.cast.remains>0\nactions+=/{filler}"));
        simulator.Run(SimTime.FromSeconds(100), iterations: 1, degreeOfParallelism: 1, fight: new FightOptions(Dungeon: dungeon));

        // The enemy casts at 30, 60 and 90 s; each cast is interrupted in its 2 s.
        Assert.Equal(3, interrupts);
        Assert.Equal(3, casts.Count);
        foreach (var (cast, start) in casts.Zip([30.0, 60.0, 90.0])) Assert.InRange(cast, start, start + 2);
    }

    [Fact]
    public void Target_Cast_Conditions_Read_The_Targets_Cast()
    {
        var hero = HeroImporter.Import(Build());
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(1001, 1, "Caster", maxHealth: 1000);
        enemy.CastOnCooldown(ctx, new EnemyCast("Big Cast", 10, 2));
        ctx.Player.CurrentTarget = enemy;

        void Reads(string text, double expected)
        {
            var expression = AplParser.ParseExpression(text);
            Assert.Equal(expected, expression.EvaluateNumeric(hero, ctx), 6);
            Assert.Equal(expected, BoundExpression.Bind(expression, hero).EvaluateNumeric(ctx), 6);
        }

        ctx.RunUntil(SimTime.FromSeconds(5));
        Reads("target.casting", 0);
        Reads("target.cast.remains", 0);
        Assert.Equal(SimTime.Infinite, BoundExpression.Bind(AplParser.ParseExpression("target.cast.remains<1"), hero).NextChange(ctx));
        ctx.RunUntil(SimTime.FromSeconds(10.5));
        Reads("target.casting", 1);
        Reads("target.cast.remains", 1.5);
        var bound = BoundExpression.Bind(AplParser.ParseExpression("target.cast.remains<1"), hero);
        Assert.Equal(SimTime.FromSeconds(11).Milliseconds, bound.NextChange(ctx).Milliseconds, 2.0);
        Assert.True(ctx.Interrupt(enemy));
        Reads("target.casting", 0);
        Assert.False(ctx.Interrupt(enemy));
    }

    [Fact]
    public void Boss_And_Trash_Conditions_Follow_The_Enemies_In_Combat()
    {
        // The boss pull takes trash in at its start: both are in combat, then the boss alone.
        var dungeon = Dungeon(Trash(1, 2, 4), Boss(2, 10, final: true) with { Join = new PullJoin("3") }, Trash(3, 1, 2));
        var log = Fight(dungeon, FrostBolts);
        Assert.All(log.Hits.Where(h => h.Enemy.Id == 1001), h => Assert.True(h.Trash && !h.Boss && !h.Final));
        Assert.All(log.Hits.Where(h => h.Enemy.Id == 1003), h => Assert.True(h.Trash && h.Boss && h.Final));
        Assert.All(log.Hits.Where(h => h.Time > log.DeathOf(3) + 0.1), h => Assert.True(!h.Trash && h.Boss && h.Final));
    }

    [Fact]
    public void Countdowns_Estimate_The_Next_Pulls()
    {
        var dungeon = Dungeon(Trash(1, 1, 10), Boss(2, 10) with { DowntimeSeconds = 20 });
        var log = Fight(dungeon, FrostBolts);
        // The boss starts 20 s after the trash dies at its 10 s.
        Assert.All(log.Hits.Where(h => h.Enemy.Id == 1001), h => Assert.Equal(30, h.NextBossAt.TotalSeconds, 3));
        Assert.All(log.Hits.Where(h => h.Enemy.Id == 1002), h => Assert.Equal(SimTime.Infinite, h.NextBossAt));
    }

    public static TheoryData<string> Heroes() => new(Core.Catalog.Heroes.Keys);

    [Theory]
    [MemberData(nameof(Heroes))]
    public void Every_Hero_Fights_The_Default_Dungeon_And_Hits_No_Dead_Enemy(string hero)
    {
        var build = Build(hero);
        int deadHits = 0;
        var simulator = new MonteCarloSimulator(() =>
        {
            var h = HeroImporter.Import(build);
            h.OnDamageDealt += (in DamageContext hit) =>
            {
                if (hit.Target is { IsSleeping: true }) Interlocked.Increment(ref deadHits);
            };
            return h;
        }, AplParser.ParseScript(DefaultActionLists.Read(hero, DefaultActionLists.DefaultName)!));
        var dungeon = DefaultDungeons.Default;
        var result = simulator.Run(SimTime.FromSeconds(dungeon.TimeLimitSeconds), iterations: 5, degreeOfParallelism: 1,
            fight: new FightOptions(LengthVariance: 0.2, Dungeon: dungeon));

        Assert.True(result.MeanDps > 0);
        Assert.True(result.BossDps > 0);
        Assert.True(result.DurationSeconds < dungeon.TimeLimitSeconds);
        Assert.Equal(0, deadHits);
    }

    [Fact]
    public void A_Dungeons_Chunks_Combine_To_The_Result_Of_One_Run()
    {
        var simulator = new MonteCarloSimulator(Build(), AplParser.ParseScript(DefaultActionLists.Read("Rime", DefaultActionLists.DefaultName)!));
        var dungeon = DefaultDungeons.Default;
        var fight = new FightOptions(LengthVariance: 0.2, Dungeon: dungeon);
        var duration = SimTime.FromSeconds(dungeon.TimeLimitSeconds);
        const int iterations = 6;
        var run = simulator.Run(duration, iterations, degreeOfParallelism: 1, fight: fight);
        // Each chunk goes through JSON, as a web worker sends it.
        var chunks = Enumerable.Range(0, MonteCarloSimulator.ChunkCount(iterations))
            .Select(i => JsonSerializer.Deserialize<MonteCarloChunk>(JsonSerializer.Serialize(simulator.RunChunk(i, duration, iterations, fight: fight)))!)
            .ToList();
        // A chunk has the seconds up to its longest fight's end, not up to the time limit.
        Assert.All(chunks, c => Assert.True(c.Report.DamageSum.Length < dungeon.TimeLimitSeconds));
        var combined = MonteCarloSimulator.Combine(chunks, duration, iterations);
        Assert.Equal(JsonSerializer.Serialize(run), JsonSerializer.Serialize(combined));
    }

    [Fact]
    public void A_Dungeon_With_Trash_Reports_The_Damage_To_Bosses()
    {
        var dungeon = Dungeon(Trash(1, 3, 5), Boss(2, 15));
        var result = new MonteCarloSimulator(Build(), AplParser.ParseScript(FrostBolts))
            .Run(SimTime.FromSeconds(600), iterations: 1, degreeOfParallelism: 1, fight: new FightOptions(Dungeon: dungeon));
        Assert.True(result.BossDps > 0);
        Assert.Equal(0, result.PrimaryTargetDps);
    }

    [Fact]
    public void Target_Conditions_Read_The_Enemy_And_Its_Pull()
    {
        var hero = HeroImporter.Import(Build());
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        ctx.FightEnd = SimTime.FromSeconds(100);
        ctx.Trash = true;
        ctx.NextBossAt = SimTime.FromSeconds(40);
        var mob = ctx.AddEnemy(1000, 1, "Trash Mob 1", maxHealth: 1000);
        var priority = ctx.AddEnemy(1000, 2, "Priority Mob 2", maxHealth: 1000);
        priority.IsPriority = true;
        mob.FollowTime(ctx, SimTime.Zero, SimTime.FromSeconds(30));
        priority.FollowTime(ctx, SimTime.Zero, SimTime.FromSeconds(30));
        ctx.RunUntil(SimTime.FromSeconds(10));

        void Reads(string text, double expected)
        {
            var expression = AplParser.ParseExpression(text);
            Assert.Equal(expected, expression.EvaluateNumeric(hero, ctx), 6);
            Assert.Equal(expected, BoundExpression.Bind(expression, hero).EvaluateNumeric(ctx), 6);
        }

        ctx.Player.CurrentTarget = mob;
        Reads("target.is_priority", 0);
        Reads("target.is_boss", 0);
        Reads("target.time_to_die", 20);
        Reads("target.health.pct", 100.0 * 2 / 3);
        Reads("dungeon_event.adds.count", 2);
        Reads("dungeon_event.boss.count", 0);
        Reads("dungeon_event.boss.in", 30);
        Reads("dungeon_event.adds.in", PropertyExpression.NeverIn);
        Reads("dungeon_event.fight.remains", 90);
        Reads("fight_remains", 90);
        ctx.Player.CurrentTarget = priority;
        Reads("target.is_priority", 1);

        var bound = BoundExpression.Bind(AplParser.ParseExpression("dungeon_event.fight.remains<10"), hero);
        Assert.Equal(SimTime.FromSeconds(90).Milliseconds, bound.NextChange(ctx).Milliseconds, 2.0);
    }

    [Fact]
    public void An_Enemy_Following_Time_Starts_At_Full_Health_And_Dies_At_Its_End()
    {
        var hero = HeroImporter.Import(Build());
        var ctx = new SimulationContext(hero);
        var enemy = ctx.AddEnemy(1001, 1, "Boss 1", maxHealth: 1000);
        enemy.FollowTime(ctx, SimTime.FromSeconds(20), SimTime.FromSeconds(60));
        ctx.RunUntil(SimTime.FromSeconds(20));
        Assert.Equal(100, enemy.HealthPercent, 6);
        ctx.RunUntil(SimTime.FromSeconds(40));
        Assert.Equal(50, enemy.HealthPercent, 6);
        ctx.RunUntil(SimTime.FromSeconds(60));
        Assert.Equal(0, enemy.HealthPercent, 6);
        Assert.Equal(SimTime.FromSeconds(60), enemy.DiesAt);
    }
}
