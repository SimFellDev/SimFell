using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Frostweaver's Wrath with Icy Talons: a Talon cast uses a stack, and its first hit alone gets the +100% crit.</summary>
public class FrostweaversWrathTests
{
    /// <summary>Each hit's damage (its expected damage, crit included) of one cast of <typeparamref name="T"/> with 5 Winter Orbs, with or without a stack.</summary>
    private static (List<double> Hits, int StacksLeft) Cast<T>(bool withStack) where T : Ability
    {
        var hero = (Rime)HeroImporter.Import(new CharacterBuild
        {
            Hero = "Rime",
            PrimaryStat = 1000,
            CritRating = 400,
            Talents = [IcyTalons.TalentId, FrostweaversWrath.TalentId],
        });
        var ctx = new SimulationContext(hero);
        var target = ctx.AddEnemy(101, 1, "Training Mob 1", maxHealth: 100_000_000);
        hero.CurrentTarget = target;
        hero.Resources.Get(Rime.WinterOrbsResource).Current = Rime.MaxWinterOrbs;
        if (withStack) hero.Auras.Apply(FrostweaversWrath.Buff, ctx.CurrentTime);
        var ability = hero.GetAbility<T>()!;
        var chances = new List<double>();
        hero.OnDamageDealt += (in dmg) => { if (dmg.AbilityId == ability.DamageLogId) chances.Add(dmg.Amount); };
        ability.ResolveCast(ctx, target);
        ctx.RunUntil(SimTime.FromSeconds(5));
        return (chances, hero.Auras.HasAura(FrostweaversWrath.Buff, ctx.CurrentTime) ? 1 : 0);
    }

    [Fact]
    public void A_Talon_Strike_Crits_Its_First_Spike_Alone()
    {
        var (without, _) = Cast<TalonStrike>(withStack: false);
        var (with, stacksLeft) = Cast<TalonStrike>(withStack: true);
        Assert.Equal(Rime.MaxWinterOrbs, with.Count);
        Assert.Equal(0, stacksLeft);
        // The first spike gets the +100% (its chance is capped at 1 when expected outcomes are dealt); the others as without.
        Assert.True(with[0] > without[0]);
        Assert.Equal(without.Skip(1), with.Skip(1));
    }

    [Fact]
    public void Rising_Talons_Crits_Its_First_Wave_Alone()
    {
        var (without, _) = Cast<RisingTalons>(withStack: false);
        var (with, stacksLeft) = Cast<RisingTalons>(withStack: true);
        Assert.Equal(Rime.MaxWinterOrbs, with.Count);
        Assert.Equal(0, stacksLeft);
        Assert.True(with[0] > without[0]);
        Assert.Equal(without.Skip(1), with.Skip(1));
    }
}
