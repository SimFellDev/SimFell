using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// Each check of <see cref="ReplayVerifier"/> with a logged value inside the engine's range, below it and above it (or the
/// one side of a one-sided range): the issue's expected range, observed value, deviation and relative deviation.
/// </summary>
public class ReplayVerifierTests
{
    private const int DamageId = 1_000_001;
    private const int Precision = 9;

    private readonly Hero _hero;
    private readonly SimulationContext _ctx;
    private readonly EnemyTarget _enemy;
    private readonly ReplayVerifier _verifier;

    public ReplayVerifierTests()
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        _hero = HeroImporter.Import(build);
        _ctx = new SimulationContext(_hero);
        _hero.ResetForIteration();
        _hero.BeginFight(_ctx);
        _enemy = _ctx.AddEnemy(100, 1, "Dummy", 1e9);
        _hero.CurrentTarget = _enemy;
        // Default tolerances: the damage spread, rounding, no unexplained margin.
        _verifier = new ReplayVerifier(new ReplayScript(), new ReplayOptions(), _hero);
    }

    private HitRequest Hit(double baseDamage, bool canCrit = true, double tickFraction = 1.0, bool? forcedCrit = null) =>
        new(_hero, _enemy, DamageId, "Test Hit", baseDamage, canCrit, 0.25, 2.0, _ctx.CurrentTime, tickFraction, forcedCrit);

    private static LogDamage Log(double amount, bool crit = false, double overkill = 0) =>
        new() { Time = 0, AbilityId = DamageId, AbilityName = "Test Hit", Target = new UnitRef(100, 1), Amount = amount, Crit = crit, Overkill = overkill };

    private ReplayIssue? Only(IssueCategory category) => _verifier.Issues.SingleOrDefault(i => i.Category == category);

    private static void AssertIssue(ReplayIssue? issue, double low, double high, double observed, double deviation, double? relative)
    {
        Assert.NotNull(issue);
        Assert.NotNull(issue.Expected);
        Assert.Equal(low, issue.Expected.Low, Precision);
        Assert.Equal(high, issue.Expected.High, Precision);
        Assert.Equal(observed, issue.Observed!.Value, Precision);
        Assert.Equal(deviation, issue.Deviation!.Value, Precision);
        if (relative is { } r) Assert.Equal(r, issue.RelativeDeviation!.Value, Precision);
        else Assert.Null(issue.RelativeDeviation);
        Assert.NotNull(issue.State);
    }

    // ------------------------------------------------------------------ hits

    [Theory]
    [InlineData(1000.0, false)]
    [InlineData(850.0, true)]
    [InlineData(1200.0, true)]
    public void Hit_Damage(double logged, bool flagged)
    {
        // Average 1000: 1000 x 0.9 - 1 to 1000 x 1.1 + 1.
        _verifier.CheckHit(_ctx, Hit(1000), Log(logged), 0);
        var issue = Only(IssueCategory.Damage);
        if (!flagged) { Assert.Null(issue); return; }
        double bound = logged < 899 ? 899 : 1101;
        AssertIssue(issue, 899, 1101, logged, logged - bound, (logged - bound) / bound);
    }

    [Theory]
    [InlineData(2000.0, false)]
    [InlineData(1700.0, true)]
    [InlineData(2400.0, true)]
    public void Hit_Damage_Of_A_Logged_Crit(double logged, bool flagged)
    {
        // A logged crit's average is the base damage times the crit multiplier: 2000.
        var pair = _verifier.CheckHit(_ctx, Hit(1000), Log(logged, crit: true), 0);
        Assert.Equal(2000, pair.EngineAmount, Precision);
        Assert.Equal(1799, pair.ExpectedLow, Precision);
        Assert.Equal(2201, pair.ExpectedHigh, Precision);
        var issue = Only(IssueCategory.Damage);
        if (!flagged) { Assert.Null(issue); return; }
        double bound = logged < 1799 ? 1799 : 2201;
        AssertIssue(issue, 1799, 2201, logged, logged - bound, (logged - bound) / bound);
    }

    [Theory]
    [InlineData(500.0, false)]
    [InlineData(300.0, true)]
    [InlineData(700.0, true)]
    public void Partial_Tick_Damage(double logged, bool flagged)
    {
        // Half a tick of 1000: 500 x 0.9 - 1 - 1000 x 0.1 to 500 x 1.1 + 1 + 1000 x 0.1.
        _verifier.CheckHit(_ctx, Hit(500, tickFraction: 0.5), Log(logged), 0);
        var issue = Only(IssueCategory.Damage);
        if (!flagged) { Assert.Null(issue); return; }
        double bound = logged < 349 ? 349 : 651;
        AssertIssue(issue, 349, 651, logged, logged - bound, (logged - bound) / bound);
    }

    [Theory]
    [InlineData(300.0, false)]
    [InlineData(1200.0, true)]
    public void Damage_With_Overkill(double logged, bool flagged)
    {
        // Overkill cuts the logged amount: 0 to the upper bound.
        _verifier.CheckHit(_ctx, Hit(1000), Log(logged, overkill: 5000), 0);
        var issue = Only(IssueCategory.Damage);
        if (!flagged) { Assert.Null(issue); return; }
        AssertIssue(issue, 0, 1101, logged, logged - 1101, (logged - 1101) / 1101);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Crit_On_A_Hit_That_Cannot_Crit(bool loggedCrit, bool flagged)
    {
        _verifier.CheckHit(_ctx, Hit(1000, canCrit: false), Log(loggedCrit ? 2000 : 1000, crit: loggedCrit), 0);
        var issue = Only(IssueCategory.Hits);
        if (!flagged) { Assert.Null(issue); return; }
        AssertIssue(issue, 0, 0, 1, 1, null);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void Forced_Crit(bool forced, bool loggedCrit, bool flagged)
    {
        _verifier.CheckHit(_ctx, Hit(1000, forcedCrit: forced), Log(loggedCrit ? 2000 : 1000, crit: loggedCrit), 0);
        var issue = Only(IssueCategory.Hits);
        if (!flagged) { Assert.Null(issue); return; }
        double expected = forced ? 1 : 0, observed = loggedCrit ? 1 : 0;
        AssertIssue(issue, expected, expected, observed, observed - expected, forced ? -1.0 : null);
    }

    [Fact]
    public void Engine_Hit_With_No_Log_Hit()
    {
        // The base damage at release: 1000, observed 0 (below the range).
        var pair = _verifier.EngineHitWithoutLogHit(_ctx, Hit(1000));
        Assert.Null(pair.Log);
        Assert.Equal(1000, pair.EngineAmount, Precision);
        AssertIssue(Only(IssueCategory.Hits), 899, 1101, 0, -899, -1.0);
        Assert.Equal(899, pair.ExpectedLow, Precision);
        Assert.Equal(1101, pair.ExpectedHigh, Precision);
    }

    [Fact]
    public void Log_Hit_With_No_Engine_Hit()
    {
        // The engine expects nothing: 0, observed the logged amount (above the range).
        _verifier.LogHitWithoutEngineHit(_ctx, Log(1500), _enemy);
        AssertIssue(Only(IssueCategory.Hits), 0, 0, 1500, 1500, null);
    }

    // ------------------------------------------------------------------ casts

    private Ability Bolt => _hero.GetAbility(FrostBolt.AbilityName)!;

    [Theory]
    [InlineData(2.0, false)]
    [InlineData(1.5, true)]
    [InlineData(2.5, true)]
    public void Cast_Time(double logged, bool flagged)
    {
        // A 2s engine cast: 1.9 to 2.1.
        _verifier.CheckCastTime(_ctx, Bolt, 2.0, logged);
        var issue = Only(IssueCategory.Casts);
        if (!flagged) { Assert.Null(issue); return; }
        double bound = logged < 1.9 ? 1.9 : 2.1;
        AssertIssue(issue, 1.9, 2.1, logged, logged - bound, (logged - bound) / bound);
        Assert.NotNull(issue!.State!.Cast);
    }

    [Theory]
    [InlineData(2.0, 2.05, false)]
    [InlineData(2.0, 0.0, true)]
    [InlineData(0.0, 1.2, true)]
    public void Instant_Or_Cast(double engine, double logged, bool flagged)
    {
        // A logged cast (0) against a 2s engine cast is below 1.9; a logged 1.2s begin_cast against an engine instant is
        // above 0.1.
        _verifier.CheckInstantOrCast(_ctx, Bolt, engine, logged, logged == 0 ? CommandKind.Cast : CommandKind.BeginCast);
        var issue = Only(IssueCategory.Casts);
        if (!flagged) { Assert.Null(issue); return; }
        double low = Math.Max(0, engine - 0.1), high = engine + 0.1;
        double bound = logged < low ? low : high;
        AssertIssue(issue, low, high, logged, logged - bound, (logged - bound) / bound);
    }

    [Theory]
    [InlineData(0.05, false)]
    [InlineData(1.0, true)]
    public void Global_Cooldown(double globalCooldownEnd, bool flagged)
    {
        // The cast at 0 comes no sooner than the global cooldown's end less 0.1s.
        _hero.GlobalCooldownEndTime = SimTime.FromSeconds(globalCooldownEnd);
        foreach (var issue in _verifier.CastRefusals(_ctx, Bolt)) _verifier.Report(issue);
        var gcd = Only(IssueCategory.GlobalCooldown);
        if (!flagged) { Assert.Null(gcd); return; }
        AssertIssue(gcd, globalCooldownEnd - 0.1, double.PositiveInfinity, 0, -(globalCooldownEnd - 0.1), -1.0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cooldown(bool onCooldown)
    {
        var ability = _hero.Abilities.First(a => a.Cooldown > SimTime.Zero && a.MaxCharges == 1);
        if (onCooldown) ability.TriggerCooldown(_ctx);
        foreach (var issue in _verifier.CastRefusals(_ctx, ability)) _verifier.Report(issue);
        var cooldown = Only(IssueCategory.Cooldown);
        if (!onCooldown) { Assert.Null(cooldown); return; }
        // The cast at 0 comes no sooner than now plus the cooldown left.
        double remains = _hero.GetCooldownRemains(ability.Name, _ctx.CurrentTime);
        Assert.True(remains > 0);
        AssertIssue(cooldown, remains, double.PositiveInfinity, 0, -remains, -1.0);
    }

    [Fact]
    public void Other_Refusal_Has_Its_Reason_And_No_Numeric_Range()
    {
        var refused = _hero.Abilities.First(a => !a.CanCast(_ctx) && (a.Cooldown == SimTime.Zero || _hero.CooldownOf(a).ChargesAt(_ctx.CurrentTime) > 0));
        foreach (var issue in _verifier.CastRefusals(_ctx, refused)) _verifier.Report(issue);
        var other = Only(IssueCategory.Casts);
        Assert.NotNull(other);
        Assert.Null(other.Expected);
        Assert.Null(other.Observed);
        Assert.Null(other.Deviation);
        Assert.NotNull(other.State!.Cast!.Refusal);

        _verifier.Issues.Clear();
        foreach (var issue in _verifier.CastRefusals(_ctx, Bolt)) _verifier.Report(issue);
        Assert.Null(Only(IssueCategory.Casts));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Still_Casting(bool beforeTheEnd)
    {
        Assert.True(_ctx.BeginCast(_hero, Bolt, _enemy).Succeeded);
        var cast = _hero.CurrentCast!;
        // The engine completes the cast later than its cast time (the log's completion), so it is still current after its end.
        _ctx.SetCastCompletion(_hero, cast.EndTime + SimTime.FromSeconds(1));
        if (!beforeTheEnd) _ctx.RunUntil(cast.EndTime + SimTime.FromSeconds(0.1));
        var issue = _verifier.StillCasting(_ctx, _hero.GetAbility(IceComet.AbilityName)!);
        if (!beforeTheEnd) { Assert.Null(issue); return; }
        double end = cast.EndTime.TotalSeconds;
        AssertIssue(issue, end, double.PositiveInfinity, 0, -end, -1.0);
    }

    // ------------------------------------------------------------------ periodic ticks

    [Theory]
    [InlineData(2.0, false)]
    [InlineData(1.9, true)]
    [InlineData(2.1, true)]
    public void Periodic_Tick_Time(double logged, bool flagged)
    {
        var effect = PeriodicEffect.Begin(_ctx, "Test Dot", 5.0, 1.0, tickOnStart: false, partialFinalTick: false, onTick: (_, _) => { });
        _verifier.CheckTickTime(_ctx, effect, SimTime.FromSeconds(2.0), logged);
        var issue = Only(IssueCategory.Ticks);
        if (!flagged) { Assert.Null(issue); return; }
        double tolerance = PeriodicEffect.ServerFrameSeconds;
        double bound = logged < 2.0 ? 2.0 - tolerance : 2.0 + tolerance;
        AssertIssue(issue, 2.0 - tolerance, 2.0 + tolerance, logged, logged - bound, (logged - bound) / bound);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    public void Periodic_Tick_Count(int reserved, bool flagged)
    {
        var effect = PeriodicEffect.Begin(_ctx, "Test Dot", 3.0, 1.0, tickOnStart: false, partialFinalTick: false, onTick: (_, _) => { });
        _ctx.RunUntil(SimTime.FromSeconds(3.0));
        Assert.Equal(3, effect.TicksFired);
        _verifier.CheckTickCount(_ctx, effect, reserved);
        var issue = Only(IssueCategory.Ticks);
        if (!flagged) { Assert.Null(issue); return; }
        AssertIssue(issue, 3, 3, reserved, reserved - 3, (reserved - 3) / 3.0);
    }

    // ------------------------------------------------------------------ state before a cast

    [Theory]
    [InlineData(5.0, false)]
    [InlineData(3.0, true)]
    [InlineData(7.0, true)]
    public void Pre_Cast_Resource(double logged, bool flagged)
    {
        // Engine 5 with a tolerance of 1: 4 to 6.
        _verifier.CheckResource(_ctx, new ReplayStep(), "Anima", 5.0, logged, 1.0);
        var issue = Only(IssueCategory.Resources);
        if (!flagged) { Assert.Null(issue); return; }
        double bound = logged < 5 ? 4 - 1e-9 : 6 + 1e-9;
        AssertIssue(issue, 4 - 1e-9, 6 + 1e-9, logged, logged - bound, (logged - bound) / bound);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public void Pre_Cast_Aura(int logged, bool flagged)
    {
        _verifier.CheckAura(_ctx, new ReplayStep(), "Test Buff", 2, logged);
        var issue = Only(IssueCategory.Auras);
        if (!flagged) { Assert.Null(issue); return; }
        AssertIssue(issue, 2, 2, logged, logged - 2, (logged - 2) / 2.0);
    }

    [Theory]
    [InlineData(0.0, false, false)]
    [InlineData(0.5, true, false)]
    [InlineData(0.0, true, true)]
    public void Proc_With_Zero_Chance(double chance, bool procced, bool flagged)
    {
        _verifier.CheckProc(_ctx, "test_proc", chance, procced, _enemy);
        var issue = Only(IssueCategory.Procs);
        if (!flagged) { Assert.Null(issue); return; }
        AssertIssue(issue, 0, 0, 1, 1, null);
    }
}
