using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Nature's Fury (Branch of the Everliving), over every hero that can have it (Rime, Ardeos, Elarion): "Conjure the forces of nature to
/// bounce between up to 4 enemies, dealing 1397.7% Main Stat nature damage to each one. The 1st hit from Nature's Fury has +100% increased
/// damage or healing. Nature's Fury has an additional +30% Critical Strike chance."
///
/// Its cooldown is in WeaponCooldowns.cs. Which enemy each bounce picks is left to the dice.
/// </summary>
public class WeaponNaturesFury
{
    /// <summary>Nature's Fury's cast takes 1.5 seconds, shortened by haste.</summary>
    [Theory]
    [InlineData("Rime", 0.0)]
    [InlineData("Rime", 0.2)]
    [InlineData("Rime", 0.5)]
    [InlineData("Ardeos", 0.0)]
    [InlineData("Ardeos", 0.2)]
    [InlineData("Ardeos", 0.5)]
    [InlineData("Elarion", 0.0)]
    [InlineData("Elarion", 0.2)]
    [InlineData("Elarion", 0.5)]
    public void Natures_Furys_Cast_Takes_1_5_Seconds_Shortened_By_Haste(string hero, double haste)
    {
        var f = StepFights.Of(hero, haste: haste, weaponAbilityId: NaturesFury.AbilityId);

        f.Cast<NaturesFury>();

        AssertSeconds(1.5 / (1 + haste), f.LastCastDuration("Nature's Fury"));
    }

    /// <summary>
    /// Nature's Fury hits 4 times: the first as the cast completes, then each 0.3 seconds after the one before, whatever your haste.
    ///
    /// From the combat logs of two players with the weapon.
    /// </summary>
    [Theory]
    [InlineData("Rime", 0.0)]
    [InlineData("Rime", 0.5)]
    [InlineData("Ardeos", 0.0)]
    [InlineData("Ardeos", 0.5)]
    [InlineData("Elarion", 0.0)]
    [InlineData("Elarion", 0.5)]
    public void Natures_Fury_Hits_4_Times_0_3_Seconds_Apart(string hero, double haste)
    {
        var f = StepFights.Of(hero, haste: haste, weaponAbilityId: NaturesFury.AbilityId);

        f.Cast<NaturesFury>();
        f.Wait(1); // the bounces land

        double cast = f.CompletedAt("Nature's Fury");
        var hits = f.Damage<NaturesFury>();

        using var check = new Checks(f);
        check.Equal(4, hits.Count);
        for (int i = 0; i < hits.Count; i++) check.Seconds(cast + 0.3 * i, hits[i].Time.TotalSeconds, what: $"hit {i + 1}");
    }

    /// <summary>
    /// Each hit of Nature's Fury after the first deals 1397.7% of your main stat, times your class's balancing aura (Rime 1.04, Ardeos 1.1,
    /// Elarion 1.08).
    /// </summary>
    [Theory]
    [InlineData("Rime", 1.04)]
    [InlineData("Ardeos", 1.1)]
    [InlineData("Elarion", 1.08)]
    public void Each_Natures_Fury_Hit_Deals_1397_7_Percent_Main_Stat(string hero, double balancing)
    {
        var f = StepFights.Of(hero, weaponAbilityId: NaturesFury.AbilityId).WithoutDamageSpread();

        f.Cast<NaturesFury>(crit: false); // its own 30% crit chance would roll
        f.Wait(1); // the bounces land

        var hits = f.Damage<NaturesFury>();

        using var check = new Checks(f);
        check.Equal(4, hits.Count);
        foreach (var hit in hits.Skip(1))
        {
            check.Ratio(13.977 * balancing, hit.Value / f.MainStat(), what: $"hit at {hit.Time.TotalSeconds:F3}s");
        }
    }

    /// <summary>The first hit of Nature's Fury deals 100% more: twice the hits after it.</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    public void Natures_Furys_First_Hit_Deals_Twice_As_Much(string hero)
    {
        var f = StepFights.Of(hero, weaponAbilityId: NaturesFury.AbilityId).WithoutDamageSpread();

        f.Cast<NaturesFury>(crit: false); // its own 30% crit chance would roll
        f.Wait(1); // the bounces land

        var hits = f.Damage<NaturesFury>();

        AssertRatio(2.0, hits[0].Value / hits[1].Value);
    }

    /// <summary>Nature's Fury has 30% more crit chance than you: 50% with your 20%.</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    public void Natures_Fury_Has_30_Percent_More_Crit_Chance(string hero)
    {
        var f = StepFights.Of(hero, crit: 0.2, weaponAbilityId: NaturesFury.AbilityId);

        f.Cast<NaturesFury>(crit: false);
        f.Wait(1); // the bounces land

        var hits = f.Damage<NaturesFury>();

        using var check = new Checks(f);
        check.Equal(4, hits.Count);
        foreach (var hit in hits) check.Equal(0.2 + 0.3, hit.CritChance, what: $"hit at {hit.Time.TotalSeconds:F3}s");
    }

    /// <summary>
    /// Nature's Fury bounces up to 4 times in all, the first on your target, however many enemies there are.
    ///
    /// A bounce can strike an enemy already hit, so the test does not count the enemies hit.
    /// </summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    public void Natures_Fury_Hits_4_Times_In_All_Among_Many_Enemies(string hero)
    {
        var f = StepFights.Of(hero, enemies: 6, weaponAbilityId: NaturesFury.AbilityId);

        f.Cast<NaturesFury>();
        f.Wait(1); // the bounces land

        var hits = f.Damage<NaturesFury>();

        using var check = new Checks(f);
        check.Equal(4, hits.Count);
        check.True(hits[0].Target == f.Enemy.Name);
    }
}
