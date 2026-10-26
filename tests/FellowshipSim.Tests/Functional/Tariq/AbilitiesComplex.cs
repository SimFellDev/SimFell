using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Tariq's abilities together: Thunder Call's lightning with several enemies and in what order it lands, Focused Wrath over a whole Hammer Storm,
/// Spirit Refund and Rising Spirit, Raging Tempest taking over from Thunder Call.
/// Dev: damage is the expected value, so ratios are exact; a Wild Swing hit (129.14% of Strength) is the unit where an absolute number is needed.
/// </summary>
public class TariqAbilitiesComplex
{
    private const double Ratio = 0.001;

    private static readonly double Strength = TariqFights.StrengthUnit();

    // ---- Thunder Call

    /// <summary>
    /// Player: with Thunder Call, a Heavy Strike hits in this order: the target, then the other enemies with the cleave, then the lightning on all of them.
    /// Dev: 3 enemies; Thunder Call; one Heavy Strike; the damage events in order: the target, 2 cleave hits, 3 lightning hits.
    /// </summary>
    [Fact]
    public void Heavy_Strike_Hits_The_Target_Then_The_Others_Then_The_Lightning()
    {
        var f = StepFights.Tariq(enemies: 3);
        f.Cast<ThunderCall>();

        f.Cast<HeavyStrike>();

        var ids = f.Events.Where(e => e.Kind == StepEventKind.Damage).Select(e => e.AbilityId).ToList();
        Assert.Equal([LogIds.Effect(1776), LogIds.Effect(2755), LogIds.Effect(2755), LogIds.Effect(1779), LogIds.Effect(1779), LogIds.Effect(1779)], ids);
    }

    /// <summary>
    /// Player: with Thunder Call, every spin of Hammer Storm hits every enemy with the physical damage first, then every enemy with the lightning.
    /// Dev: 2 enemies; Thunder Call; one Hammer Storm; the first four damage events: physical, physical, lightning, lightning.
    /// </summary>
    [Fact]
    public void Hammer_Storm_Spin_Hits_Every_Enemy_With_Physical_Then_Every_Enemy_With_Lightning()
    {
        var f = StepFights.Tariq(enemies: 2);
        f.SetFury(100);
        f.Cast<ThunderCall>();

        f.Cast<HammerStorm>();

        var names = f.Events.Where(e => e.Kind == StepEventKind.Damage).Take(4).Select(e => e.Name).ToList();
        Assert.Equal(["Hammer Storm", "Hammer Storm", "Thunder Call: Hammer Storm", "Thunder Call: Hammer Storm"], names);
    }

    /// <summary>
    /// Player: beyond 15 enemies the lightning of Hammer Storm falls off (sqrt(15 / enemies)), while its physical hit already did beyond 12.
    /// Dev: 16 enemies; Thunder Call; one Hammer Storm; the first spin's physical hit is sqrt(12 / 16) of a 12-enemy one's, and its lightning
    /// hit is sqrt(15 / 16) of a 15-enemy one's.
    /// </summary>
    [Fact]
    public void Hammer_Storm_Lightning_Falls_Off_Beyond_15_Enemies()
    {
        (double Physical, double Lightning) Spin(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies);
            f.SetFury(100);
            f.Cast<ThunderCall>();
            f.Cast<HammerStorm>();
            return (f.Damage("Hammer Storm")[0].Value, f.Damage("Thunder Call: Hammer Storm")[0].Value);
        }

