using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>
/// Icicles of An'zhyr (Frozen Staff of An'zhyr): "Conjure 3 waves of icicles to pelt the target area, dealing 144% Main Stat frost damage to all
/// enemies in the area with each wave. Enemies hit by the final wave are afflicted by Curse of An'zhyr permanently, taking 38.4% Main Stat frost damage every 3
/// seconds. Enemies afflicted by Curse of An'zhyr take +200% increased direct damage from Icicles of An'zhyr. Can be cast during Global Cooldown." Cooldown 40
/// seconds.
/// Dev: damage is the expected value with no crit; a hit is read as a multiple of Main Stat (1000) over the class tuning. From the logs: the waves land 0.27, 1.27
/// and 2.27 seconds after the cast, damage falls off as sqrt(15/N) beyond 15 enemies, the curse ticks every 3 seconds shortened by haste (no tick on
/// application).
/// </summary>
public class WeaponIciclesOfAnzhyr
{
    private const double Ratio = 0.001;
    private const int Id = WeaponHeroes.IciclesId;
    private const string Name = "Icicles of An'zhyr";
    private const string Curse = "Curse of An'zhyr";

    /// <summary>Player: each wave deals 144% of your Main Stat to the enemy. Dev: one enemy, three waves.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void Each_Wave_Deals_144_Percent(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.Wait(3);

