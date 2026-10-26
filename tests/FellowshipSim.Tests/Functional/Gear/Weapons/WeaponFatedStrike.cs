using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>
/// Fated Strike (Fateful Arms, melee heroes): "Strike target enemy, dealing 739% damage to it and cleaving for 287.4% damage to nearby enemies. Fated Strike
/// applies Glorious Purpose to you for 6 seconds, granting you +200% increased Cooldown Acceleration for the duration." Cooldown 60 seconds.
/// Dev: damage is the expected value with no crit; a hit is read as a multiple of Main Stat (1000) over the class tuning (<see cref="WeaponFights.Coefficient"/>).
/// The cleave loses damage as sqrt(3/N) beyond 3 enemies cleaved (the log: 26 other enemies hit gave sqrt(3/26)).
/// </summary>
public class WeaponFatedStrike
{
    private const double Ratio = 0.001;
    private const int Id = WeaponHeroes.FatedStrikeId;

    /// <summary>Player: Fated Strike hits the target for 739% of your Main Stat. Dev: one enemy.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.FatedStrike), MemberType = typeof(WeaponHeroes))]
    public void It_Hits_The_Target_For_739_Percent(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());

        Assert.Equal(7.39, f.Coefficient(f.Damage("Fated Strike").Single().Value), 7.39 * Ratio);
    }

    /// <summary>
    /// Player: it cleaves for 287.4% on each nearby enemy, and up to 3 of them take it at full strength.
    /// Dev: 4 enemies (3 nearby): the target's hit is 739%, each of the other three 287.4%.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.FatedStrike), MemberType = typeof(WeaponHeroes))]
    public void It_Cleaves_Nearby_Enemies_For_287_Percent(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 4);
        f.Cast(f.Weapon());

        var hits = f.Damage("Fated Strike");
        Assert.Equal(4, hits.Count);
        Assert.Equal("Training Mob 1", hits[0].Target);
        Assert.Equal(7.39, f.Coefficient(hits[0].Value), 7.39 * Ratio);
        foreach (var cleave in hits.Skip(1)) Assert.Equal(2.874, f.Coefficient(cleave.Value), 2.874 * Ratio);
        Assert.Equal(3, hits.Skip(1).Select(h => h.Target).Distinct().Count());
    }

    /// <summary>
    /// Player: past 3 nearby enemies the cleave weakens: each one takes sqrt(3/N) of it.
    /// Dev: 6 enemies (5 nearby, the target not counted, as in the log with 26 others), so each cleave is 287.4% x sqrt(3/5); the target is not weakened.
    /// TODO: check in game whether the count includes the target (the log with 26 others fits sqrt(3/26) with the target left out).
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.FatedStrike), MemberType = typeof(WeaponHeroes))]
    public void The_Cleave_Loses_Damage_Beyond_Three_Enemies(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 6);
        f.Cast(f.Weapon());

        var hits = f.Damage("Fated Strike");
        Assert.Equal(6, hits.Count);
        Assert.Equal(7.39, f.Coefficient(hits[0].Value), 7.39 * Ratio);
        double cleave = 2.874 * Math.Sqrt(3.0 / 5.0);
        foreach (var hit in hits.Skip(1)) Assert.Equal(cleave, f.Coefficient(hit.Value), cleave * Ratio);
    }

    /// <summary>Player: Fated Strike has a 60 second cooldown, and haste does not shorten it. Dev: 0% and 50% haste, read the moment the cast completes.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.FatedStrike), MemberType = typeof(WeaponHeroes))]
    public void Its_Cooldown_Is_60_Seconds_Whatever_The_Haste(GearHero hero)
    {
        foreach (double haste in new[] { 0.0, 0.5 })
        {
            var f = hero.WithWeapon(Id, haste: haste);
            var remaining = f.CooldownAtCompletion(f.Weapon());
            f.Cast(f.Weapon());

            Assert.Equal(60.0, remaining(), 0.002);
        }
    }

    /// <summary>
    /// Player: using Fated Strike gives you Glorious Purpose for 6 seconds.
    /// Dev: the aura is up just before 6 seconds after the cast and gone just after (the game ends a timed aura up to half a frame late, so the check is
    /// at 6.03 seconds).
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.FatedStrike), MemberType = typeof(WeaponHeroes))]
    public void It_Gives_Glorious_Purpose_For_6_Seconds(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        double castAt = f.CompletedAt("Fated Strike");

        f.WaitUntil(castAt + 5.99);
        Assert.True(f.IsUp("Glorious Purpose"));
        f.WaitUntil(castAt + 6.03);
        Assert.False(f.IsUp("Glorious Purpose"));
    }

    /// <summary>
    /// Player: Glorious Purpose makes your other cooldowns recover 200% faster, so three times as fast while it lasts.
    /// Dev: the hero's own ability with a cooldown (its Core ability; Mara's Maiden of Death) is cast right after Fated Strike (inside the 6 seconds); 1.5
    /// seconds later it has 3 cooldown seconds less left (counted in cooldown seconds, as the hero reports the time left at the current recovery speed) than the same cast at the same time in a fight without Fated Strike (4.5 seconds of cooldown
    /// recovered against 1.5).
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.FatedStrike), MemberType = typeof(WeaponHeroes))]
    public void Glorious_Purpose_Triples_The_Recovery_Of_Other_Cooldowns(GearHero hero)
    {
        var type = hero.Name == "Mara" ? hero.Major : hero.Core;

        var with = hero.WithWeapon(Id);
        with.Cast(with.Weapon());
        double castAt = with.Now.TotalSeconds;
        hero.CastProbe(with, type);
        double readAt = castAt + 1.5;
        with.WaitUntil(readAt);
        double leftWith = with.CooldownSecondsLeft(hero.Get(with, type));

        var without = hero.WithWeapon(Id);
        without.WaitUntil(castAt);
        hero.CastProbe(without, type);
        without.WaitUntil(readAt);
        double leftWithout = without.CooldownSecondsLeft(hero.Get(without, type));

        Assert.Equal(3.0, leftWithout - leftWith, 0.01);
    }

    /// <summary>
    /// Player: Glorious Purpose does not speed up Fated Strike's own cooldown (a weapon ability ignores cooldown recovery).
    /// Dev: 6 seconds after the cast the cooldown has exactly 6 seconds less left (the whole 6 seconds of the buff ran at normal speed).
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.FatedStrike), MemberType = typeof(WeaponHeroes))]
    public void Glorious_Purpose_Does_Not_Speed_Up_Fated_Strikes_Own_Cooldown(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.Wait(6.0 - f.Now.TotalSeconds);

        Assert.Equal(54.0, f.Hero.GetCooldownRemains("Fated Strike", f.Now), 0.002);
    }
}
