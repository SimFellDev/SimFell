using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>
/// Nature's Fury (Branch of the Everliving, ranged heroes): "Conjure the forces of nature to bounce between up to 4 enemies, dealing 1397.7% Main Stat nature
/// damage to each one. The 1st hit from Nature's Fury has +100% increased damage or healing. Nature's Fury has an additional +30% Critical Strike chance." Cast
/// time 1.5 seconds (hasted), cooldown 60 seconds.
/// Dev: damage is the expected value, so the +30% crit chance adds 30% to each hit (crit multiplier 2): a hit is 13.977 x 1.3 times Main Stat (and the class
/// tuning). From the logs of two players with the weapon (not Rime logs): the first hit lands as the cast ends and each bounce 0.3 seconds after the one before;
/// a bounce can strike an enemy already hit.
/// </summary>
public class WeaponNaturesFury
{
    private const double Ratio = 0.001;
    private const int Id = WeaponHeroes.NaturesFuryId;
    private const string Name = "Nature's Fury";

    /// <summary>One hit's coefficient with its extra 30% crit chance (and the crit multiplier 2).</summary>
    private const double Hit = 13.977 * 1.3;

    /// <summary>Player: the cast takes 1.5 seconds, shortened by haste. Dev: 0%, 20% and 50% haste.</summary>
    [Theory]
    [MemberData(nameof(CastTimes))]
    public void The_Cast_Time_Is_1_5_Seconds_Shortened_By_Haste(GearHero hero, double haste)
    {
        var f = hero.WithWeapon(Id, haste: haste);
        f.Cast(f.Weapon());

        Assert.Equal(1.5 / (1 + haste), f.CompletedAt(Name), 0.002);
    }

    public static IEnumerable<object[]> CastTimes =>
        WeaponHeroes.NaturesFury.SelectMany(h => new[] { 0.0, 0.2, 0.5 }.Select(haste => new[] { h[0], (object)haste }));

    /// <summary>
    /// Player: it hits 4 times, the first as the cast ends and each next one 0.3 seconds after the one before (haste does not change it).
    /// Dev: 0% and 50% haste.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.NaturesFury), MemberType = typeof(WeaponHeroes))]
    public void It_Hits_Four_Times_0_3_Seconds_Apart(GearHero hero)
    {
        foreach (double haste in new[] { 0.0, 0.5 })
        {
            var f = hero.WithWeapon(Id, haste: haste);
            f.Cast(f.Weapon());
            f.Wait(3);

            double cast = f.CompletedAt(Name);
            var hits = f.Damage(Name);
            Assert.Equal(4, hits.Count);
            Assert.Equal(cast, hits[0].Time.TotalSeconds, 0.002);
            for (int i = 1; i < 4; i++) Assert.Equal(cast + 0.3 * i, hits[i].Time.TotalSeconds, 0.002);
        }
    }

    /// <summary>
    /// Player: each hit deals 1397.7% of your Main Stat; the first has +100% damage (twice as much).
    /// Dev: one enemy; with the +30% crit chance every hit is 13.977 x 1.3, and the first twice that.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.NaturesFury), MemberType = typeof(WeaponHeroes))]
    public void Each_Hit_Deals_1397_7_Percent_And_The_First_Twice_That(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.Wait(3);

        var hits = f.Damage(Name);
        Assert.Equal(2 * Hit, f.Coefficient(hits[0].Value), 2 * Hit * Ratio);
        foreach (var hit in hits.Skip(1)) Assert.Equal(Hit, f.Coefficient(hit.Value), Hit * Ratio);
    }

    /// <summary>
    /// Player: Nature's Fury has an additional +30% crit chance, on top of yours.
    /// Dev: 20% crit: each hit's crit chance is 50%.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.NaturesFury), MemberType = typeof(WeaponHeroes))]
    public void It_Has_An_Extra_30_Percent_Crit_Chance(GearHero hero)
    {
        var f = hero.WithWeapon(Id, crit: 0.2);
        f.Cast(f.Weapon());
        f.Wait(3);

        foreach (var hit in f.Damage(Name)) Assert.Equal(0.5, hit.CritChance, 1e-9);
    }

    /// <summary>
    /// Player: it bounces between up to 4 enemies: with more enemies around, it still hits 4 times in all, and a bounce can land on an enemy already hit.
    /// Dev: 6 enemies: 4 hits in all, the first on the target.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.NaturesFury), MemberType = typeof(WeaponHeroes))]
    public void It_Hits_Four_Times_In_All_With_Many_Enemies(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 6);
        f.Cast(f.Weapon());
        f.Wait(3);

        var hits = f.Damage(Name);
        Assert.Equal(4, hits.Count);
        Assert.Equal(f.Enemy.Name, hits[0].Target);
    }

    /// <summary>Player: Nature's Fury has a 60 second cooldown, and haste does not shorten it. Dev: 0% and 50% haste, read the moment the cast completes.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.NaturesFury), MemberType = typeof(WeaponHeroes))]
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
}
