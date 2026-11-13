using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Tariq's Fury (0 to 100), on a bare Tariq. Wild Swing, Face Breaker, Heavy Strike and Chain Lightning build it; Skull Crusher, Hammer
/// Storm and Culling Strike spend it; Focused Wrath makes the first two cheaper. Talents that give Fury (Bloodline, Ride the Lightning,
/// High Road) are in TariqTalents.cs, and what a Spirit Refund gives every hero is in Generic/SpiritRefund.cs.
///
/// Fury is set directly for the spenders, and the amounts gained and spent are read off the fight's events.
/// </summary>
public class TariqResourceEconomy
{
    /// <summary>You start a fight with no Fury.</summary>
    [Fact]
    public void A_Fight_Starts_With_No_Fury()
    {
        var f = StepFights.Tariq();

        Assert.Equal(0, f.Amount(Tariq.Fury));
    }

    /// <summary>
    /// Fury does not drain during a fight: 50 Fury are still 50 after 30 seconds with nothing cast.
    ///
    /// TODO: Bloodline's tooltip says "You no longer lose Fury while out of combat", so Fury drains out of combat without it. The engine
    /// has no drain at all (the logs showed none between pulls). Check in game.
    /// </summary>
    [Fact]
    public void Fury_Does_Not_Drain_During_A_Fight()
    {
        var f = StepFights.Tariq();
        f.Set(Tariq.Fury, 50);

        f.Wait(30);

        Assert.Equal(50, f.Amount(Tariq.Fury));
    }

    // ---- Generators

    /// <summary>Wild Swing generates 3 Fury, Face Breaker 7 and Heavy Strike 12: one gain of that amount per cast, from 0 Fury.</summary>
    [Theory]
    [InlineData("Wild Swing", 3)]
    [InlineData("Face Breaker", 7)]
    [InlineData("Heavy Strike", 12)]
    public void Generators_Give_Their_Fury(string ability, double fury)
    {
        var f = StepFights.Tariq();

        f.Cast(ability);

        using var check = new Checks(f);
        check.True(f.Gains("Fury").Select(gain => gain.Value).SequenceEqual([fury]), "one gain of the generator's Fury");
        check.Equal(fury, f.Amount(Tariq.Fury));
    }

    /// <summary>A generator gives its Fury once per cast, however many enemies it hits: Wild Swing, Face Breaker and Heavy Strike on 5
    /// enemies give one gain each.</summary>
    [Fact]
    public void Generators_Give_Fury_Once_Per_Cast_Whatever_The_Enemy_Count()
    {
        var f = StepFights.Tariq(enemies: 5);

        f.Cast<WildSwing>();
        f.Cast<FaceBreaker>();
        f.Cast<HeavyStrike>();

        Assert.Equal([3.0, 7.0, 12.0], f.Gains("Fury").Select(gain => gain.Value));
    }

    /// <summary>Chain Lightning generates 1.2 Fury per hit: after Thunder Call, one Chain Lightning on one enemy gives 6 gains of 1.2,
    /// 7.2 in all.</summary>
    [Fact]
    public void Chain_Lightning_Gives_1_2_Fury_Per_Hit()
    {
        var f = StepFights.Tariq();

        f.Cast<ThunderCall>();

        f.Cast<ChainLightning>();

        using var check = new Checks(f);
        check.Equal(6, f.Gains("Fury").Count);
        foreach (var gain in f.Gains("Fury")) check.Equal(1.2, gain.Value, what: "a Chain Lightning hit's Fury");
        check.Equal(7.2, f.Amount(Tariq.Fury));
    }

    /// <summary>Fury stops at 100, and what a generator would give beyond it is lost: at 98 Fury a Wild Swing (3) leaves 100, with an
    /// overflow of 1.</summary>
    [Fact]
    public void Fury_Is_Capped_At_100()
    {
        var f = StepFights.Tariq();
        f.Set(Tariq.Fury, 98);

        f.Cast<WildSwing>();

        using var check = new Checks(f);
        check.Equal(100, f.Amount(Tariq.Fury));
        check.Equal(1.0, f.Overflows("Fury").Sum(overflow => overflow.Value));
    }

    // ---- Spenders

    /// <summary>Skull Crusher costs 25 Fury and Hammer Storm 50: from 100 Fury, one of each spends 25 then 50, leaving 25.</summary>
    [Fact]
    public void Skull_Crusher_Costs_25_Fury_And_Hammer_Storm_50()
    {
        var f = StepFights.Tariq();
        f.Set(Tariq.Fury, 100);

        f.Cast<SkullCrusher>();
        f.Cast<HammerStorm>();

        using var check = new Checks(f);
        check.True(f.Spends("Fury").Select(spend => spend.Value).SequenceEqual([25.0, 50.0]), "spends of 25 then 50");
        check.Equal(25, f.Amount(Tariq.Fury));
    }

    /// <summary>
    /// You cannot cast Skull Crusher without 25 Fury, nor Hammer Storm without 50, and nothing is spent when you are refused: at 24 Fury
    /// Skull Crusher is refused, at 49 Hammer Storm is refused and the 49 are still there. With exactly 25 and 50 they work.
    /// </summary>
    [Fact]
    public void Spenders_Need_Their_Fury()
    {
        var f = StepFights.Tariq();

        using var check = new Checks(f);
        f.Set(Tariq.Fury, 24);
        check.False(f.TryCast<SkullCrusher>().Succeeded);
        f.Set(Tariq.Fury, 49);
        check.False(f.TryCast<HammerStorm>().Succeeded);
        check.Equal(49, f.Amount(Tariq.Fury));

        f.Set(Tariq.Fury, 25);
        check.True(f.TryCast<SkullCrusher>().Succeeded);
        f.Set(Tariq.Fury, 50);
        check.True(f.TryCast<HammerStorm>().Succeeded);
    }

