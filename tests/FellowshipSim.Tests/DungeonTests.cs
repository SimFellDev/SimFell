using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Dungeon fights: the dungeon profile's text, the segments in turn with their enemies, the dungeon_event and target
/// conditions that read them, and old action-list names in the new ones.
/// </summary>
public class DungeonTests
{
    private static CharacterBuild Build() => JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;

    private static DungeonProfile Dungeon(params DungeonSegment[] segments) => new("Test", segments);

    // A cast is reported as it completes, so one begun before a segment ends can complete up to a cast time after it.
    private const double CastTime = 2.5;

    // The casts of the typical fight of a dungeon, with an action list of one frost bolt under the condition.
    private static IReadOnlyList<CastReport> Casts(DungeonProfile dungeon, string condition) =>
        new MonteCarloSimulator(Build(), AplParser.ParseScript($"actions+=/frost_bolt,if={condition}"))
            .Run(SimTime.FromSeconds(dungeon.LengthSeconds), iterations: 1, degreeOfParallelism: 1, fight: new FightOptions(Dungeon: dungeon))
            .Casts.Where(c => c.Name == "Frost Bolt").ToList();

    [Fact]
    public void The_Default_Dungeon_Reads_Back_From_Its_Text()
    {
        var dungeon = DefaultDungeons.Default;
        Assert.Equal(DefaultDungeons.DefaultName, dungeon.Name);
        Assert.Equal(300, dungeon.LengthSeconds);
        Assert.Null(dungeon.Problem());
        Assert.Equal(dungeon.Format(), DungeonProfile.Parse(dungeon.Format()).Format());
        Assert.Contains(dungeon.Segments, s => s.Kind == DungeonSegmentKind.Council && s.Final);
    }

    [Fact]
    public void A_Creature_With_A_Comma_Is_Quoted()
    {
        var dungeon = Dungeon(new DungeonSegment(DungeonSegmentKind.Boss, 90, Final: true, Creature: "Xul’vorith, Sentinel of the Bloodbound Gate"));
        string text = dungeon.Format();
        Assert.Contains("creature=\"Xul’vorith, Sentinel of the Bloodbound Gate\"", text);
        Assert.Equal(dungeon.Segments[0], DungeonProfile.Parse(text).Segments[0]);
    }

    [Theory]
    [InlineData("segment=trash,enemies=5,length=60", "no dungeon=<name> line")]
    [InlineData("dungeon=X", "no segment line")]
    [InlineData("dungeon=X\nsegment=pack,length=60", "'pack' is not a segment kind")]
    [InlineData("dungeon=X\nsegment=trash,enemies=2,priority=3,length=60", "priority targets are 0 to the enemy count")]
    [InlineData("dungeon=X\nsegment=trash,enemies=2,length=60,final=1", "only a boss or a council is the final boss")]
    [InlineData("dungeon=X\nsegment=boss,enemies=2,length=60", "a boss segment has one enemy")]
    [InlineData("dungeon=X\nsegment=boss,length=0", "the length is more than 0 seconds")]
    public void A_Dungeon_Text_Names_What_Is_Wrong(string text, string problem)
    {
        var e = Assert.Throws<FormatException>(() => DungeonProfile.Parse(text));
        Assert.Contains(problem, e.Message);
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
    public void Each_Segment_Runs_For_Its_Share_Of_The_Fight()
    {
        var dungeon = Dungeon(new DungeonSegment(DungeonSegmentKind.Trash, 20, 5), new DungeonSegment(DungeonSegmentKind.Boss, 20, Final: true));
        var boss = Casts(dungeon, "dungeon_event.boss.exists");
        Assert.NotEmpty(boss);
        Assert.All(boss, c => Assert.True(c.Time >= 20));
        var trash = Casts(dungeon, "dungeon_event.adds.exists&dungeon_event.adds.count=5");
        Assert.NotEmpty(trash);
        Assert.All(trash, c => Assert.True(c.Time < 20 + CastTime));
    }

    [Fact]
    public void Only_The_Final_Boss_Reads_Final()
    {
        var dungeon = Dungeon(new DungeonSegment(DungeonSegmentKind.Boss, 20), new DungeonSegment(DungeonSegmentKind.Council, 20, 3, Final: true));
        var final = Casts(dungeon, "dungeon_event.boss.final&dungeon_event.boss.count=3");
        Assert.NotEmpty(final);
        Assert.All(final, c => Assert.True(c.Time >= 20));
        var notFinal = Casts(dungeon, "dungeon_event.boss.exists&!dungeon_event.boss.final");
        Assert.NotEmpty(notFinal);
        Assert.All(notFinal, c => Assert.True(c.Time < 20 + CastTime));
    }

    [Fact]
    public void A_Countdown_Wakes_The_Hero_When_It_Crosses()
    {
        // Nothing else happens in the fight, so only the countdown's crossing lets the hero cast before the boss comes.
        var dungeon = Dungeon(new DungeonSegment(DungeonSegmentKind.Trash, 30, 1), new DungeonSegment(DungeonSegmentKind.Boss, 30));
        var casts = Casts(dungeon, "dungeon_event.boss.in<5&dungeon_event.boss.in>0");
        Assert.NotEmpty(casts);
        Assert.All(casts, c => Assert.InRange(c.Time, 25, 30 + CastTime));
    }

    public static TheoryData<string> Heroes() => new(Core.Catalog.Heroes.Keys);

    [Theory]
    [MemberData(nameof(Heroes))]
    public void Every_Hero_Fights_The_Default_Dungeon_And_Hits_No_Dead_Enemy(string hero)
    {
        var build = JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{hero.ToLowerInvariant()}_character.json")))!;
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
        var result = simulator.Run(SimTime.FromSeconds(dungeon.LengthSeconds), iterations: 20, degreeOfParallelism: 1,
            fight: new FightOptions(LengthVariance: 0.2, HealthFollowsTime: true, Dungeon: dungeon));

        Assert.True(result.MeanDps > 0);
        Assert.True(result.BossDps > 0);
        Assert.Equal(0, deadHits);
    }

    [Fact]
    public void A_Dungeon_With_Trash_Reports_The_Damage_To_Bosses()
    {
        var dungeon = Dungeon(new DungeonSegment(DungeonSegmentKind.Trash, 30, 3), new DungeonSegment(DungeonSegmentKind.Boss, 30));
        var result = new MonteCarloSimulator(Build(), AplParser.ParseScript("actions+=/frost_bolt,if=dungeon_event.boss.exists"))
            .Run(SimTime.FromSeconds(60), iterations: 3, degreeOfParallelism: 1, fight: new FightOptions(Dungeon: dungeon));
        // All the damage is in the boss's half, so the boss DPS is about twice the mean.
        Assert.InRange(result.BossDps / result.MeanDps, 1.8, 2.2);
        Assert.Equal(0, result.PrimaryTargetDps);
    }

    [Fact]
    public void Target_Conditions_Read_The_Enemy_And_Its_Segment()
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
    public void A_Segments_Enemy_Starts_At_Full_Health_And_Dies_At_Its_End()
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
