using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// Exact replay over small replay scripts built here with the fixture Rime character: every outcome and every damage
/// amount comes from the log, and periodic ticks with a damage log ID fire at the log ticks they reserve.
/// </summary>
public class ExactReplayTests
{
    private static readonly UnitRef Dummy = new(100, 1);
    private const int UnmodeledId = 1_999_001;
    private const int TickId = 1_999_002;

    private static ReplayScript Script(IEnumerable<ReplayCommand> commands, IEnumerable<LogDamage> damage) => new()
    {
        Schema = ReplayScript.SchemaVersion,
        ReportCode = "test",
        FightId = 1,
        FightName = "Test",
        DurationSeconds = 10,
        Player = new ReplayPlayer { Id = 1, Name = "Tester", Hero = "Rime" },
        Character = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json"))),
        Commands =
        [
            new ReplayCommand { Time = 0.5, Kind = CommandKind.EnemyArise, Enemy = Dummy, Name = "Dummy", MaxHp = 1e9, Hp = 1e9 },
            .. commands,
        ],
        Observations = new ReplayObservations { Damage = damage.ToList() },
    };

    private static LogDamage Hit(double time, int id, double amount, bool crit = false) =>
        new() { Time = time, AbilityId = id, AbilityName = id.ToString(), Target = Dummy, Amount = amount, Crit = crit };

    /// <summary>A begin_cast of Frost Bolt at 1s and its complete_cast after the engine's cast time.</summary>
    private static (List<ReplayCommand> Commands, int DamageId, double Completed) FrostBoltCast(ReplayScript probe)
    {
        var bolt = ReplayDriver.CreateHero(probe).GetAbility(FrostBolt.AbilityName)!;
        double completed = 1.0 + bolt.GetAdjustedCastTime().TotalSeconds;
        return ([
            new ReplayCommand { Time = 1.0, Kind = CommandKind.BeginCast, AbilityId = bolt.Id, AbilityName = bolt.Name, Target = Dummy },
            new ReplayCommand { Time = completed, Kind = CommandKind.CompleteCast, AbilityId = bolt.Id, AbilityName = bolt.Name, Target = Dummy },
        ], bolt.DamageLogId, completed);
    }

