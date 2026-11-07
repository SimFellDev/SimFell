using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Sahril's Wrath (Sahril's Arms), over every hero that can have it (Gunde, Mara, Tariq): "Instantly deal 2109% fire damage to all
/// enemies within a 1000 radius. You gain 1 stack of Sundering Wrath for each enemy hit up to a maximum of 5 stacks, granting you +6%
/// additional critical strike chance per stack for 20 seconds. Can be cast during Global Cooldown."
///
/// Its cooldown, and casting it during the global cooldown, are in WeaponCooldowns.cs.
/// </summary>
public class WeaponSahrilsWrath
{
    // ---- Sahril's Wrath

    /// <summary>
    /// Sahril's Wrath deals 2109% of your main stat, times your class's balancing aura (Gunde 1.0, Mara 1.04, Tariq 1.0).
    /// </summary>
    [Theory]
    [InlineData("Gunde", 1.0)]
    [InlineData("Mara", 1.04)]
    [InlineData("Tariq", 1.0)]
    public void Sahrils_Wrath_Deals_2109_Percent_Main_Stat(string hero, double balancing)
    {
        var f = StepFights.Of(hero, weaponAbilityId: SahrilsWrath.AbilityId).WithoutDamageSpread();

        f.Cast<SahrilsWrath>();

        AssertRatio(21.09 * balancing, f.Damage<SahrilsWrath>().Single().Value / f.MainStat());
    }

    /// <summary>
    /// Sahril's Wrath hits every enemy, and beyond 1 enemy each takes sqrt(1 / enemies) of it.
    ///
    /// From the game data (a threshold of 1 enemy).
    /// </summary>
    [Theory]
    [InlineData("Gunde", 2)]
    [InlineData("Gunde", 5)]
    [InlineData("Mara", 2)]
    [InlineData("Mara", 5)]
    [InlineData("Tariq", 2)]
    [InlineData("Tariq", 5)]
    public void Sahrils_Wrath_Hits_Every_Enemy_For_Less_Beyond_1(string hero, int enemies)
    {
        List<double> HitOnEachEnemy(int enemies)
        {
            var f = StepFights.Of(hero, enemies: enemies, weaponAbilityId: SahrilsWrath.AbilityId).WithoutDamageSpread();

            f.Cast<SahrilsWrath>();

            return f.FirstHitOnEachEnemy("Sahril's Wrath");
        }

        double aloneHit = HitOnEachEnemy(1).Single();
        var crowdHits = HitOnEachEnemy(enemies);

        using var check = new Checks();
        check.Equal(enemies, crowdHits.Count);
        foreach (double hit in crowdHits) check.Ratio(Math.Sqrt(1.0 / enemies), hit / aloneHit);
    }

    // ---- Sundering Wrath

    /// <summary>
    /// Sahril's Wrath gives a stack of Sundering Wrath for each enemy hit, up to 5, and each stack gives 6% crit chance.
    /// </summary>
    [Theory]
    [InlineData("Gunde", 1)]
    [InlineData("Gunde", 3)]
    [InlineData("Gunde", 5)]
    [InlineData("Gunde", 7)]
    [InlineData("Mara", 1)]
    [InlineData("Mara", 3)]
    [InlineData("Mara", 5)]
    [InlineData("Mara", 7)]
    [InlineData("Tariq", 1)]
    [InlineData("Tariq", 3)]
    [InlineData("Tariq", 5)]
    [InlineData("Tariq", 7)]
    public void Sahrils_Wrath_Gives_A_Sundering_Wrath_Stack_Per_Enemy_Hit_Up_To_5_For_6_Percent_Crit_Each(string hero, int enemies)
    {
        var f = StepFights.Of(hero, enemies: enemies, weaponAbilityId: SahrilsWrath.AbilityId);

        f.Cast<SahrilsWrath>();

        int stacks = Math.Min(enemies, 5);

        using var check = new Checks(f);
        check.Equal(stacks, f.Stacks(SahrilsWrath.SunderingWrath));
        check.Equal(0.06 * stacks, f.CritChance());
    }

    /// <summary>Sundering Wrath lasts 20 seconds.</summary>
    [Theory]
    [InlineData("Gunde")]
    [InlineData("Mara")]
    [InlineData("Tariq")]
    public void Sundering_Wrath_Lasts_20_Seconds(string hero)
    {
        var f = StepFights.Of(hero, weaponAbilityId: SahrilsWrath.AbilityId);

        f.Cast<SahrilsWrath>();
        double start = f.CompletedAt("Sahril's Wrath");

        f.WaitUntil(start + 20 - 0.1);
        Assert.True(f.Has(SahrilsWrath.SunderingWrath));
        f.WaitUntil(start + 20 + 0.1);
        Assert.False(f.Has(SahrilsWrath.SunderingWrath));
    }
}
