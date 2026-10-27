using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Martial Initiative (aspect 51): "Your Weapon Ability grants you 10% increased Main Stat and 15% Damage Reduction for a duration equal to 10% / 18% /
/// 25% / 32% of your Weapon Ability's default cooldown."
/// Dev: over the six heroes, each of the weapon abilities they can have, and the four ranks where the rank matters. The buff is read from the
/// hero's Main Stat (1000 in these fights) and from the hit it makes bigger; the Damage Reduction is not tested (the sim takes no damage, there is nothing
/// to read it from). A weapon ability's buff comes with its cast completing.
/// </summary>
public class AspectMartialInitiative
{
    private static readonly double[] DurationShare = [0.10, 0.18, 0.25, 0.32];

    public static IEnumerable<object[]> HeroWeapons => AspectFights.HeroWeapons;
    public static IEnumerable<object[]> HeroWeaponRanks => AspectFights.HeroWeaponRanks;
    public static IEnumerable<object[]> Heroes => GearHero.All;

    private static StepDriver Fight(AspectHeroWeapon hw, bool aspect, int rank = 1) =>
        hw.Hero.Fight(AspectFights.With(aspect ? AspectFights.MartialInitiative : 0, rank, weapon: hw.WeaponId));

    /// <summary>Casts the weapon ability (nothing else).</summary>
    private static void CastWeapon(StepDriver f)
    {
        GearFights.Fill(f);
        f.Cast(f.AspectWeapon());
    }

    /// <summary>
    /// Player: the weapon ability gives +10% Main Stat.
    /// Dev: Main Stat is 1000 before, 1100 right after the weapon cast.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void The_Weapon_Ability_Gives_Ten_Percent_Main_Stat(AspectHeroWeapon hw)
    {
        var f = Fight(hw, true, 4);
        Assert.Equal(1000.0, f.Hero.PrimaryAttribute(f.Now), 1e-9);
        CastWeapon(f);
        Assert.Equal(1100.0, f.Hero.PrimaryAttribute(f.Now), 1e-9);
    }

    /// <summary>
    /// Player: the buff lasts the rank's share (10% / 18% / 25% / 32%) of the weapon ability's default cooldown: Fated Strike 6s at rank 1, Chronoshift
    /// 57.6s at rank 4.
    /// Dev: the buff's removal minus its gain. The engine removes auras about 17ms late (a server frame), hence the 0.05s tolerance.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeaponRanks))]
    public void The_Buff_Lasts_The_Rank_Share_Of_The_Weapons_Default_Cooldown(AspectHeroWeapon hw, int rank)
    {
        var f = Fight(hw, true, rank);
        CastWeapon(f);
        double expected = DurationShare[rank - 1] * hw.WeaponCooldown;
        f.Wait(expected + 1.0);
        var (gained, removed) = f.AspectAuraWindow("Martial Initiative");
        Assert.Equal(expected, removed - gained, 0.05);
    }

    /// <summary>
    /// Player: when the buff ends the Main Stat is back to normal.
    /// Dev: rank 1, one second after the buff's end.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void The_Main_Stat_Comes_Back_When_It_Ends(AspectHeroWeapon hw)
    {
        var f = Fight(hw, true, 1);
        CastWeapon(f);
        f.Wait(0.10 * hw.WeaponCooldown + 1.0);
        Assert.Equal(1000.0, f.Hero.PrimaryAttribute(f.Now), 1e-9);
    }

    /// <summary>
    /// Player: the bigger Main Stat shows in your damage: a hit after the weapon ability deals 10% more than the same hit without the aspect.
    /// Dev: the weapon cast, then the unit hit (inside the shortest buff, Icicles of An'zhyr's 4s); its first damage event against the same fight without
    /// the aspect: ratio 1.1.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroWeapons))]
    public void The_Bigger_Main_Stat_Shows_In_Damage(AspectHeroWeapon hw)
    {
        double Hit(bool aspect)
        {
            var f = Fight(hw, aspect, 1);
            CastWeapon(f);
            int before = f.Events.Count;
            AspectFights.CastWhenReady(f, hw.Hero, hw.Hero.Unit);
            var unit = hw.Hero.Get(f, hw.Hero.Unit).Name;
            return f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage && e.Name == unit).Value;
        }
        Assert.Equal(1.1, Hit(true) / Hit(false), 1e-6);
    }

    /// <summary>
    /// Player: only the weapon ability gives it.
    /// Dev: the hero's other abilities cast (its five, with full resources): Main Stat stays 1000.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Other_Abilities_Do_Not_Give_It(GearHero hero)
    {
        var f = hero.Fight(AspectFights.With(AspectFights.MartialInitiative, 4, weapon: hero.WeaponAbilities[0]));
        foreach (var type in new[] { hero.Basic, hero.Core, hero.Power, hero.Major, hero.Spirit })
        {
            hero.CastProbe(f, type);
            Assert.Equal(1000.0, f.Hero.PrimaryAttribute(f.Now), 1e-9);
        }
    }
}