        var waves = f.Damage(Name);
        Assert.Equal(3, waves.Count);
        foreach (var wave in waves) Assert.Equal(1.44, f.Coefficient(wave.Value), 1.44 * Ratio);
    }

    /// <summary>
    /// Player: the waves land 0.27, 1.27 and 2.27 seconds after the cast, whatever your haste.
    /// Dev: 0% and 50% haste.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void The_Waves_Land_At_0_27_1_27_And_2_27_Seconds(GearHero hero)
    {
        foreach (double haste in new[] { 0.0, 0.5 })
        {
            var f = hero.WithWeapon(Id, haste: haste);
            f.Cast(f.Weapon());
            f.Wait(3);

            var times = f.Damage(Name).Select(e => e.Time.TotalSeconds).ToList();
            Assert.Equal([0.27, 1.27, 2.27], times.Select(t => Math.Round(t, 3)).ToArray());
        }
    }

    /// <summary>
    /// Player: every wave hits all enemies in the area; past 15 enemies each takes sqrt(15/N) of the damage.
    /// Dev: 20 enemies: each of the 3 waves hits all 20, each for 144% x sqrt(15/20).
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void Waves_Hit_Every_Enemy_With_A_Falloff_Beyond_15(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 20);
        f.Cast(f.Weapon());
        f.Wait(3);

        var waves = f.Damage(Name);
        Assert.Equal(60, waves.Count);
        Assert.Equal(20, waves.Select(w => w.Target).Distinct().Count());
        double each = 1.44 * Math.Sqrt(15.0 / 20.0);
        foreach (var wave in waves) Assert.Equal(each, f.Coefficient(wave.Value), each * Ratio);
    }

    /// <summary>
    /// Player: 15 enemies are hit at full strength (no falloff yet).
    /// Dev: 15 enemies, one wave each at 144%.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void There_Is_No_Falloff_Up_To_15_Enemies(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 15);
        f.Cast(f.Weapon());
        f.Wait(3);

        var waves = f.Damage(Name);
        Assert.Equal(45, waves.Count);
        foreach (var wave in waves) Assert.Equal(1.44, f.Coefficient(wave.Value), 1.44 * Ratio);
    }

    /// <summary>
    /// Player: the enemies hit by the final wave are cursed for good; the first waves do not curse.
    /// Dev: the curse is on the enemy after the final wave (2.27 seconds), not before it, and still there 100 seconds later.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void The_Final_Wave_Curses_The_Enemy_Permanently(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.WaitUntil(2.0);
        Assert.DoesNotContain(f.Enemy.Auras.Active(f.Now), a => a.Def.Name == Curse);
        f.WaitUntil(2.5);
        Assert.Contains(f.Enemy.Auras.Active(f.Now), a => a.Def.Name == Curse);
        f.WaitUntil(100);
        Assert.Contains(f.Enemy.Auras.Active(f.Now), a => a.Def.Name == Curse);
    }

    /// <summary>
    /// Player: the curse deals 38.4% of your Main Stat every 3 seconds, the first one 3 seconds after the final wave.
    /// Dev: no haste, 30 seconds: ticks at 5.27, 8.27 ... 29.27 (9 of them), each 38.4%.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void The_Curse_Ticks_Every_3_Seconds_For_38_4_Percent(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.Wait(30);

        var ticks = f.Damage(Curse);
        Assert.Equal(9, ticks.Count);
        Assert.Equal(5.27, ticks[0].Time.TotalSeconds, 0.002);
        for (int i = 1; i < ticks.Count; i++) Assert.Equal(3.0, ticks[i].Time.TotalSeconds - ticks[i - 1].Time.TotalSeconds, 0.002);
        foreach (var tick in ticks) Assert.Equal(0.384, f.Coefficient(tick.Value), 0.384 * Ratio);
    }

    /// <summary>
    /// Player: haste shortens the curse's tick: every 3 / (1 + haste) seconds, for the same damage.
    /// Dev: 50% haste: ticks every 2 seconds from 4.27, each 38.4%.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void Haste_Shortens_The_Curse_Tick(GearHero hero)
    {
        var f = hero.WithWeapon(Id, haste: 0.5);
        f.Cast(f.Weapon());
        f.Wait(12);

        var ticks = f.Damage(Curse);
        Assert.Equal([4.27, 6.27, 8.27, 10.27], ticks.Select(t => Math.Round(t.Time.TotalSeconds, 3)).ToArray());
        foreach (var tick in ticks) Assert.Equal(0.384, f.Coefficient(tick.Value), 0.384 * Ratio);
    }

    /// <summary>
    /// Player: a cursed enemy takes 200% more direct damage from Icicles: three times each wave.
    /// Dev: a first cast curses the enemy; after the 40 second cooldown a second cast's three waves each deal 144% x 3.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void A_Cursed_Enemy_Takes_Three_Times_The_Damage_From_Icicles(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.WaitUntil(41);
        f.Cast(f.Weapon());
        f.Wait(3);

        var waves = f.Damage(Name);
        Assert.Equal(6, waves.Count);
        Assert.Equal(1.44, f.Coefficient(waves[2].Value), 1.44 * Ratio);
        foreach (var wave in waves.Skip(3)) Assert.Equal(1.44 * 3.0, f.Coefficient(wave.Value), 1.44 * 3.0 * Ratio);
    }

    /// <summary>
    /// Player: the curse's +200% is for Icicles only, not for your other damage or for the curse's own ticks.
    /// Dev: the hero's own hit on a cursed enemy deals the same as on an uncursed one; a curse tick is still 38.4%.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void The_Curse_Does_Not_Boost_Other_Damage(GearHero hero)
    {
        var plain = hero.WithWeapon(Id);
        plain.CastUnit(hero);
        double uncursed = plain.Events.First(e => e.Kind == StepEventKind.Damage).Value;

        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.WaitUntil(10);
        int before = f.Events.Count;
        f.CastUnit(hero);
        double cursed = f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage && e.Name != Curse).Value;

        Assert.Equal(uncursed, cursed, uncursed * Ratio);
    }

    /// <summary>Player: Icicles of An'zhyr has a 40 second cooldown, and haste does not shorten it. Dev: 0% and 50% haste, read the moment the cast completes.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void Its_Cooldown_Is_40_Seconds_Whatever_The_Haste(GearHero hero)
    {
        foreach (double haste in new[] { 0.0, 0.5 })
        {
            var f = hero.WithWeapon(Id, haste: haste);
            var remaining = f.CooldownAtCompletion(f.Weapon());
            f.Cast(f.Weapon());

            Assert.Equal(40.0, remaining(), 0.002);
        }
    }

    /// <summary>
    /// Player: it can be cast during the global cooldown.
    /// Dev: an instant ability of the hero starts a global cooldown and Icicles is cast 0.05 seconds later.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Icicles), MemberType = typeof(WeaponHeroes))]
    public void It_Can_Be_Cast_During_The_Global_Cooldown(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.StartGlobalCooldown(hero);

        Assert.True(f.TryCast(f.Weapon()).Succeeded);
    }
}
