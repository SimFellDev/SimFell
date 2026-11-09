using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;
using Xunit.Sdk;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The harness itself: the step driver's clock and casts, and the decorators that force outcomes. These tests read the driver's members
/// and events directly, which the functional tests do not.
/// </summary>
public class StepDriverTests
{
    // ---- The clock and casts

    /// <summary>Waiting moves the fight clock by exactly that long.</summary>
    [Fact]
    public void Wait_Moves_The_Clock()
    {
        var f = StepFights.Rime();

        f.Wait(2.0);

        AssertSeconds(2.0, f.Seconds());
    }

    /// <summary>An instant cast returns when its global cooldown ends.</summary>
    [Fact]
    public void An_Instant_Cast_Returns_When_The_Global_Cooldown_Ends()
    {
        var f = StepFights.Rime();

        f.Cast<ColdSnap>();

        using var check = new Checks(f);
        check.Seconds(1.5, f.Seconds());
        check.True(f.IsFree);
    }

    /// <summary>A cast as long as the global cooldown returns when both end: Frost Bolt is a 1.5 second cast inside a 1.5 second global cooldown.</summary>
    [Fact]
    public void A_Cast_As_Long_As_The_Global_Cooldown_Returns_When_Both_End()
    {
        var f = StepFights.Rime();

        f.Cast<FrostBolt>();

        using var check = new Checks(f);
        check.Seconds(1.5, f.Seconds());
        check.True(f.IsFree);
    }

    /// <summary>A cast longer than the global cooldown returns when the cast ends: Bursting Ice is 2 seconds, the global cooldown 1.5.</summary>
    [Fact]
    public void A_Cast_Longer_Than_The_Global_Cooldown_Returns_When_The_Cast_Ends()
    {
        var f = StepFights.Rime();

        f.Cast<BurstingIce>();

        using var check = new Checks(f);
        check.Seconds(2.0, f.Seconds());
        check.True(f.IsFree);
    }

    /// <summary>A cast the engine refuses throws, names the ability, and does not move the clock.</summary>
    [Fact]
    public void A_Refused_Cast_Throws_And_Does_Not_Move_The_Clock()
    {
        var f = StepFights.Rime();

        var refusal = Assert.Throws<InvalidOperationException>(() => f.Cast<GlacialBlast>()); // no Winter Orbs

        using var check = new Checks(f);
        check.True(refusal.Message.Contains("Glacial Blast"), "the message names the ability");
        check.Equal(0.0, f.Seconds());
    }

    /// <summary>TryCast returns the engine's reason for a refusal instead of throwing.</summary>
    [Fact]
    public void TryCast_Returns_The_Reason_For_A_Refusal()
    {
        var f = StepFights.Rime();

        var attempt = f.TryCast<GlacialBlast>(); // no Winter Orbs

        using var check = new Checks(f);
        check.False(attempt.Succeeded);
        check.False(string.IsNullOrEmpty(attempt.Reason));
    }

    /// <summary>The events record a cast's start before its completion, at the cast's times.</summary>
    [Fact]
    public void Events_Record_A_Cast_Start_Then_Its_Completion()
    {
        var f = StepFights.Rime();

        f.Cast<BurstingIce>();

        var casts = f.Events.Where(e => e.Kind is StepEventKind.CastStarted or StepEventKind.CastCompleted).ToList();

        using var check = new Checks(f);
        check.Equal(2, casts.Count);
        check.True(casts[0].Kind == StepEventKind.CastStarted, "the start comes first");
        check.Seconds(0.0, casts[0].Time.TotalSeconds);
        check.Seconds(2.0, casts[1].Time.TotalSeconds);
    }

    /// <summary>ResetCooldown makes an ability on cooldown ready at once, every charge included.</summary>
    [Fact]
    public void ResetCooldown_Makes_The_Ability_Ready_At_Once()
    {
        var f = StepFights.Rime();

        f.Cast<BurstingIce>();
        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>();

        f.ResetCooldown<BurstingIce>();
        f.ResetCooldown<ColdSnap>();

        using var check = new Checks(f);
        check.True(f.IsReady<BurstingIce>());
        check.Equal(2, f.Charges<ColdSnap>());
    }

    /// <summary>StartCast returns as the cast starts, without moving the clock, so another cast can be tried during it; Wait lets the
    /// cast go on to its end.</summary>
    [Fact]
    public void StartCast_Returns_As_The_Cast_Starts_And_Wait_Lets_It_Complete()
    {
        var f = StepFights.Rime();

        var attempt = f.StartCast<BurstingIce>(); // a 2 second cast

        using var check = new Checks(f);
        check.True(attempt.Succeeded);
        check.Equal(0.0, f.Seconds());
        check.False(f.IsFree);
        check.Equal(0, f.CastsCompleted("Bursting Ice").Count);
        f.Wait(1.0);
        check.False(f.TryCast<FrostBolt>().Succeeded, "a cast tried during it is refused");
        f.Wait(1.1);
        check.Equal(1, f.CastsCompleted("Bursting Ice").Count);
    }

