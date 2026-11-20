using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Debugging;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using Superpower;

namespace FellowshipSim.Tests.Apl;

/// <summary>
/// The <c>switch_target</c> action (<see cref="AplSwitchTarget"/>): its text, the check's problem for a line without
/// <c>target_if</c>, the hero's target after each decision, and the steps a traced fight shows.
/// </summary>
public class AplSwitchTargetTests
{
    private static readonly AplVocabulary Gunde = AplVocabulary.For("Gunde");

    private static CharacterBuild Build() => JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gunde_character.json")))!;

    private const string TrashName = "Trash Mob", BossName = "Boss";

    private const double TrashHitPoints = 10_000, BossHitPoints = 60_000;

    private static EnemyGroup Trash(int count = 1, double? dies = null) => new(count, TrashName, 3, HitPoints: TrashHitPoints, DiesSeconds: dies);

    private static EnemyGroup Boss(double firstSeen = 0) => new(1, BossName, 45, HitPoints: BossHitPoints, Boss: true, FirstSeenSeconds: firstSeen);

    private static DungeonProfile Dungeon(double seconds, params EnemyGroup[] groups) =>
        new("Test", 600, [new DungeonPull("1", groups, seconds, Final: groups.Any(g => g.Boss))], DifficultyScaling.Base.Eternal);

    // One enemy of trash from the pull's start to 20 s, and a boss entering combat at 5 s until the pull's 30 s.
    private static readonly DungeonProfile BossAfterTrash = Dungeon(30, Trash(dies: 20), Boss(firstSeen: 5));

    private const string SwitchToBoss = "actions=switch_target,target_if=target.is_boss";

    // The weapon swing: one hit on the hero's current target each swing.
    private const string Swing = "actions+=/attack";

    private static AplFightTrace Trace(string actions, DungeonProfile dungeon, CharacterBuild? build = null) =>
        new MonteCarloSimulator(build ?? Build(), AplParser.ParseScript(actions))
            .TraceFight(0, SimTime.FromSeconds(dungeon.TimeLimitSeconds), options: new FightOptions(Dungeon: dungeon));

    // The steps of a decision at the switch_target line, the list's first line.
    private static AplStepTrace SwitchStep(AplDecision decision) => Assert.Single(decision.Steps, s => s.Line == 0);

    // A fight outside a dungeon with the hero on an enemy of trash and a boss in combat.
    private static (Hero Hero, SimulationContext Ctx, EnemyTarget Trash, EnemyTarget Boss) TrashAndBoss()
    {
        var hero = HeroImporter.Import(Build());
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var trash = ctx.AddEnemy(1001, 1, TrashName, maxHealth: 1000);
        var boss = ctx.AddEnemy(1001, 2, BossName, maxHealth: 5000);
        boss.IsBoss = true;
        ctx.Player.CurrentTarget = trash;
        return (hero, ctx, trash, boss);
    }

    [Fact]
    public void A_Switch_Target_Line_Reads_Back_From_Its_Text()
    {
        const string text = """
            actions+=/switch_target,target_if=target.is_boss
            actions+=/switch_target,target_if=max:target.health.pct,if=active_enemies>1
            actions+=/attack,target_if=min:target.health.pct

            """;
        var parsed = AplParser.ParseScript(text).Actions;
        Assert.Equal([true, true, false], parsed.Select(a => a.SwitchTarget));
        Assert.Equal([AplTargetSelection.First, AplTargetSelection.Max, AplTargetSelection.Min], parsed.Select(a => a.TargetIf!.Selection));
        Assert.Null(parsed[0].Condition);
        Assert.NotNull(parsed[1].Condition);

        // The visual editor's actions, and the text they write.
        var actions = AplDocument.Parse(text);
        Assert.Equal(
            [
                new AplDocumentAction(AplSwitchTarget.ActionName, TargetSelection: AplTargetSelection.First, TargetExpression: "target.is_boss"),
                new AplDocumentAction(AplSwitchTarget.ActionName, "active_enemies > 1", AplTargetSelection.Max, "target.health.pct"),
                new AplDocumentAction("attack", TargetSelection: AplTargetSelection.Min, TargetExpression: "target.health.pct"),
            ],
            actions);
        var (written, errors) = AplDocument.Write(actions);
        Assert.Empty(errors);
        Assert.Equal("""
            actions+=/switch_target,target_if=first:target.is_boss
            actions+=/switch_target,target_if=max:target.health.pct,if=active_enemies > 1
            actions+=/attack,target_if=min:target.health.pct

            """.Replace("\r\n", "\n"), written);
        Assert.Equal(actions, AplDocument.Parse(written!));
        Assert.Equal(parsed.Select(a => a.SwitchTarget), AplParser.ParseScript(written!).Actions.Select(a => a.SwitchTarget));
    }

