using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Brave Machinations (aspect 48): "Your Weapon Ability's Critical Strike chance is increased by 9.5% / 17% / 24% / 32%. If your Weapon Ability deals a
/// Critical Strike, its Cooldown is reduced by 15% / 20% / 25% / 30%" (of its default cooldown, once per cast).
/// Dev: over the six heroes and each weapon ability the hero can have (melee: Fated Strike, Sahril's Wrath, Voidbringer's Touch; ranged: Chronoshift,
/// Icicles of An'zhyr, Voidbringer's Touch, Nature's Fury; the engine has no Earthbreaker yet), and the four ranks. Each test compares a fight with
/// the aspect to the same fight without it, so what the weapon does on its own cancels. The cooldown part is not checked against a log (UNVERIFIED in the
/// engine); it is tested by its tooltip. Crit chance 1.0 makes every hit a crit in expected-value mode (a crit chance over 50% is a crit).
/// </summary>
public class AspectBraveMachinations
{
    private static readonly double[] CritChance = [0.095, 0.17, 0.24, 0.32];
    private static readonly double[] Reduction = [0.15, 0.20, 0.25, 0.30];

    public static IEnumerable<object[]> HeroWeapons => AspectFights.HeroWeapons;
    public static IEnumerable<object[]> HeroWeaponRanks => AspectFights.HeroWeaponRanks;
    public static IEnumerable<object[]> HeroWeaponRanksWithoutVoidbringers => AspectFights.HeroWeaponRanksWithoutVoidbringers;
    public static IEnumerable<object[]> Heroes => GearHero.All;

    private static StepDriver Fight(AspectHeroWeapon hw, bool aspect, int rank = 1, double crit = 0.0, int enemies = 1) =>
        hw.Hero.Fight(AspectFights.With(aspect ? AspectFights.BraveMachinations : 0, rank, weapon: hw.WeaponId, crit: crit, enemies: enemies));

    /// <summary>
    /// Player: the weapon ability's crit chance is raised by the rank's amount (9.5% / 17% / 24% / 32%).
    /// Dev: the weapon's first hit's crit chance with the aspect minus without it. Not for Voidbringer's Touch: its eruption already has 100% and the
    /// event's chance is capped there.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeaponRanksWithoutVoidbringers))]
    public void The_Weapon_Gains_Crit_Chance(AspectHeroWeapon hw, int rank)
    {
        var with = Fight(hw, true, rank);
        var without = Fight(hw, false);
        with.AspectPlayWeapon(hw.Hero);
        without.AspectPlayWeapon(hw.Hero);
        Assert.Equal(CritChance[rank - 1], with.AspectWeaponHit().CritChance - without.AspectWeaponHit().CritChance, 1e-9);
    }

    /// <summary>
    /// Player: Voidbringer's Touch erupts at 100% crit chance already, so the aspect adds nothing visible to it.
    /// Dev: the eruption's crit chance is 1.0 with the aspect (rank 4) as without.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Voidbringers_Touch_Is_Already_At_Full_Crit_Chance(GearHero hero)
    {
        var hw = new AspectHeroWeapon(hero, AspectFights.VoidbringersTouch);
        var with = Fight(hw, true, 4);
        with.AspectPlayWeapon(hero);
        Assert.Equal(1.0, with.AspectWeaponHit().CritChance, 1e-9);
    }

    /// <summary>
    /// Player: a crit from the weapon ability takes the rank's share (15% / 20% / 25% / 30%) of its default cooldown off.
    /// Dev: crit chance 1.0; the cooldown left after the cast and its hits (the same moment in both fights) with the aspect is the one without
    /// minus share x default cooldown.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeaponRanks))]
    public void A_Weapon_Crit_Cuts_Its_Cooldown_By_The_Rank_Share_Of_The_Default(AspectHeroWeapon hw, int rank)
    {
        var with = Fight(hw, true, rank, crit: 1.0);
        var without = Fight(hw, false, crit: 1.0);
        with.AspectPlayWeapon(hw.Hero);
        without.AspectPlayWeapon(hw.Hero);
        Assert.Equal(Reduction[rank - 1] * hw.WeaponCooldown, without.AspectWeaponCooldownLeft() - with.AspectWeaponCooldownLeft(), 1e-6);
    }

    /// <summary>
    /// Player: the cooldown comes off once per cast, however many hits crit (several enemies, several waves, bounces).
    /// Dev: as above with three enemies and rank 4: still 30% of the default cooldown.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void The_Cooldown_Comes_Off_Once_Per_Cast(AspectHeroWeapon hw)
    {
        var with = Fight(hw, true, 4, crit: 1.0, enemies: 3);
        var without = Fight(hw, false, crit: 1.0, enemies: 3);
        with.AspectPlayWeapon(hw.Hero);
        without.AspectPlayWeapon(hw.Hero);
        Assert.Equal(0.30 * hw.WeaponCooldown, without.AspectWeaponCooldownLeft() - with.AspectWeaponCooldownLeft(), 1e-6);
    }

    /// <summary>
    /// Player: a weapon cast that does not crit gets no cooldown back.
    /// Dev: no crit chance; rank 4 (crit chance 32%, under the 50% that expected-value mode counts as a crit): the cooldowns are equal. Not for Nature's
    /// Fury (+30% crit chance of its own, so 62% with the aspect) or Voidbringer's Touch (100%).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void No_Crit_No_Cooldown_Reduction(AspectHeroWeapon hw)
    {
        if (hw.WeaponId is AspectFights.NaturesFury or AspectFights.VoidbringersTouch) return;
        var with = Fight(hw, true, 4);
        var without = Fight(hw, false);
        with.AspectPlayWeapon(hw.Hero);
        without.AspectPlayWeapon(hw.Hero);
        Assert.Equal(without.AspectWeaponCooldownLeft(), with.AspectWeaponCooldownLeft(), 1e-6);
    }

    /// <summary>
    /// Player: the aspect only concerns the weapon ability: without a weapon the hero fights as it would without the aspect.
    /// Dev: no weapon; the unit hit's damage with the aspect equals the one without.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Without_A_Weapon_It_Does_Nothing(GearHero hero)
    {
        var with = hero.Fight(AspectFights.With(AspectFights.BraveMachinations, 4, crit: 1.0));
        var without = hero.Fight(AspectFights.With(0, crit: 1.0));
        hero.CastProbe(with, hero.Unit);
        hero.CastProbe(without, hero.Unit);
        Assert.Equal(without.Events.Count(e => e.Kind == StepEventKind.Damage), with.Events.Count(e => e.Kind == StepEventKind.Damage));
        Assert.Equal(without.Events.Where(e => e.Kind == StepEventKind.Damage).Sum(e => e.Value), with.Events.Where(e => e.Kind == StepEventKind.Damage).Sum(e => e.Value), 1e-6);
    }
}
