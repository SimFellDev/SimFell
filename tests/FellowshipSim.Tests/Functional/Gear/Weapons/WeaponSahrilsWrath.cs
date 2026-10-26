using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>
/// Sahril's Wrath (Sahril's Arms, melee heroes): "Instantly deal 2109% fire damage to all enemies within a 1000 radius. You gain 1 stack of Sundering Wrath for
/// each enemy hit up to a maximum of 5 stacks, granting you +6% additional critical strike chance per stack for 20 seconds. Can be cast during Global
/// Cooldown." Cooldown 120 seconds; the damage falls off as sqrt(1/N) beyond 1 enemy (game data: threshold 1; the log: 13 enemies hit gave 5 stacks).
/// Dev: damage is the expected value with no crit; a hit is read as a multiple of Main Stat (1000) over the class tuning.
/// </summary>
public class WeaponSahrilsWrath
{
    private const double Ratio = 0.001;
    private const int Id = WeaponHeroes.SahrilsWrathId;

    /// <summary>Player: Sahril's Wrath deals 2109% of your Main Stat to an enemy. Dev: one enemy.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.SahrilsWrath), MemberType = typeof(WeaponHeroes))]
    public void It_Deals_2109_Percent(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());

        Assert.Equal(21.09, f.Coefficient(f.Damage("Sahril's Wrath").Single().Value), 21.09 * Ratio);
    }

    /// <summary>
    /// Player: it hits every enemy, each one weaker as there are more: 1/sqrt(N) of the damage each.
    /// Dev: 4 enemies: four hits, one per enemy, each 2109% x sqrt(1/4).
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.SahrilsWrath), MemberType = typeof(WeaponHeroes))]
    public void It_Hits_Every_Enemy_With_A_Falloff(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 4);
        f.Cast(f.Weapon());

        var hits = f.Damage("Sahril's Wrath");
        Assert.Equal(4, hits.Count);
        Assert.Equal(4, hits.Select(h => h.Target).Distinct().Count());
        foreach (var hit in hits) Assert.Equal(21.09 * 0.5, f.Coefficient(hit.Value), 21.09 * 0.5 * Ratio);
    }

    /// <summary>
    /// Player: you gain a stack of Sundering Wrath for each enemy hit, up to 5, each giving +6% crit chance.
    /// Dev: 1, 3, 5 and 7 enemies: the stacks after the cast are 1, 3, 5 and 5 (the cap); the next hit's crit chance is 6% per stack.
    /// </summary>
    [Theory]
    [MemberData(nameof(Stacks))]
    public void It_Gives_A_Stack_Per_Enemy_Hit_Up_To_Five(GearHero hero, int enemies, int stacks)
    {
        var f = hero.WithWeapon(Id, enemies: enemies);
        f.Cast(f.Weapon());
        int before = f.Events.Count;
        // Core ability: Tariq's Wild Swing cannot crit, so the melee heroes' Core abilities are the hit that shows a crit chance.
        hero.CastProbe(f, hero.Core);

        var hit = f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage);
        Assert.Equal(0.06 * stacks, hit.CritChance, 1e-9);
    }

    public static IEnumerable<object[]> Stacks =>
        WeaponHeroes.SahrilsWrath.SelectMany(h => new[] { 1, 3, 5, 7 }.Select(n => new[] { h[0], n, Math.Min(n, 5) }));

    /// <summary>
    /// Player: Sundering Wrath lasts 20 seconds.
    /// Dev: up just before 20 seconds after the cast and gone just after (the game ends a timed aura up to half a frame late, so the check is at 20.03 seconds).
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.SahrilsWrath), MemberType = typeof(WeaponHeroes))]
    public void Sundering_Wrath_Lasts_20_Seconds(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 3);
        f.Cast(f.Weapon());
        double castAt = f.CompletedAt("Sahril's Wrath");

        f.WaitUntil(castAt + 19.99);
        Assert.True(f.IsUp("Sundering Wrath"));
        f.WaitUntil(castAt + 20.03);
        Assert.False(f.IsUp("Sundering Wrath"));
    }

    /// <summary>Player: Sahril's Wrath has a 120 second cooldown, and haste does not shorten it. Dev: 0% and 50% haste, read the moment the cast completes.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.SahrilsWrath), MemberType = typeof(WeaponHeroes))]
    public void Its_Cooldown_Is_120_Seconds_Whatever_The_Haste(GearHero hero)
    {
        foreach (double haste in new[] { 0.0, 0.5 })
        {
            var f = hero.WithWeapon(Id, haste: haste);
            var remaining = f.CooldownAtCompletion(f.Weapon());
            f.Cast(f.Weapon());

            Assert.Equal(120.0, remaining(), 0.002);
        }
    }

    /// <summary>
    /// Player: it can be cast during the global cooldown.
    /// Dev: an instant ability of the hero starts a global cooldown (started with the engine's cast command, which does not wait for it to end); Sahril's
    /// Wrath is cast 0.05 seconds later, with the global cooldown still running.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.SahrilsWrath), MemberType = typeof(WeaponHeroes))]
    public void It_Can_Be_Cast_During_The_Global_Cooldown(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.StartGlobalCooldown(hero);

        Assert.True(f.TryCast(f.Weapon()).Succeeded);
    }
}