    [Fact]
    public void A_Switch_Target_Line_Keeps_An_Ability_Lines_Switches_As_Other_Arguments()
    {
        var action = Assert.Single(AplDocument.Parse("actions+=/switch_target,use_off_gcd=1,target_if=target.is_boss"));
        Assert.False(action.UseOffGcd);
        Assert.Equal("use_off_gcd=1", action.Arguments);
    }

    [Fact]
    public void A_Line_Without_Target_If_Is_A_Problem_Of_The_List()
    {
        const string text = "actions+=/attack\nactions+=/switch_target,if=active_enemies>1";
        var e = Assert.Throws<ParseException>(() => AplParser.ParseScript(text));
        Assert.Equal("APL line 2: `switch_target` needs `target_if=<expression>`.", e.Message);

        var check = Gunde.CheckList(text);
        Assert.Equal("Line 2: `switch_target` needs `target_if=<expression>`.", check.Error);
        Assert.Equal(text.IndexOf('\n') + 1, check.ErrorStart);

        // The visual editor names the target's field.
        var (written, errors) = AplDocument.Write([new AplDocumentAction("attack"), new AplDocumentAction(AplSwitchTarget.ActionName, "active_enemies > 1")]);
        Assert.Null(written);
        Assert.Equal(new AplDocumentError(1, "targetExpression", "Enter an expression for target selection."), Assert.Single(errors));
    }

    [Fact]
    public void The_Check_Has_The_Action_As_A_Keyword_And_Its_Target_If_As_An_Ability_Lines()
    {
        const string text = "actions+=/switch_target,target_if=max:target.health.pct,if=target.is_boss";
        var check = Gunde.CheckList(text);
        Assert.Null(check.Error);
        Assert.Equal(
            [
                ("actions", AplTokenKind.List),
                ("switch_target", AplTokenKind.Keyword),
                ("target_if", AplTokenKind.Keyword),
                ("max", AplTokenKind.Keyword),
                ("target.health.pct", AplTokenKind.Named),
                ("if", AplTokenKind.Keyword),
                ("target.is_boss", AplTokenKind.Named),
            ],
            check.Tokens.Select(t => (text.Substring(t.Start, t.Length), t.Kind)));
        Assert.All(check.Tokens, t => Assert.Null(t.Problem));
    }

