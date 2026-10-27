using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>
/// Voidbringer's Touch (Void-touched Conduit, every hero): "Apply Voidbringer's Touch to target enemy for 15 seconds, causing 10% of all damage you deal to any
/// enemy to be replicated and stored in the effect. Once Voidbringer's Touch expires, or a total of 4250% Main Stat damage has been accumulated, the effect
/// erupts in a shadowy explosion, instantly dealing the accumulated damage to the target. Voidbringer's Touch has +100% critical strike chance. ... always
/// triggers if the damage it holds is greater than the remaining health of the enemy ... Can be cast during Global Cooldown." Cooldown 90 seconds.
/// Dev: damage is the expected value, so an eruption with 100% crit chance is its stored damage times the crit multiplier (2). The stored damage is 10% of the
/// damage events dealt between the cast and the eruption. Unverified in the engine: the eruption comes 0.6-1.2 seconds early in two of six logged cases.
/// </summary>
public class WeaponVoidbringersTouch
{
    private const int Id = WeaponHeroes.VoidbringersTouchId;
    private const string Name = "Voidbringer's Touch";

    private static double StoredBefore(StepDriver f, StepEvent eruption)
    {
        double sum = 0;
        foreach (var e in f.Events)
        {
            if (e.Equals(eruption)) break;
            if (e.Kind == StepEventKind.Damage && e.Name != Name) sum += e.Value;
        }
        return sum;
    }

    /// <summary>
    /// Player: the touch erupts when its 15 seconds run out, and deals nothing before.
    /// Dev: the cast, one hit of the hero's, then 20 seconds; the eruption is the only Voidbringer's Touch damage, 15 seconds after the cast.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.VoidbringersTouch), MemberType = typeof(WeaponHeroes))]
    public void It_Erupts_After_15_Seconds(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        double castAt = f.CompletedAt(Name);
        Assert.Empty(f.Damage(Name));
        f.CastUnit(hero);
        f.WaitUntil(castAt + 14.9);
        Assert.Empty(f.Damage(Name));
        f.WaitUntil(castAt + 20);

        var eruption = Assert.Single(f.Damage(Name));
        Assert.Equal(castAt + 15.0, eruption.Time.TotalSeconds, 0.002);
    }

    /// <summary>
    /// Player: it stores 10% of all the damage you deal and deals it all at the end, with +100% crit chance (so it always crits).
    /// Dev: the cast, one hit of the hero's, then 20 seconds: the eruption is 10% of the damage dealt in between, times the crit multiplier (2) and times the
    /// class tuning again (1.04 for Rime and Mara, 1.1 for Ardeos, 1.08 for Elarion: the engine runs the eruption through the damage calculation, whose scale
    /// the stored damage already carries); its crit chance is 100%. Damage over time of the hero's counts too.
    /// TODO: check in game whether the stored damage is dealt as it is (without the class tuning a second time). The logs' eruptions are 0.94-1.05 of
    /// the engine's (mean 0.99, Rime), which fits both; the test follows the engine.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.VoidbringersTouch), MemberType = typeof(WeaponHeroes))]
    public void It_Deals_10_Percent_Of_The_Damage_You_Dealt_As_A_Crit(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.CastUnit(hero);
        f.Wait(20);

        var eruption = Assert.Single(f.Damage(Name));
        double stored = StoredBefore(f, eruption);
        Assert.True(stored > 0);
        Assert.Equal(1.0, eruption.CritChance, 1e-9);
        Assert.Equal(0.10 * stored * 2.0 * f.Tuning(), eruption.Value, stored * 0.002);
    }

