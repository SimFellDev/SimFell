using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Visions of Grandeur (aspect 47): "Your Weapon Ability's Cooldown is reduced by 25% / 50% / 75% / 100% when you activate your Spirit Ability" (of its
/// default cooldown), and "Your Weapon Ability generates 2.5 Spirit Points multiplied by a value equal to your Weapon Ability's default cooldown, divided
/// by 30" (combat logs: 15 Spirit Points per Chronoshift, 180s).
/// Dev: one generic test per rule, run over the six heroes and every weapon ability each is authorized to have (<see cref="GearHero.WeaponAbilities"/>; default
/// cooldowns: Fated Strike 60s, Sahril's Wrath 120s, Voidbringer's Touch 90s, Chronoshift 180s, Icicles of An'zhyr 40s, Nature's Fury 60s; the engine has no Earthbreaker yet).
/// Each compares a fight with the aspect to the same fight without it, so what the weapon and the hero do on their own cancels out.
/// </summary>
public class AspectVisionsOfGrandeur
{
    private static readonly double[] CooldownShare = [0.25, 0.5, 0.75, 1.0];

    public static IEnumerable<object[]> HeroWeapons => AspectFights.HeroWeapons;
    public static IEnumerable<object[]> HeroWeaponRanks => AspectFights.HeroWeaponRanks;
    public static IEnumerable<object[]> Heroes => GearHero.All;

    private static StepDriver Fight(AspectHeroWeapon hw, bool aspect, int rank = 1) =>
        hw.Hero.Fight(AspectFights.With(aspect ? AspectFights.VisionsOfGrandeur : 0, rank, weapon: hw.WeaponId));

    private static double SpiritPoints(StepDriver f) => f.Hero.ResourceAmount(Hero.SpiritPoints);

    // ---- Spirit Points

    /// <summary>
    /// Player: casting the weapon ability gives 2.5 Spirit Points per 30 seconds of its default cooldown: 5 for Fated Strike, 15 for Chronoshift.
    /// Dev: Spirit Points right after the weapon cast, with the aspect minus without (passive regeneration and the like cancel).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void The_Weapon_Ability_Gives_Spirit_Points_By_Its_Cooldown(AspectHeroWeapon hw)
    {
        var with = Fight(hw, true);
        var without = Fight(hw, false);
        with.Cast(with.AspectWeapon());
        without.Cast(without.AspectWeapon());
        Assert.Equal(2.5 * hw.WeaponCooldown / 30.0, SpiritPoints(with) - SpiritPoints(without), 1e-6);
    }

    /// <summary>
    /// Player: only the weapon ability gives them: the hero's other abilities give no extra Spirit Points.
    /// Dev: the hero's five other abilities cast in both fights; Spirit Points are equal throughout.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void Other_Abilities_Give_No_Extra_Spirit_Points(AspectHeroWeapon hw)
    {
        var with = Fight(hw, true);
        var without = Fight(hw, false);
        var hero = hw.Hero;
        foreach (var type in new[] { hero.Basic, hero.Core, hero.Power, hero.Major })
        {
            hero.CastProbe(with, type);
            hero.CastProbe(without, type);
            Assert.Equal(SpiritPoints(without), SpiritPoints(with), 1e-6);
        }
    }

    // ---- Cooldown

    /// <summary>
    /// Player: casting the Spirit ability takes the rank's share (25% / 50% / 75% / 100%) of the weapon ability's default cooldown off its running
    /// cooldown (never more than is left).
    /// Dev: the weapon cast, then the Spirit ability (full resources); the cooldown left at the end with the aspect is the one without minus the share
    /// of the default cooldown, but not below 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeaponRanks))]
    public void The_Spirit_Ability_Cuts_The_Weapon_Cooldown_By_The_Rank_Share(AspectHeroWeapon hw, int rank)
    {
        double Left(bool aspect)
        {
            var f = Fight(hw, aspect, rank);
            f.Cast(f.AspectWeapon());
            hw.Hero.CastProbe(f, hw.Hero.Spirit);
            return f.AspectWeaponCooldownLeft();
        }
        double without = Left(false);
        Assert.Equal(Math.Max(0.0, without - CooldownShare[rank - 1] * hw.WeaponCooldown), Left(true), 1e-6);
    }

    /// <summary>
    /// Player: only the Spirit ability does it.
    /// Dev: the weapon cast, then the hero's four other abilities (full resources); the weapon's cooldown left equals the one without the aspect.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void Other_Abilities_Do_Not_Cut_The_Weapon_Cooldown(AspectHeroWeapon hw)
    {
        double Left(bool aspect)
        {
            var f = Fight(hw, aspect, 4);
            f.Cast(f.AspectWeapon());
            foreach (var type in new[] { hw.Hero.Basic, hw.Hero.Core, hw.Hero.Power, hw.Hero.Major }) hw.Hero.CastProbe(f, type);
            return f.AspectWeaponCooldownLeft();
        }
        Assert.Equal(Left(false), Left(true), 1e-6);
    }

    /// <summary>
    /// Player: with the weapon ability on cooldown for less than the share, the Spirit ability brings it back at once (rank 4 takes the whole default
    /// cooldown: the weapon is ready again whatever is left).
    /// Dev: rank 4; the weapon cast and the Spirit ability; the weapon is ready.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void At_Rank_Four_The_Spirit_Ability_Makes_The_Weapon_Ready(AspectHeroWeapon hw)
    {
        var f = Fight(hw, true, 4);
        f.Cast(f.AspectWeapon());
        hw.Hero.CastProbe(f, hw.Hero.Spirit);
        Assert.Equal(0.0, f.AspectWeaponCooldownLeft(), 1e-9);
    }

    // ---- Without a weapon

    /// <summary>
    /// Player: without a weapon ability the aspect has nothing to work on and the hero fights as usual.
    /// Dev: no weapon; the Spirit ability casts (Spirit Points spent as without the aspect).
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Without_A_Weapon_It_Does_Nothing(GearHero hero)
    {
        var with = hero.Fight(AspectFights.With(AspectFights.VisionsOfGrandeur, 4));
        var without = hero.Fight(AspectFights.With(0));
        hero.CastProbe(with, hero.Spirit);
        hero.CastProbe(without, hero.Spirit);
        Assert.Equal(SpiritPoints(without), SpiritPoints(with), 1e-6);
    }
}