    /// <summary>Remove takes an aura off at once.</summary>
    [Fact]
    public void Remove_Takes_An_Aura_Off()
    {
        var f = StepFights.Rime();
        f.Apply(IceBlitz.Buff);

        f.Remove(IceBlitz.Buff);

        Assert.False(f.Has(IceBlitz.Buff));
    }

    /// <summary>RecoveryRate is 1 for a cooldown nothing speeds up: it recovers in real time.</summary>
    [Fact]
    public void RecoveryRate_Is_1_With_Nothing_Speeding_The_Cooldown_Up()
    {
        var f = StepFights.Rime();

        f.Cast<BurstingIce>();

        Assert.Equal(1.0, f.RecoveryRate<BurstingIce>());
    }

    // ---- Checks

    /// <summary>Checks reports every failed check when it is disposed, each with its line, its expression and the fight's time.</summary>
    [Fact]
    public void Checks_Reports_Every_Failed_Check_With_Its_Expression_And_Time()
    {
        var f = StepFights.Rime();
        var check = new Checks(f);

        check.True(f.Seconds() > 1);
        check.Equal(3, f.Seconds());
        check.Equal(0, f.Seconds()); // passes

        var failure = Assert.Throws<XunitException>(check.Dispose);
        Assert.Contains("2 check(s) failed", failure.Message);
        Assert.Contains("f.Seconds() > 1 is false", failure.Message);
        Assert.Contains("f.Seconds() is 0, expected 3", failure.Message);
        Assert.Contains("at 0.000s", failure.Message);
    }

    /// <summary>Checks with no failed check does not throw.</summary>
    [Fact]
    public void Checks_With_No_Failure_Does_Not_Throw()
    {
        var check = new Checks();

        check.True(true);
        check.Ratio(1.2, 1.2004);

        check.Dispose();
    }

    // ---- Forced outcomes

    /// <summary>
    /// <c>crit: true</c> makes every hit of that cast crit, even at 0% crit, and the next cast of the ability is no longer forced.
    /// <c>crit: false</c> makes none crit, even at 100%.
    /// </summary>
    [Fact]
    public void A_Forced_Crit_Applies_To_That_Cast_Only()
    {
        var noCritScenario = StepFights.Rime(); // 0% crit

        noCritScenario.Cast<FrostBolt>(crit: true);
        noCritScenario.Cast<FrostBolt>();

        var alwaysCritScenario = StepFights.Rime(crit: 1.0);

        alwaysCritScenario.Cast<FrostBolt>(crit: false);

        using var check = new Checks();
        check.True(noCritScenario.Damage<FrostBolt>()[0].IsCrit, "the forced bolt crits");
        check.False(noCritScenario.Damage<FrostBolt>()[1].IsCrit, "the next bolt is not forced");
        check.False(alwaysCritScenario.Damage<FrostBolt>().Single().IsCrit, "the bolt forced not to crit");
    }

    /// <summary>Without the damage spread, the same hit deals the same damage every time; with it, it varies.</summary>
    [Fact]
    public void Without_Damage_Spread_The_Same_Hit_Deals_The_Same_Damage()
    {
        var spreadScenario = StepFights.Rime();
        for (int i = 0; i < 5; i++) spreadScenario.Cast<FrostBolt>();

        var noSpreadScenario = StepFights.Rime().WithoutDamageSpread();
        for (int i = 0; i < 5; i++) noSpreadScenario.Cast<FrostBolt>();

        using var check = new Checks();
        check.True(spreadScenario.Damage<FrostBolt>().Select(hit => hit.Value).Distinct().Count() > 1, "the spread varies the damage");
        check.Equal(1, noSpreadScenario.Damage<FrostBolt>().Select(hit => hit.Value).Distinct().Count(), what: "distinct damage values");
    }

    /// <summary>A forced proc happens or not as forced, and its chance is recorded either way.</summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void A_Forced_Proc_Happens_As_Forced_And_Its_Chance_Is_Recorded(bool forced, int stacks)
    {
        var f = StepFights.Rime(talents: [FrostweaversWrath.TalentId]);
        var procs = f.ForceProcs(key => key == FrostweaversWrath.ProcKey ? forced : null);

        f.Cast<ColdSnap>(); // gives an orb, which rolls the proc

        using var check = new Checks(f);
        check.Equal(stacks, f.Stacks(FrostweaversWrath.Buff));
        check.Equal(1, procs.ChancesOf(FrostweaversWrath.ProcKey).Count, what: "rolls recorded");
    }
}