    /// <summary>
    /// Player: the damage you deal to any enemy is stored, not only to the touched one.
    /// Dev: 2 enemies, the touch on the first, the hero's hit on the second: the eruption (on the first) still holds 10% of it, times the crit multiplier and the class tuning (see above).
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.VoidbringersTouch), MemberType = typeof(WeaponHeroes))]
    public void It_Stores_Damage_Dealt_To_Other_Enemies(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 2);
        var first = f.Enemy;
        var second = f.Context.AllEnemies[1];
        f.Cast(f.Weapon(), first);
        GearFights.Fill(f);
        f.Cast(hero.Get(f, hero.Unit), second);
        f.Wait(20);

        var eruption = Assert.Single(f.Damage(Name));
        Assert.Equal(first.Name, eruption.Target);
        double stored = StoredBefore(f, eruption);
        Assert.True(f.Damage(hero.Get(f, hero.Unit).Name).Any(e => e.Target == second.Name));
        Assert.Equal(0.10 * stored * 2.0 * f.Tuning(), eruption.Value, stored * 0.002);
    }

    /// <summary>
    /// Player: when the stored damage reaches 4250% of your Main Stat the touch erupts at once, without waiting for its 15 seconds.
    /// Dev: Expertise 10000% makes each hit about 100 times its size, so the cap is reached within a few casts; the touch is cast and the hero's hits follow
    /// until it erupts: before the 15 seconds are up.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.VoidbringersTouch), MemberType = typeof(WeaponHeroes))]
    public void It_Erupts_As_Soon_As_It_Holds_4250_Percent_Of_Main_Stat(GearHero hero)
    {
        var f = ToTheCap(hero, out double castAt);

        var eruption = Assert.Single(f.Damage(Name));
        Assert.True(eruption.Time.TotalSeconds < castAt + 14, "it erupted at the cap, before its 15 seconds");
    }

    /// <summary>
    /// Player: an eruption at the cap deals 4250% of your Main Stat, as a crit (x2), and your Expertise scales it like any other hit.
    /// Dev: as above with Expertise 10000%: the eruption is 42.5 x 2 x (1 + 100) times Main Stat (and the class tuning). The engine's own comment says the
    /// logged eruptions were 1.37 times too high with Expertise in them; the user confirms Expertise does scale the eruption (it is the damage stored that
    /// should not carry it twice), so the test expects the scaled value.
    /// TODO: the engine's comment on the eruption says Expertise is left out; check that the comment, not the behavior, is what is stale.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.VoidbringersTouch), MemberType = typeof(WeaponHeroes))]
    public void The_Eruption_At_The_Cap_Is_Scaled_By_Expertise(GearHero hero)
    {
        var f = ToTheCap(hero, out _);

        var eruption = Assert.Single(f.Damage(Name));
        Assert.Equal(42.5 * 2.0 * 101.0, f.Coefficient(eruption.Value), 42.5 * 2.0 * 101.0 * 0.002);
    }

    private static StepDriver ToTheCap(GearHero hero, out double castAt)
    {
        var f = hero.WithWeapon(Id, expertise: 100);
        f.Cast(f.Weapon());
        castAt = f.CompletedAt(Name);
        for (int i = 0; i < 40 && !f.Damage(Name).Any() && f.Now.TotalSeconds < castAt + 14; i++)
        {
            GearFights.Fill(f);
            f.TryCast(hero.Get(f, hero.Unit));
        }
        return f;
    }

    /// <summary>
    /// Player: it also erupts at once if what it holds is more than the touched enemy's remaining health.
    /// Dev: 2 enemies, the touch on the first, which is brought down to 1 health; the hero's hit on the second stores more than that, and the touch erupts on the
    /// first right then, not 15 seconds later.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.VoidbringersTouch), MemberType = typeof(WeaponHeroes))]
    public void It_Erupts_At_Once_When_It_Holds_More_Than_The_Enemys_Health(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 2);
        var first = f.Enemy;
        f.Cast(f.Weapon(), first);
        first.CurrentHealth = 1;
        GearFights.Fill(f);
        f.Cast(hero.Get(f, hero.Unit), f.Context.AllEnemies[1]);

        var eruption = Assert.Single(f.Damage(Name));
        Assert.Equal(first.Name, eruption.Target);
        Assert.True(eruption.Time.TotalSeconds < 10);
    }

    /// <summary>Player: Voidbringer's Touch has a 90 second cooldown. Dev: no haste, read the moment the cast completes.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.VoidbringersTouch), MemberType = typeof(WeaponHeroes))]
    public void Its_Cooldown_Is_90_Seconds(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        var remaining = f.CooldownAtCompletion(f.Weapon());
        f.Cast(f.Weapon());

        Assert.Equal(90.0, remaining(), 0.002);
    }

    /// <summary>
    /// Player: it can be cast during the global cooldown.
    /// Dev: an instant ability of the hero starts a global cooldown and Voidbringer's Touch is cast 0.05 seconds later.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.VoidbringersTouch), MemberType = typeof(WeaponHeroes))]
    public void It_Can_Be_Cast_During_The_Global_Cooldown(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.StartGlobalCooldown(hero);

        Assert.True(f.TryCast(f.Weapon()).Succeeded);
    }
}
