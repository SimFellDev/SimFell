using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// What Tariq gets and spends: Fury (0 to 100). Wild Swing, Face Breaker, Heavy Strike and Chain Lightning build it; Skull Crusher, Hammer Storm and
/// Culling Strike spend it; Focused Wrath makes the first two cheaper. Spirit Refund gives spent Fury back. Talents that give Fury (Bloodline, Ride the
/// Lightning, High Road) are in the talent files.
/// Dev: Fury is set directly on the hero for the spenders; gains and spends are read from the fight's events. Spirit Refund is forced.
/// </summary>
public class TariqResourceEconomy
{
    /// <summary>
    /// Player: you start a fight with no Fury.
    /// Dev: Fury at the first moment of a fight.
    /// </summary>
    [Fact]
    public void A_Fight_Starts_With_No_Fury()
    {
        var f = StepFights.Tariq();

        Assert.Equal(0, f.Fury());
    }

    /// <summary>
    /// Player: Fury does not drain during a fight.
    /// Dev: 50 Fury given; 30 seconds pass with nothing cast; still 50.
    /// TODO: the tooltip of Bloodline says "You no longer lose Fury while out of combat", so Fury drains out of combat without it. The engine has no
    /// drain at all (the logs showed none between pulls). Check in game.
    /// </summary>
    [Fact]
    public void Fury_Does_Not_Drain_During_A_Fight()
    {
        var f = StepFights.Tariq();
        f.SetFury(50);

        f.Wait(30);

        Assert.Equal(50, f.Fury());
    }

    // ---- Generators

    /// <summary>
    /// Player: Wild Swing generates 3 Fury, Face Breaker 7 and Heavy Strike 12.
    /// Dev: one cast of each from 0 Fury; one gain of that amount.
    /// </summary>
    [Theory]
    [InlineData("Wild Swing", 3)]
    [InlineData("Face Breaker", 7)]
    [InlineData("Heavy Strike", 12)]
    public void Generators_Give_Their_Fury(string ability, double fury)
    {
        var f = StepFights.Tariq();

        f.Cast(f.Hero.Abilities.First(a => a.Name == ability));

        Assert.Equal([fury], f.FuryGained("Fury"));
        Assert.Equal(fury, f.Fury());
    }

    /// <summary>
    /// Player: a generator gives its Fury once per cast, however many enemies it hits.
    /// Dev: Wild Swing, Face Breaker and Heavy Strike on 5 enemies; one gain each.
    /// </summary>
    [Fact]
    public void Generators_Give_Fury_Once_Per_Cast_Whatever_The_Enemy_Count()
    {
        var f = StepFights.Tariq(enemies: 5);

        f.Cast<WildSwing>();
        f.Cast<FaceBreaker>();
        f.Cast<HeavyStrike>();

        Assert.Equal([3.0, 7.0, 12.0], f.FuryGained("Fury"));
    }

    /// <summary>
    /// Player: Chain Lightning generates 1.2 Fury per hit: 7.2 for its 6 hits.
    /// Dev: Thunder Call, then one Chain Lightning on one enemy; 6 gains of 1.2.
    /// </summary>
    [Fact]
    public void Chain_Lightning_Gives_1_2_Fury_Per_Hit()
    {
        var f = StepFights.Tariq();
        f.Cast<ThunderCall>();

        f.Cast<ChainLightning>();

        Assert.Equal(6, f.FuryGained("Fury").Count);
        Assert.All(f.FuryGained("Fury"), g => Assert.Equal(1.2, g, 1e-9));
        Assert.Equal(7.2, f.Fury(), 1e-9);
    }

    /// <summary>
    /// Player: Fury stops at 100: what a generator would give beyond it is lost.
    /// Dev: 98 Fury; a Wild Swing (3); Fury reads 100, and one overflow of 1 is reported.
    /// </summary>
    [Fact]
    public void Fury_Is_Capped_At_100()
    {
        var f = StepFights.Tariq();
        f.SetFury(98);

        f.Cast<WildSwing>();

        Assert.Equal(100, f.Fury());
        Assert.Equal(1.0, f.Events.Where(e => e.Kind == StepEventKind.ResourceOverflow && e.Name == "Fury").Sum(e => e.Value), 1e-9);
    }

    // ---- Spenders

    /// <summary>
    /// Player: Skull Crusher costs 25 Fury and Hammer Storm 50.
    /// Dev: 100 Fury; one cast of each; the amounts spent are 25 and 50.
    /// </summary>
    [Fact]
    public void Skull_Crusher_Costs_25_Fury_And_Hammer_Storm_50()
    {
        var f = StepFights.Tariq();
        f.SetFury(100);

        f.Cast<SkullCrusher>();
        f.Cast<HammerStorm>();

        Assert.Equal([25.0, 50.0], f.FurySpent("Fury"));
        Assert.Equal(25, f.Fury());
    }

    /// <summary>
    /// Player: you cannot cast Skull Crusher without 25 Fury, nor Hammer Storm without 50, and nothing is spent when you are refused.
    /// Dev: 24 Fury: Skull Crusher refused; 49 Fury: Hammer Storm refused; Fury unchanged. With exactly 25 and 50 they work.
    /// </summary>
    [Fact]
    public void Spenders_Need_Their_Fury()
    {
        var f = StepFights.Tariq();

        f.SetFury(24);
        Assert.False(f.TryCast<SkullCrusher>().Succeeded);
        f.SetFury(49);
        Assert.False(f.TryCast<HammerStorm>().Succeeded);
        Assert.Equal(49, f.Fury());

        f.SetFury(25);
        Assert.True(f.TryCast<SkullCrusher>().Succeeded);
        f.SetFury(50);
        Assert.True(f.TryCast<HammerStorm>().Succeeded);
    }