        var sixteen = Spin(16);
        Assert.Equal(Math.Sqrt(12.0 / 16), sixteen.Physical / Spin(12).Physical, Ratio);
        Assert.Equal(Math.Sqrt(15.0 / 16), sixteen.Lightning / Spin(15).Lightning, Ratio);
    }

    /// <summary>
    /// Player: the lightning of a Thunder Call Skull Crusher and of Heavy Strike does not need the physical hit to land first: they follow the target.
    /// Dev: 2 enemies; Thunder Call; one Skull Crusher on the second enemy; the bolt hits that enemy and not the first.
    /// </summary>
    [Fact]
    public void Skull_Crusher_Thunder_Bolt_Strikes_The_Cast_Target()
    {
        var f = StepFights.Tariq(enemies: 2);
        f.SetFury(100);
        var second = f.Context.AllEnemies[1];
        f.Cast<ThunderCall>();

        f.Cast<SkullCrusher>(second);

        Assert.Equal(second.Name, f.Damage("Thunder Call: Skull Crusher").Single().Target);
        Assert.Equal(second.Name, f.Damage("Skull Crusher").Single().Target);
    }

    // ---- Focused Wrath over a Hammer Storm

    /// <summary>
    /// Player: Focused Wrath on a Hammer Storm uses one stack for the whole cast: all 3 spins and their lightning deal 10% more.
    /// Dev: 2 stacks; Thunder Call; one Hammer Storm: 1 stack left; all 3 spins and 3 lightning hits are 1.1 times those of a Hammer Storm without it.
    /// </summary>
    [Fact]
    public void Focused_Wrath_Boosts_Every_Spin_Of_One_Hammer_Storm_And_Uses_One_Stack()
    {
        (List<StepEvent> Spins, List<StepEvent> Lightning) Storm(bool wrath)
        {
            var g = StepFights.Tariq();
            g.SetFury(100);
            g.Cast<ThunderCall>();
            if (wrath) g.Cast<FocusedWrath>();
            g.Cast<HammerStorm>();
            g.Wait(1.5);
            if (wrath) Assert.Equal(1, g.Stacks(FocusedWrath.Buff));
            return (g.Damage("Hammer Storm"), g.Damage("Thunder Call: Hammer Storm"));
        }

        var plain = Storm(false);
        var boosted = Storm(true);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(1.1, boosted.Spins[i].Value / plain.Spins[i].Value, Ratio);
            Assert.Equal(1.1, boosted.Lightning[i].Value / plain.Lightning[i].Value, Ratio);
        }
    }

    /// <summary>
    /// Player: Focused Wrath boosts the thunder bolt of a Skull Crusher as well as the physical hit.
    /// Dev: Thunder Call and Focused Wrath; one Skull Crusher: the bolt is 4.06 / 11.6 of the physical hit, both 1.1 times what they are without.
    /// </summary>
    [Fact]
    public void Focused_Wrath_Boosts_The_Skull_Crusher_Thunder_Bolt_Too()
    {
        StepEvent Bolt(bool wrath)
        {
            var g = StepFights.Tariq();
            g.SetFury(100);
            g.Cast<ThunderCall>();
            if (wrath) g.Cast<FocusedWrath>();
            g.Cast<SkullCrusher>();
            return g.Damage("Thunder Call: Skull Crusher").Single();
        }

        Assert.Equal(1.1, Bolt(true).Value / Bolt(false).Value, Ratio);
    }

    // ---- Spirit Refund and Rising Spirit

    /// <summary>
    /// Player: each Spirit Refund gives a stack of Rising Spirit: +5% of your main stat each, so your hits deal 5% more per stack.
    /// Dev: Spirit Refund forced on; 100 Fury; two Skull Crushers: 2 stacks; a Wild Swing after them is 1.10 times a Wild Swing without.
    /// </summary>
    [Fact]
    public void Rising_Spirit_Adds_5_Percent_Damage_Per_Stack()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SetFury(100);

        f.Cast<SkullCrusher>();
        f.Cast<SkullCrusher>();
        Assert.Equal(2, f.Stacks(Hero.RisingSpirit));
        f.Cast<WildSwing>();

        Assert.Equal(1.2914 * 1.10, f.FirstDamageOf("Wild Swing") / Strength, 1.2914 * 1.10 * Ratio);
    }

    /// <summary>
    /// Player: a Spirit Refund after a discounted Skull Crusher gives back the Fury actually spent.
    /// Dev: Focused Wrath; 100 Fury; Spirit Refund forced on; one Skull Crusher: 12 spent and 100 - 12 + 12 = 100 afterwards.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Back_The_Discounted_Fury()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SetFury(100);
        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();

        Assert.Equal([12.0], f.FurySpent("Fury"));
        Assert.Equal(100, f.Fury(), 1e-9);
    }

    /// <summary>
    /// Player: a Culling Strike's Spirit Refund gives back the Fury it spent, which was part of its damage.
    /// Dev: an enemy at 20% health; 10 Fury; Spirit Refund forced on; one Culling Strike: 10 spent, 10 back; its hit was 5 times a no-Fury one.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Back_The_Fury_A_Culling_Strike_Spent()
    {
        var f = StepFights.Tariq(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SetHealth(0.2);
        f.SetFury(10);

        f.Cast<CullingStrike>();
        f.Wait(1);

        Assert.Equal([10.0], f.FurySpent("Fury"));
        Assert.Equal(10, f.Fury(), 1e-9);
    }

    // ---- Raging Tempest

    /// <summary>
    /// Player: during Raging Tempest your abilities have lightning as with Thunder Call, and so does Heavy Strike's.
    /// Dev: 2 enemies; Raging Tempest; one Heavy Strike: 2 lightning hits.
    /// </summary>
    [Fact]
    public void Heavy_Strike_Has_Lightning_During_Raging_Tempest()
    {
        var f = StepFights.Tariq(enemies: 2);
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();

        f.Cast<HeavyStrike>();

        Assert.Equal(2, f.Damage("Thunder Call: Heavy Strike").Count);
    }

    /// <summary>
    /// Player: Raging Currents strikes a random nearby enemy: with several enemies the strikes land on them, not on one only.
    /// Dev: 4 enemies; Raging Tempest; 21 seconds pass at 2% haste (26 strikes with the 30% of Raging Tempest); every strike is on an enemy in the fight, and
    /// at least 2 different enemies are struck.
    /// </summary>
    [Fact]
    public void Raging_Currents_Strikes_Random_Enemies()
    {
        var f = StepFights.Tariq(0.02, enemies: 4);
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();

        f.Wait(21);

        var targets = f.Damage("Raging Currents").Select(h => h.Target).ToList();
        Assert.Equal(26, targets.Count);
        Assert.True(targets.Distinct().Count() >= 2);
    }
}