    [Fact]
    public void With_The_Line_The_Heros_Target_Is_The_Boss_From_The_First_Decision_After_It_Enters()
    {
        var trace = Trace($"{SwitchToBoss}\n{Swing}", BossAfterTrash);

        // Before the boss enters, first finds no enemy and the hero swings at the trash.
        var before = trace.Decisions.Where(d => d.Time < 5).ToList();
        Assert.Contains(before, d => d.Cast == "Attack");
        Assert.All(before, d =>
        {
            Assert.Equal(AplStepOutcome.NoTarget, SwitchStep(d).Outcome);
            Assert.Null(SwitchStep(d).Target);
            if (d.Cast != null) Assert.Equal(TrashName, d.Target);
        });

        // The first decision after: the hero is on the trash as it starts, and the line switches the target.
        var first = trace.Decisions.First(d => d.Time >= 5);
        Assert.Equal(5, first.Time, 0.001);
        Assert.Equal(TrashName, Assert.Single(first.State.Enemies, e => e.Current).Name);
        var step = SwitchStep(first);
        Assert.Equal(AplStepOutcome.TargetSwitched, step.Outcome);
        Assert.Equal(BossName, step.Target);
        Assert.Equal(AplTargetSelection.First, step.TargetIf!.Selection);
        Assert.Equal(BossName, step.TargetIf.Picked);
        Assert.Equal([(TrashName, 0.0), (BossName, 1.0)], step.TargetIf.Candidates.Select(c => (c.Enemy, c.Value.Value)));

        // From then on the target stays the boss, with the trash in combat until 20 s.
        var after = trace.Decisions.Where(d => d.Time >= 5).Skip(1).ToList();
        Assert.Contains(after, d => d.Time < 20 && d.Cast == "Attack");
        Assert.All(after, d =>
        {
            Assert.Equal(BossName, Assert.Single(d.State.Enemies, e => e.Current).Name);
            Assert.Equal(AplStepOutcome.TargetKept, SwitchStep(d).Outcome);
        });
        Assert.All(trace.Decisions.Where(d => d.Time >= 5 && d.Cast != null), d => Assert.Equal(BossName, d.Target));
    }

    [Fact]
    public void Without_The_Line_The_Heros_Target_Stays_The_Trash_Until_It_Dies()
    {
        var trace = Trace("actions=attack", BossAfterTrash);
        var casts = trace.Decisions.Where(d => d.Cast != null).ToList();
        Assert.Contains(casts, d => d.Time > 5 && d.Time < 20);
        Assert.All(casts.Where(d => d.Time < 20), d => Assert.Equal(TrashName, d.Target));
        Assert.Contains(casts, d => d.Time > 20);
        Assert.All(casts.Where(d => d.Time > 20), d => Assert.Equal(BossName, d.Target));
    }

    [Fact]
    public void First_With_No_Match_Keeps_The_Target()
    {
        // Two enemies of trash and no boss.
        var trace = Trace($"{SwitchToBoss}\n{Swing}", Dungeon(20, Trash(count: 2)));
        Assert.All(trace.Decisions, d =>
        {
            var step = SwitchStep(d);
            Assert.Equal(AplStepOutcome.NoTarget, step.Outcome);
            Assert.Null(step.TargetIf!.Picked);
            Assert.Equal($"{TrashName} #1", Assert.Single(d.State.Enemies, e => e.Current).Name);
        });
        Assert.Contains(trace.Decisions, d => d.Cast == "Attack");
        Assert.All(trace.Decisions.Where(d => d.Cast != null), d => Assert.Equal($"{TrashName} #1", d.Target));
    }

    [Fact]
    public void The_If_Reads_The_Enemy_Picked()
    {
        // The pick is the boss, and the if is false at the boss and true at the hero's target, the trash: no switch.
        var kept = Trace($"{SwitchToBoss},if=!target.is_boss\n{Swing}", BossAfterTrash);
        var withBoss = kept.Decisions.Where(d => d.Time >= 5 && d.Time < 20).ToList();
        Assert.Contains(withBoss, d => d.Cast == "Attack");
        Assert.All(withBoss, d =>
        {
            var step = SwitchStep(d);
            Assert.Equal(AplStepOutcome.ConditionFalse, step.Outcome);
            Assert.Equal(BossName, step.Target);
            Assert.Equal(0, step.Condition!.Value);
            if (d.Cast != null) Assert.Equal(TrashName, d.Target);
        });

        // The pick is the enemy with the most health: the trash alone, then the boss. The if is true at the boss alone.
        var switched = Trace($"actions=switch_target,target_if=max:target.health.pct,if=target.is_boss\n{Swing}", BossAfterTrash);
        Assert.All(switched.Decisions.Where(d => d.Time < 5), d => Assert.Equal(AplStepOutcome.ConditionFalse, SwitchStep(d).Outcome));
        Assert.Equal(AplStepOutcome.TargetSwitched, SwitchStep(switched.Decisions.First(d => d.Time >= 5)).Outcome);
        Assert.All(switched.Decisions.Where(d => d.Time >= 5 && d.Cast != null), d => Assert.Equal(BossName, d.Target));
    }