    /// <summary>Culling Strike spends up to 10 Fury, and works with less or none: on an enemy at 20% health, Culling Strikes at 100, 6 and
    /// 0 Fury spend 10, then 6, then nothing.</summary>
    [Fact]
    public void Culling_Strike_Spends_Up_To_10_Fury()
    {
        var f = StepFights.Tariq();
        f.SetHealth(0.2); // in execute range

        f.Set(Tariq.Fury, 100);
        f.Cast<CullingStrike>();
        f.Wait(6);
        f.Set(Tariq.Fury, 6);
        f.Cast<CullingStrike>();
        f.Wait(6);
        f.Set(Tariq.Fury, 0);
        f.Cast<CullingStrike>();

        using var check = new Checks(f);
        check.True(f.Spends("Fury").Select(spend => spend.Value).SequenceEqual([10.0, 6.0]), "spends of 10 then 6, and none for the last");
        check.Equal(0, f.Amount(Tariq.Fury));
    }

    // ---- Focused Wrath

    /// <summary>Focused Wrath makes your next two Skull Crusher or Hammer Storm casts cost 52% less Fury: a Skull Crusher then a Hammer
    /// Storm spend 12 and 24, and a third cast, a Skull Crusher, costs 25 again.</summary>
    [Fact]
    public void Focused_Wrath_Makes_The_Next_Two_Spenders_52_Percent_Cheaper()
    {
        var f = StepFights.Tariq();
        f.Set(Tariq.Fury, 100);

        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();
        f.Cast<HammerStorm>();
        f.Cast<SkullCrusher>();

        Assert.Equal([12.0, 24.0, 25.0], f.Spends("Fury").Select(spend => spend.Value));
    }

    /// <summary>With Focused Wrath you can cast Skull Crusher with only its reduced cost: at 12 Fury it works and spends 12.</summary>
    [Fact]
    public void Focused_Wrath_Lets_You_Cast_With_The_Reduced_Cost_Alone()
    {
        var f = StepFights.Tariq();

        f.Cast<FocusedWrath>();
        f.Set(Tariq.Fury, 12); // after the cast

        f.Cast<SkullCrusher>(); // throws if refused

        Assert.Equal([12.0], f.Spends("Fury").Select(spend => spend.Value));
    }

    /// <summary>Focused Wrath lasts 15 seconds from the cast: its stacks are up before they end and gone after.</summary>
    [Fact]
    public void Focused_Wrath_Lasts_15_Seconds()
    {
        var f = StepFights.Tariq();

        f.Cast<FocusedWrath>();
        double end = f.CompletedAt("Focused Wrath") + 15;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Stacks(FocusedWrath.Buff) > 0);
        f.WaitUntil(end + 0.1);
        Assert.Equal(0, f.Stacks(FocusedWrath.Buff));
    }

    // ---- Spirit Refund

    /// <summary>A Spirit Refund is rolled each time you spend Fury, at Spirit / (1 + Spirit): at 20% Spirit, a Skull Crusher and a Hammer
    /// Storm roll twice at 0.2 / 1.2.</summary>
    [Fact]
    public void Spirit_Refund_Is_Rolled_On_Each_Spend_With_Spirit_Over_1_Plus_Spirit()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // only to read the rolls
        f.Set(Tariq.Fury, 100);

        f.Cast<SkullCrusher>();
        f.Cast<HammerStorm>();

        var rolls = procs.ChancesOf(Hero.SpiritRefundProc);
        using var check = new Checks(f);
        check.Equal(2, rolls.Count);
        foreach (double chance in rolls) check.Equal(0.2 / 1.2, chance, what: "a Spirit Refund roll's chance");
    }

    /// <summary>A Spirit Refund is not rolled when no Fury is spent: not by the builders Wild Swing, Face Breaker and Heavy Strike, and
    /// not by a Culling Strike with no Fury.</summary>
    [Fact]
    public void Spirit_Refund_Is_Not_Rolled_For_Builders_Or_A_Culling_Strike_With_No_Fury()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // only to count the rolls
        f.SetHealth(0.2); // in execute range

        using var check = new Checks(f);
        f.Cast<WildSwing>();
        f.Cast<FaceBreaker>();
        f.Cast<HeavyStrike>();
        check.Equal(0, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls after the builders");

        f.Set(Tariq.Fury, 0);
        f.Cast<CullingStrike>();
        check.Equal(0, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls after Culling Strike");
    }

    /// <summary>A Spirit Refund after a Skull Crusher made cheaper by Focused Wrath gives back the Fury actually spent: at 100 Fury, 12
    /// spent, and 100 - 12 + 12 = 100 afterwards.</summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Back_The_Discounted_Fury()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Set(Tariq.Fury, 100);

        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();

        using var check = new Checks(f);
        check.True(f.Spends("Fury").Select(spend => spend.Value).SequenceEqual([12.0]), "one spend of 12");
        check.Equal(100, f.Amount(Tariq.Fury));
    }

    /// <summary>A Culling Strike's Spirit Refund gives back the Fury it spent, which was part of its damage: on an enemy at 20% health
    /// with 10 Fury, 10 spent and 10 back.</summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Back_The_Fury_A_Culling_Strike_Spent()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SetHealth(0.2); // in execute range
        f.Set(Tariq.Fury, 10);

        f.Cast<CullingStrike>();
        f.Wait(1); // the refund lands

        using var check = new Checks(f);
        check.True(f.Spends("Fury").Select(spend => spend.Value).SequenceEqual([10.0]), "one spend of 10");
        check.Equal(10, f.Amount(Tariq.Fury));
    }
}