    /// <summary>
    /// Player: Culling Strike spends up to 10 Fury, and works with less (or none).
    /// Dev: an enemy at 20% health; Culling Strike at 100, 6 and 0 Fury; the amounts spent are 10 and 6, and nothing for the last.
    /// </summary>
    [Fact]
    public void Culling_Strike_Spends_Up_To_10_Fury()
    {
        var f = StepFights.Tariq();
        f.SetHealth(0.2);

        f.SetFury(100);
        f.Cast<CullingStrike>();
        f.Wait(6);
        f.SetFury(6);
        f.Cast<CullingStrike>();
        f.Wait(6);
        f.SetFury(0);
        f.Cast<CullingStrike>();

        Assert.Equal([10.0, 6.0], f.FurySpent("Fury"));
        Assert.Equal(0, f.Fury());
    }

    // ---- Focused Wrath

    /// <summary>
    /// Player: Focused Wrath makes your next two Skull Crusher or Hammer Storm casts cost 52% less Fury: 12 and 24 Fury.
    /// Dev: Focused Wrath, then 100 Fury; a Skull Crusher, a Hammer Storm; the amounts spent are 12 and 24; a third Skull Crusher costs 25.
    /// </summary>
    [Fact]
    public void Focused_Wrath_Makes_The_Next_Two_Spenders_52_Percent_Cheaper()
    {
        var f = StepFights.Tariq();
        f.SetFury(100);
        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();
        f.Cast<HammerStorm>();
        f.Cast<SkullCrusher>();

        Assert.Equal([12.0, 24.0, 25.0], f.FurySpent("Fury"));
    }

    /// <summary>
    /// Player: with Focused Wrath you can cast Skull Crusher with only the reduced cost: 12 Fury.
    /// Dev: Focused Wrath; 12 Fury; a Skull Crusher works, spending 12 (it throws if refused).
    /// </summary>
    [Fact]
    public void Focused_Wrath_Lets_You_Cast_With_The_Reduced_Cost_Alone()
    {
        var f = StepFights.Tariq();
        f.Cast<FocusedWrath>();
        f.SetFury(12);

        f.Cast<SkullCrusher>();

        Assert.Equal([12.0], f.FurySpent("Fury"));
    }

    /// <summary>
    /// Player: Focused Wrath lasts 15 seconds: after that its stacks are gone and the spenders cost full price.
    /// Dev: Focused Wrath; 16 seconds later with 100 Fury, a Skull Crusher spends 25.
    /// </summary>
    [Fact]
    public void Focused_Wrath_Lasts_15_Seconds()
    {
        var f = StepFights.Tariq();
        f.Cast<FocusedWrath>();
        f.Wait(16);
        f.SetFury(100);

        f.Cast<SkullCrusher>();

        Assert.Equal([25.0], f.FurySpent("Fury"));
    }

    // ---- Spirit Refund

    /// <summary>
    /// Player: a Spirit Refund on a spender gives the Fury you spent back, a moment later, plus a stack of Rising Spirit and a Spirit Point.
    /// Dev: Spirit Refund forced on; 100 Fury; one Skull Crusher: 25 spent; by the end of its global cooldown (the refund comes about 0.2s after the spend) Fury is 100 again, with a stack of Rising
    /// Spirit and 1 Spirit Point.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Gives_The_Spent_Fury_Back_With_Rising_Spirit_And_A_Spirit_Point()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SetFury(100);

        f.Cast<SkullCrusher>();

        Assert.Equal([25.0], f.FurySpent("Fury"));
        Assert.Equal(100, f.Fury());
        Assert.Equal(1, f.Stacks(Hero.RisingSpirit));
        Assert.Equal(1, f.Hero.ResourceAmount(Hero.SpiritPoints));
    }

    /// <summary>
    /// Player: Spirit Refund is rolled each time you spend Fury, with a chance of Spirit / (1 + Spirit).
    /// Dev: 20% Spirit; one Skull Crusher, one Hammer Storm; two rolls of 0.2 / 1.2 (Spirit Refund forced off).
    /// </summary>
    [Fact]
    public void Spirit_Refund_Is_Rolled_On_Each_Spend_With_Spirit_Over_1_Plus_Spirit()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.SetFury(100);

        f.Cast<SkullCrusher>();
        f.Cast<HammerStorm>();

        var rolls = procs.Rolls.Where(r => r.Key == Hero.SpiritRefundProc).ToList();
        Assert.Equal(2, rolls.Count);
        Assert.All(rolls, r => Assert.Equal(0.2 / 1.2, r.Chance, 1e-9));
    }

    /// <summary>
    /// Player: Spirit Refund is not rolled when no Fury is spent: not by builders, and not by a Culling Strike with no Fury.
    /// Dev: Wild Swing, Face Breaker and Heavy Strike; no roll; then an enemy at 20% health and a Culling Strike with Fury set to 0; still no roll.
    /// </summary>
    [Fact]
    public void Spirit_Refund_Is_Not_Rolled_For_Builders_Or_A_Culling_Strike_With_No_Fury()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.SetHealth(0.2);

        f.Cast<WildSwing>();
        f.Cast<FaceBreaker>();
        f.Cast<HeavyStrike>();
        Assert.DoesNotContain(procs.Rolls, r => r.Key == Hero.SpiritRefundProc);

        f.SetFury(0);
        f.Cast<CullingStrike>();
        Assert.DoesNotContain(procs.Rolls, r => r.Key == Hero.SpiritRefundProc);
    }
}