    [Fact]
    public void A_Line_Above_The_Action_Reads_The_Target_From_Before_In_That_Decision()
    {
        const string actions = """
            actions=variable,name=before,value=target.is_boss
            actions+=/switch_target,target_if=target.is_boss
            actions+=/variable,name=after,value=target.is_boss
            actions+=/attack
            """;
        var decisions = Trace(actions, BossAfterTrash).Decisions.Where(d => d.Time >= 5).ToList();

        // The decision of the switch: the variable above reads the trash, and the one below the boss.
        Assert.Equal(0, decisions[0].Steps[0].Value);
        Assert.Equal(AplStepOutcome.TargetSwitched, decisions[0].Steps[1].Outcome);
        Assert.Equal(1, decisions[0].Steps[2].Value);
        // The next decision starts on the boss.
        Assert.Equal(1, decisions[1].Steps[0].Value);
        Assert.Equal(AplStepOutcome.TargetKept, decisions[1].Steps[1].Outcome);
    }

    [Fact]
    public void The_Action_Casts_Nothing_And_The_Decision_Goes_On_At_The_Next_Line()
    {
        var (hero, ctx, trash, boss) = TrashAndBoss();
        var lists = AplParser.ParseScript($"{SwitchToBoss}\n{Swing}");

        // One decision at one time: the switch, then the swing at the boss.
        Assert.True(lists.TryAct(hero, ctx));
        Assert.Same(boss, ctx.Player.CurrentTarget);
        Assert.Equal("Attack", ctx.PreviousCast!.Name);
        Assert.Equal(SimTime.Zero, ctx.CurrentTime);
        ctx.RunUntil(SimTime.FromSeconds(0.5));
        Assert.True(boss.HeroDamage > 0);
        Assert.Equal(0, trash.HeroDamage);

        // A list of the action alone casts nothing, and starts no global cooldown.
        var (hero2, ctx2, _, boss2) = TrashAndBoss();
        Assert.False(AplParser.ParseScript(SwitchToBoss).TryAct(hero2, ctx2));
        Assert.Same(boss2, ctx2.Player.CurrentTarget);
        Assert.Null(ctx2.PreviousCast);
        Assert.True(hero2.GlobalCooldownEndTime <= ctx2.CurrentTime);
    }

    [Fact]
    public void Nothing_Changes_With_No_Enemy_In_Combat_Or_With_The_Pick_The_Target_Already()
    {
        var hero = HeroImporter.Import(Build());
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        Assert.False(AplParser.ParseScript(SwitchToBoss).TryAct(hero, ctx));
        Assert.Null(ctx.Player.CurrentTarget);
        Assert.Empty(ctx.AllEnemies);

        var (hero2, ctx2, trash, _) = TrashAndBoss();
        Assert.False(AplParser.ParseScript("actions=switch_target,target_if=!target.is_boss").TryAct(hero2, ctx2));
        Assert.Same(trash, ctx2.Player.CurrentTarget);
    }

    [Fact]
    public void The_Action_Runs_While_The_Global_Cooldown_Runs()
    {
        var (hero, ctx, trash, boss) = TrashAndBoss();
        var lists = AplParser.ParseScript($"actions=double_strike\nactions+=/switch_target,target_if=target.is_boss");

        // The decision ends at its cast, above the switch.
        Assert.True(lists.TryAct(hero, ctx));
        Assert.Same(trash, ctx.Player.CurrentTarget);
        Assert.True(hero.GlobalCooldownEndTime > ctx.CurrentTime);

        // During the global cooldown Double Strike is no candidate, and the switch runs.
        Assert.False(lists.TryAct(hero, ctx));
        Assert.Same(boss, ctx.Player.CurrentTarget);
    }

