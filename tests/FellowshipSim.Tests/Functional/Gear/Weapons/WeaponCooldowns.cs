using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Tests.Functional.GearAspect;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>
/// A weapon ability's cooldown, for every weapon ability and every hero that can have it: it is fixed (Chronoshift 180 seconds, Voidbringer's Touch 90,
/// Sahril's Wrath 120, Fated Strike and Nature's Fury 60, Icicles of An'zhyr 40) whatever the haste and whatever cooldown recovery the hero has, the
/// weapon's own effects included (Chronoshift's barrier, Fated Strike's Glorious Purpose). Only Visions of Grandeur, Brave Machinations and the Emerald
/// gem's Blessing of the Commander change it (tested in their own classes and below).
/// </summary>
public class WeaponCooldowns
{
    public static IEnumerable<object[]> Pairs => WeaponHeroes.Pairs.Select(p => new[] { p[0], p[1] });

    public static IEnumerable<object[]> PairsWithHaste =>
        WeaponHeroes.Pairs.SelectMany(p => new[] { 0.0, 0.5 }.Select(haste => new[] { p[0], p[1], (object)haste }));

    private static double Default(int weaponId) => AspectFights.WeaponCooldowns[weaponId];

    /// <summary>Player: the cooldown starts at its default length and haste does not shorten it. Dev: 0% and 50% haste, read the moment the cast completes.</summary>
    [Theory]
    [MemberData(nameof(PairsWithHaste))]
    public void The_Cooldown_Is_The_Default_Whatever_The_Haste(GearHero hero, int weaponId, double haste)
    {
        var f = hero.WithWeapon(weaponId, haste: haste);
        var remaining = f.CooldownAtCompletion(f.Weapon());
        f.Cast(f.Weapon());

        Assert.Equal(Default(weaponId), remaining(), 0.002);
    }

    /// <summary>
    /// Player: nothing speeds the cooldown up: it runs at normal speed, also while the weapon's own barrier or buff gives cooldown recovery.
    /// Dev: 50% haste; 10 seconds after the cast completes, 10 seconds of the default cooldown have run down (Chronoshift's channel gives 800% recovery
    /// for 3 seconds and Fated Strike gives 200% for 6; neither touches its own weapon ability).
    /// </summary>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void The_Cooldown_Runs_At_Normal_Speed(GearHero hero, int weaponId)
    {
        var f = hero.WithWeapon(weaponId, haste: 0.5);
        f.Cast(f.Weapon());
        double done = f.CompletedAt(WeaponHeroes.NameOf(weaponId));
        f.WaitUntil(done + 10.0);

        Assert.Equal(Default(weaponId) - 10.0, f.Hero.GetCooldownRemains(f.Weapon().Name, f.Now), 0.05);
    }

    /// <summary>
    /// Player: Blessing of the Commander shortens a weapon ability's cooldown too (4% at 450 gem power, 12% at 1500).
    /// Dev: the cooldown the moment the cast completes with the Emerald gem, over the default.
    /// </summary>
    [Theory]
    [MemberData(nameof(PairsWithPower))]
    public void Blessing_Of_The_Commander_Shortens_It(GearHero hero, int weaponId, double power, double share)
    {
        var f = hero.Fight(new GearLoadout { WeaponAbilityId = weaponId, MainStat = WeaponFights.MainStat, Gems = [(GemColor.Emerald, power)] });
        var remaining = f.CooldownAtCompletion(f.Weapon());
        f.Cast(f.Weapon());

        Assert.Equal(Default(weaponId) * (1.0 - share), remaining(), 0.002);
    }

    public static IEnumerable<object[]> PairsWithPower =>
        WeaponHeroes.Pairs.SelectMany(p => new[] { (449.0, 0.0), (450.0, 0.04), (1500.0, 0.12) }.Select(x => new[] { p[0], p[1], (object)x.Item1, x.Item2 }));
}