    /// <summary>Passes every call to the log's outcome source and keeps each <see cref="HitOutcome"/>.</summary>
    private sealed class Recording(IOutcomeSource inner) : IOutcomeSource
    {
        public List<HitOutcome> Outcomes { get; } = new();
        public bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null) => inner.RollProc(procKey, chance, ctx, target);
        public IReadOnlyList<EnemyTarget> SelectTargets(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx) => inner.SelectTargets(damageLogId, candidates, ctx);
        public EnemyTarget? ChooseTarget(int damageLogId, IReadOnlyList<EnemyTarget> candidates, SimulationContext ctx, EnemyTarget? preferred = null) => inner.ChooseTarget(damageLogId, candidates, ctx, preferred);
        public HitOutcome ResolveHit(in HitRequest hit, SimulationContext ctx)
        {
            var outcome = inner.ResolveHit(hit, ctx);
            Outcomes.Add(outcome);
            return outcome;
        }
        public double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx) => inner.Land(landed, outcome, ctx);
        public SimTime TickAt(in TickRequest tick, SimulationContext ctx) => inner.TickAt(tick, ctx);
        public void HitFinished(SimulationContext ctx) => inner.HitFinished(ctx);
        public SimTime ReactionDelay(ReactionHold hold, ReactionDelays reactions, SimulationContext ctx) => inner.ReactionDelay(hold, reactions, ctx);
        public double LoggedHitAmount(int damageLogId, EnemyTarget target, SimulationContext ctx) => inner.LoggedHitAmount(damageLogId, target, ctx);
    }

    [Fact]
    public void Every_Outcome_And_Every_Amount_Comes_From_The_Log()
    {
        var probe = Script([], []);
        var (commands, boltId, completed) = FrostBoltCast(probe);
        var script = Script(commands, [Hit(completed + 0.3, boltId, 12345, crit: true), Hit(3.0, UnmodeledId, 777)]);
        var hero = ReplayDriver.CreateHero(script);
        var dealt = new List<(int Id, double Amount, bool Crit)>();
        hero.OnDamageDealt += (in dmg) => dealt.Add((dmg.AbilityId, dmg.Amount, dmg.IsCrit));
        var driver = new ReplayDriver(hero, script, new ReplayOptions { StopOnFirstFailure = false });
        Assert.IsType<LogOutcomeSource>(driver.Context.Outcomes);
        var recording = new Recording(driver.Context.Outcomes);
        driver.Context.Outcomes = recording;

        var result = driver.Run();

        Assert.NotEmpty(recording.Outcomes);
        Assert.All(recording.Outcomes, o => Assert.Equal(1.0, o.Spread));
        Assert.Equal(script.Observations.Damage.Sum(d => d.UnblockedAmount), driver.Context.TotalDamage, 6);
        // Every log hit is dealt once: the Frost Bolt by its engine hit, the unmodeled one by the replay.
        Assert.Equal(2, result.LogHits);
        Assert.Equal(1, result.MatchedLogHits);
        Assert.Equal(1, result.LoggedHitsDealt);
        Assert.Equal(1, dealt.Count(d => d == (boltId, 12345, true)));
        Assert.Equal(1, dealt.Count(d => d == (UnmodeledId, 777, false)));
        var bolt = Assert.Single(result.AllHits, h => h.AbilityId == boltId);
        Assert.True(bolt.Crit);
        Assert.Same(script.Observations.Damage[0], bolt.Log);
        Assert.Contains(result.Issues, i => i.Category == IssueCategory.Hits && i.Observed == 777 && i.Expected is { Low: 0, High: 0 });
        ExactReplayAssertions.Hold(result);
    }

    [Fact]
    public void An_Engine_Hit_With_No_Log_Hit_Deals_Nothing_And_Has_Its_Range_And_A_Hits_Issue()
    {
        var (commands, boltId, _) = FrostBoltCast(Script([], []));
        var script = Script(commands, []);
        // Frost Bolt is one of the damage IDs whose unmatched hits hit nothing by default.
        var options = new ReplayOptions { StopOnFirstFailure = false, UnmatchedHitsMissIds = [] };
        var driver = new ReplayDriver(ReplayDriver.CreateHero(script), script, options);

        var result = driver.Run();

        var bolt = Assert.Single(result.AllHits, h => h.AbilityId == boltId);
        Assert.Null(bolt.Log);
        Assert.True(bolt.ExpectedLow > 0 && bolt.ExpectedHigh > bolt.ExpectedLow);
        Assert.InRange(bolt.EngineAmount, bolt.ExpectedLow, bolt.ExpectedHigh);
        Assert.NotNull(bolt.State);
        var issue = Assert.Single(result.Issues, i => i.Category == IssueCategory.Hits && i.Subject!.Contains(FrostBolt.AbilityName));
        Assert.Equal(bolt.ExpectedLow, issue.Expected!.Low);
        Assert.Equal(bolt.ExpectedHigh, issue.Expected.High);
        Assert.Equal(0, issue.Observed);
        Assert.Equal(-bolt.ExpectedLow, issue.Deviation!.Value, 9);
        Assert.Equal(0, driver.Context.TotalDamage);
    }

    // ------------------------------------------------------------------ periodic ticks

    /// <summary>A fight with the log's tick hits and nothing else: the hero, the dummy and the log's outcome source.</summary>
    private sealed class TickFight
    {
        public TickFight(params double[] logTicks) : this(logTicks.Select(t => Hit(t, TickId, 100))) { }

        public TickFight(IEnumerable<LogDamage> logHits)
        {
            var script = Script([], logHits);
            Hero = ReplayDriver.CreateHero(script);
            Ctx = new SimulationContext(Hero);
            Hero.ResetForIteration();
            Hero.BeginFight(Ctx);
            Enemy = Ctx.AddEnemy(Dummy.Id, Dummy.Instance, "Dummy", 1e9);
            Hero.CurrentTarget = Enemy;
            var options = new ReplayOptions { StopOnFirstFailure = false };
            Ledger = new HitLedger(script.Observations.Damage);
            Verifier = new ReplayVerifier(script, options, Hero);
            Ctx.Outcomes = new LogOutcomeSource(script, options, Ledger, Verifier);
            Hero.OnDamageDealt += (in dmg) => { if (dmg.AbilityId == TickId) TickTimes.Add(dmg.Timestamp.TotalSeconds); };
        }

        public Hero Hero { get; }
        public SimulationContext Ctx { get; }
        public EnemyTarget Enemy { get; }
        public HitLedger Ledger { get; }
        public ReplayVerifier Verifier { get; }
        /// <summary>When each tick hit landed (a matched hit lands at its log hit's time).</summary>
        public List<double> TickTimes { get; } = new();
        /// <summary>When each tick fired (a tick's hit is released as it fires).</summary>
        public List<double> Fired { get; } = new();

        public PeriodicEffect Dot(double seconds, double period = 1.0) =>
            PeriodicEffect.Begin(Ctx, "Test Dot", seconds, period, tickOnStart: false, partialFinalTick: false,
                onTick: (c, _) => Tick(c), damageLogId: TickId, target: Enemy);

        public void Tick(SimulationContext c)
        {
            Fired.Add(c.CurrentTime.TotalSeconds);
            c.ApplyDamage(Hero, Enemy, 100, TickId, "Test Dot", canCrit: false, 0.0, 1.0);
        }
    }

    [Fact]
    public void Each_Tick_Fires_At_Its_Reserved_Log_Tick()
    {
        double[] log = [1.020, 2.010, 3.020, 4.010];
        var fight = new TickFight(log);
        fight.Dot(4.0);

        fight.Ctx.RunUntil(SimTime.FromSeconds(6));

        Assert.Equal(log, fight.Fired);
        Assert.Equal(log, fight.TickTimes);
        Assert.Equal(4, fight.Ledger.MatchedHits);
        Assert.Empty(fight.Verifier.Issues);
    }

    [Fact]
    public void A_Hasted_Tick_Fires_At_Its_Reserved_Log_Tick()
    {
        // Each tick is due one hasted period after the one before; the log has each 20ms later.
        var probe = new TickFight();
        double period = 1.0 / (1.0 + probe.Hero.GetHaste(SimTime.Zero));
        var log = new List<double>();
        for (double t = 0; ;)
        {
            t = (SimTime.FromSeconds(t) + SimTime.FromSeconds(period)).TotalSeconds + 0.020;
            if (t > 3.9) break;
            log.Add(Math.Round(t, 3));
        }
        var fight = new TickFight(log.ToArray());
        // The effect ends half a period after the last log tick, before another tick is due.
        PeriodicEffect.BeginHasted(fight.Ctx, fight.Hero, "Test Dot", log[^1] + period / 2, 1.0, tickOnStart: false, partialFinalTick: false,
            onTick: (c, _) => fight.Tick(c), damageLogId: TickId, target: fight.Enemy);

        fight.Ctx.RunUntil(SimTime.FromSeconds(6));

        Assert.Equal(log, fight.Fired);
        Assert.Equal(log.Count, fight.Ledger.MatchedHits);
        // 20ms from the prediction is within the tick time tolerance (a server frame).
        Assert.Empty(fight.Verifier.Issues);
    }

    [Fact]
    public void A_Damage_Over_Time_Tick_Fires_At_Its_Reserved_Log_Tick()
    {
        // The period timer's next period runs from the moved tick: each tick is due one hasted period after the log tick
        // before it, and the log has each 20ms later.
        var probe = new TickFight();
        double period = 1.0 / (1.0 + probe.Hero.GetHaste(SimTime.Zero));
        var log = new List<double>();
        for (double t = 0; ;)
        {
            t = (SimTime.FromSeconds(t) + SimTime.FromSeconds(period)).TotalSeconds + 0.020;
            if (t > 3.9) break;
            log.Add(Math.Round(t, 3));
        }
        var fight = new TickFight(log.ToArray());
        PeriodicEffect.BeginHasted(fight.Ctx, fight.Hero, "Test Dot", log[^1] + period / 2, 1.0, tickOnStart: false, partialFinalTick: false,
            onTick: (c, _) => fight.Tick(c), damageLogId: TickId, target: fight.Enemy, damageOverTime: true);

        fight.Ctx.RunUntil(SimTime.FromSeconds(6));

        Assert.Equal(log, fight.Fired);
        Assert.Equal(log.Count, fight.Ledger.MatchedHits);
        Assert.Empty(fight.Verifier.Issues);
    }

    [Fact]
    public void A_Partial_Pulse_Fires_At_Its_Log_Tick_On_The_Logged_Enemies()
    {
        // Two full pulses on both enemies, then a partial pulse (0.7 of a period left) on the first alone. The pulse after
        // the second is due past the end, so it never fires; the partial pulse takes the log tick it reserved.
        var probe = new TickFight();
        double period = 1.0 / (1.0 + probe.Hero.GetHaste(SimTime.Zero));
        var second = new UnitRef(101, 1);
        double first = SimTime.FromSeconds(period).TotalSeconds;
        double secondPulse = (SimTime.FromSeconds(first) + SimTime.FromSeconds(period)).TotalSeconds;
        double duration = 2.7 * period;
        double partialAt = (SimTime.FromSeconds(duration) + SimTime.FromSeconds(AuraContainer.ExpiryLagSeconds)).TotalSeconds + 0.010;
        LogDamage On(UnitRef target, double t, double amount) =>
            new() { Time = t, AbilityId = TickId, AbilityName = "Test Pulse", Target = target, Amount = amount };
        var fight = new TickFight([On(Dummy, first, 100), On(second, first, 100), On(Dummy, secondPulse, 100), On(second, secondPulse, 100),
            On(Dummy, Math.Round(partialAt, 3), 70)]);
        var other = fight.Ctx.AddEnemy(second.Id, second.Instance, "Second", 1e9);
        var struck = new List<(double Time, int Enemy)>();
        fight.Hero.OnDamageDealt += (in dmg) => { if (dmg.AbilityId == TickId) struck.Add((dmg.Timestamp.TotalSeconds, dmg.Target!.Id)); };
        PeriodicEffect.BeginHasted(fight.Ctx, fight.Hero, "Test Pulse", duration, 1.0, tickOnStart: false, partialFinalTick: true,
            onTick: (c, fraction) =>
            {
                fight.Fired.Add(c.CurrentTime.TotalSeconds);
                foreach (var enemy in c.Outcomes.SelectTargets(TickId, [fight.Enemy, other], c))
                {
                    c.ApplyDamage(fight.Hero, enemy, 100 * fraction, TickId, "Test Pulse", canCrit: false, 0.0, 1.0);
                }
            }, damageLogId: TickId);

        fight.Ctx.RunUntil(SimTime.FromSeconds(5));

        Assert.Equal([first, secondPulse, Math.Round(partialAt, 3)], fight.Fired);
        Assert.Equal((Math.Round(partialAt, 3), Dummy.Id), struck[^1]);
        Assert.Equal(5, struck.Count);
        Assert.Equal(5, fight.Ledger.MatchedHits);
        Assert.Empty(fight.Verifier.Issues);
    }

    [Fact]
    public void A_Missing_Log_Tick_Leaves_The_Next_Pairing_In_Place()
    {
        var fight = new TickFight(1.020, 3.030, 4.010);
        fight.Dot(4.0);

        fight.Ctx.RunUntil(SimTime.FromSeconds(6));

        // The second tick has no log tick in its window and fires on the engine's schedule; the third still takes its own.
        Assert.Equal([1.020, 2.0, 3.030, 4.010], fight.Fired);
        Assert.Equal(3, fight.Ledger.MatchedHits);
        var extra = Assert.Single(fight.Verifier.AllHits, h => h.Log == null);
        Assert.Equal(2.0, extra.EngineTime);
        Assert.Single(fight.Verifier.Issues, i => i.Category == IssueCategory.Hits && i.Observed == 0);
        // Four engine ticks, three log ticks.
        var count = Assert.Single(fight.Verifier.Issues, i => i.Category == IssueCategory.Ticks);
        Assert.Equal(4, count.Expected!.Low);
        Assert.Equal(3, count.Observed);
    }

    [Fact]
    public void A_Clipped_Effect_Releases_Its_Reservation()
    {
        // The first effect reserves the 2.02 log tick when its first tick fires, then stops at 1.5. The effect that replaces
        // it is due at 2.5 and takes that log tick.
        var fight = new TickFight(1.020, 2.020);
        var first = fight.Dot(4.0);
        PeriodicEffect? second = null;
        fight.Ctx.ScheduleAt(SimTime.FromSeconds(1.5), c =>
        {
            first.Stop(c);
            second = fight.Dot(4.0);
        });

        fight.Ctx.RunUntil(SimTime.FromSeconds(3.0));

        Assert.False(first.IsActive);
        Assert.Equal(1, first.TicksFired);
        Assert.Equal([1.020, 2.020], fight.Fired);
        Assert.Equal(2, fight.Ledger.MatchedHits);
        Assert.Equal(1, second!.TicksFired);
    }

    [Fact]
    public void Two_Applications_On_One_Target_Keep_Their_Own_Log_Ticks()
    {
        var fight = new TickFight(1.010, 1.520, 2.010, 2.520);
        var fired = new Dictionary<string, List<double>> { ["a"] = new(), ["b"] = new() };
        PeriodicEffect Dot(string key) => PeriodicEffect.Begin(fight.Ctx, "Test Dot", 2.0, 1.0, tickOnStart: false, partialFinalTick: false,
            onTick: (c, _) =>
            {
                fired[key].Add(c.CurrentTime.TotalSeconds);
                c.ApplyDamage(fight.Hero, fight.Enemy, 100, TickId, "Test Dot", canCrit: false, 0.0, 1.0);
            }, damageLogId: TickId, target: fight.Enemy);
        Dot("a");
        fight.Ctx.ScheduleAt(SimTime.FromSeconds(0.5), _ => Dot("b"));

        fight.Ctx.RunUntil(SimTime.FromSeconds(4));

        Assert.Equal([1.010, 2.010], fired["a"]);
        Assert.Equal([1.520, 2.520], fired["b"]);
        Assert.Equal(4, fight.Ledger.MatchedHits);
        Assert.Empty(fight.Verifier.Issues);
    }
}