    [Fact]
    public void A_Decision_While_A_Cast_Or_A_Channel_Runs_Skips_The_Action()
    {
        // Chronoshift is a cast and then a channel. The swing goes during both, at the hero's target.
        const string actions = """
            actions=chronoshift
            actions+=/switch_target,target_if=target.is_boss
            actions+=/attack,use_while_casting=1
            """;
        var build = Build();
        build.WeaponAbilityId = Chronoshift.AbilityId;
        // The trash and the boss are in combat from the pull's start, and the hero starts on the trash.
        var trace = Trace(actions, Dungeon(60, Trash(), Boss()), build);

        Assert.Equal("Chronoshift", trace.Decisions[0].Cast);
        var switched = trace.Decisions.First(d => d.Steps.Any(s => s.Outcome == AplStepOutcome.TargetSwitched));
        var during = trace.Decisions.Skip(1).TakeWhile(d => d != switched).ToList();
        // The decisions during the cast and the channel leave the line out, and their swings are at the trash.
        Assert.Contains(during, d => d.Cast == "Attack");
        Assert.All(during, d =>
        {
            Assert.DoesNotContain(d.Steps, s => s.Line == 1);
            if (d.Cast != null) Assert.Equal(TrashName, d.Target);
        });
        // The first decision after the channel has the line, and the swings from then on are at the boss.
        Assert.True(switched.Time >= Chronoshift.AbilityCastTime + Chronoshift.AbilityChannelTime * 0.5);
        Assert.All(trace.Decisions.SkipWhile(d => d != switched).Where(d => d.Cast == "Attack"), d => Assert.Equal(BossName, d.Target));
    }

    [Fact]
    public void The_Next_Change_Has_The_Picks_Change_And_The_Conditions_Change_At_The_Pick()
    {
        // The trash dies at 40 s and the boss at 100 s.
        var (hero, ctx, trash, boss) = TrashAndBoss();
        trash.FollowTime(ctx, SimTime.Zero, SimTime.FromSeconds(40));
        boss.FollowTime(ctx, SimTime.Zero, SimTime.FromSeconds(100));

        // first: the trash has less than 30 s to live from 10 s, before the boss from 70 s.
        var pick = AplParser.ParseScript("actions=switch_target,target_if=target.time_to_die<30").Bind(hero);
        Assert.Equal(10_000, pick.NextChange(ctx).Milliseconds, 2.0);

        // The condition is read at the pick, the boss: true from 70 s. At the hero's target, the trash, it is true from 10 s.
        var condition = AplParser.ParseScript($"{SwitchToBoss},if=target.time_to_die<30").Bind(hero);
        Assert.Equal(70_000, condition.NextChange(ctx).Milliseconds, 2.0);
    }

    [Fact]
    public void A_Traced_Fight_With_The_Action_Casts_As_The_Rolled_Fight()
    {
        // A list of the hero's abilities below the switch, on a pull whose boss enters after its trash.
        const string list = """
            actions=switch_target,target_if=target.is_boss
            actions+=/attack
            actions+=/reign_in_blood
            actions+=/heart_splitter,target_if=min:target.health.pct
            actions+=/rupture,if=active_enemies>2
            actions+=/grim_carve
            actions+=/blood_arc
            actions+=/double_strike
            """;
        var dungeon = Dungeon(60, Trash(count: 3, dies: 40), Boss(firstSeen: 10));
        var simulator = new MonteCarloSimulator(Build(), AplParser.ParseScript(list));
        var duration = SimTime.FromSeconds(dungeon.TimeLimitSeconds);
        var options = new FightOptions(Dungeon: dungeon);

        var rolled = simulator.RollFight(2, duration, firstSeed: 5, options: options);
        var trace = simulator.TraceFight(2, duration, firstSeed: 5, options: options);

        Assert.Equal(rolled.Casts, trace.Casts);
        Assert.Contains(trace.Decisions, d => d.Steps.Any(s => s.Outcome == AplStepOutcome.TargetSwitched));
        var decided = trace.Decisions.Where(d => d.Cast != null).Select(d => (d.Time, d.Cast)).ToList();
        var cast = trace.Casts.Select(c => (c.Time, (string?)c.Name)).ToList();
        // A cast still running as the fight ends is not in the fight's casts, which list a cast once it finishes.
        Assert.Equal(cast, decided.Take(cast.Count));
        Assert.InRange(decided.Count - cast.Count, 0, 1);
    }
}
