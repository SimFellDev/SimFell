using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Icicles of An'zhyr (Frozen Staff of An'zhyr), over every hero that can have it (Rime, Ardeos, Elarion): "Conjure 3 waves of icicles to
/// pelt the target area, dealing 144% Main Stat frost damage to all enemies in the area with each wave. Enemies hit by the final wave are
/// afflicted by Curse of An'zhyr permanently, taking 38.4% Main Stat frost damage every 3 seconds. Enemies afflicted by Curse of An'zhyr
/// take +200% increased direct damage from Icicles of An'zhyr. Can be cast during Global Cooldown."
///
/// Its cooldown, and casting it during the global cooldown, are in WeaponCooldowns.cs.
/// </summary>
public class WeaponIciclesOfAnzhyr
{
    // ---- Icicles of An'zhyr

    /// <summary>
    /// Each of the 3 waves of Icicles of An'zhyr deals 144% of your main stat, times your class's balancing aura (Rime 1.04, Ardeos 1.1,
    /// Elarion 1.08).
    /// </summary>
    [Theory]
    [InlineData("Rime", 1.04)]
    [InlineData("Ardeos", 1.1)]
    [InlineData("Elarion", 1.08)]
    public void Each_Icicles_Wave_Deals_144_Percent_Main_Stat(string hero, double balancing)
    {
        var f = StepFights.Of(hero, weaponAbilityId: IciclesOfAnzhyr.AbilityId).WithoutDamageSpread();

        f.Cast<IciclesOfAnzhyr>();
        f.Wait(3); // the waves land

        var waves = f.Damage<IciclesOfAnzhyr>();

        using var check = new Checks(f);
        check.Equal(3, waves.Count);
        foreach (var wave in waves) check.Ratio(1.44 * balancing, wave.Value / f.MainStat(), what: $"wave at {wave.Time.TotalSeconds:F3}s");
    }

    /// <summary>
    /// The waves of Icicles of An'zhyr land 0.27, 1.27 and 2.27 seconds after the cast, whatever your haste.
    ///
    /// From combat logs.
    /// </summary>
    [Theory]
    [InlineData("Rime", 0.0)]
    [InlineData("Rime", 0.5)]
    [InlineData("Ardeos", 0.0)]
    [InlineData("Ardeos", 0.5)]
    [InlineData("Elarion", 0.0)]
    [InlineData("Elarion", 0.5)]
    public void Icicles_Waves_Land_0_27_1_27_And_2_27_Seconds_After_The_Cast(string hero, double haste)
    {
        var f = StepFights.Of(hero, haste: haste, weaponAbilityId: IciclesOfAnzhyr.AbilityId);

        f.Cast<IciclesOfAnzhyr>();
        f.Wait(3); // the waves land

        double cast = f.CompletedAt("Icicles of An'zhyr");
        var waves = f.Damage<IciclesOfAnzhyr>();

        using var check = new Checks(f);
        check.Equal(3, waves.Count);
        check.Seconds(cast + 0.27, waves[0].Time.TotalSeconds);
        check.Seconds(cast + 1.27, waves[1].Time.TotalSeconds);
        check.Seconds(cast + 2.27, waves[2].Time.TotalSeconds);
    }

    /// <summary>
    /// Each wave of Icicles of An'zhyr hits every enemy, and beyond 15 enemies each takes sqrt(15 / enemies) of it.
    ///
    /// From combat logs.
    /// </summary>
    [Theory]
    [InlineData("Rime", 15)]
    [InlineData("Rime", 20)]
    [InlineData("Ardeos", 15)]
    [InlineData("Ardeos", 20)]
    [InlineData("Elarion", 15)]
    [InlineData("Elarion", 20)]
    public void Icicles_Waves_Hit_Every_Enemy_For_Less_Beyond_15(string hero, int enemies)
    {
        List<double> FirstWaveOnEachEnemy(int enemies)
        {
            var f = StepFights.Of(hero, enemies: enemies, weaponAbilityId: IciclesOfAnzhyr.AbilityId).WithoutDamageSpread();

            f.Cast<IciclesOfAnzhyr>();
            f.Wait(3); // the waves land

            return f.FirstHitOnEachEnemy("Icicles of An'zhyr");
        }

        double aloneWave = FirstWaveOnEachEnemy(1).Single();
        var crowdWaves = FirstWaveOnEachEnemy(enemies);

        using var check = new Checks();
        check.Equal(enemies, crowdWaves.Count);
        foreach (double wave in crowdWaves) check.Ratio(Math.Min(1, Math.Sqrt(15.0 / enemies)), wave / aloneWave);
    }

    // ---- Curse of An'zhyr

    /// <summary>The final wave of Icicles of An'zhyr curses the enemies it hits for good; the waves before it do not.</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    public void The_Final_Icicles_Wave_Curses_Its_Enemies_For_Good(string hero)
    {
        var f = StepFights.Of(hero, weaponAbilityId: IciclesOfAnzhyr.AbilityId);

        f.Cast<IciclesOfAnzhyr>();
        double finalWave = f.CompletedAt("Icicles of An'zhyr") + 2.27;

        using var check = new Checks(f);
        f.WaitUntil(finalWave - 0.1);
        check.False(f.Has(IciclesOfAnzhyr.Curse, on: f.Enemy));
        f.WaitUntil(finalWave + 0.1);
        check.True(f.Has(IciclesOfAnzhyr.Curse, on: f.Enemy));
        f.WaitUntil(100);
        check.True(f.Has(IciclesOfAnzhyr.Curse, on: f.Enemy));
    }

    /// <summary>
    /// Curse of An'zhyr deals 38.4% of your main stat every 3 seconds shortened by haste, the first 3 hasted seconds after the final wave.
    ///
    /// Its damage is compared with a wave's 144%, before the enemy is cursed.
    /// </summary>
    [Theory]
    [InlineData("Rime", 0.0)]
    [InlineData("Rime", 0.5)]
    [InlineData("Ardeos", 0.0)]
    [InlineData("Ardeos", 0.5)]
    [InlineData("Elarion", 0.0)]
    [InlineData("Elarion", 0.5)]
    public void Curse_Of_Anzhyr_Deals_38_4_Percent_Every_3_Hasted_Seconds(string hero, double haste)
    {
        var f = StepFights.Of(hero, haste: haste, weaponAbilityId: IciclesOfAnzhyr.AbilityId).WithoutDamageSpread();

        f.Cast<IciclesOfAnzhyr>();
        f.Wait(30); // the curse ticks

        var waves = f.Damage<IciclesOfAnzhyr>();
        var ticks = f.Damage("Curse of An'zhyr");
        double finalWave = waves[^1].Time.TotalSeconds;
        double period = 3.0 / (1 + haste);

        using var check = new Checks(f);
        check.Equal(FullTicks(f.Seconds() - finalWave, period), ticks.Count);
        for (int i = 0; i < ticks.Count; i++)
        {
            check.Seconds(finalWave + (i + 1) * period, ticks[i].Time.TotalSeconds, what: $"tick {i + 1}");
            check.Ratio(0.384 / 1.44, ticks[i].Value / waves[0].Value, what: $"tick {i + 1}");
        }
    }

    /// <summary>An enemy with Curse of An'zhyr takes 200% more damage from Icicles of An'zhyr: each wave deals 3 times as much.</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    public void A_Cursed_Enemy_Takes_3_Times_The_Damage_From_Icicles(string hero)
    {
        var f = StepFights.Of(hero, weaponAbilityId: IciclesOfAnzhyr.AbilityId).WithoutDamageSpread();

        f.Cast<IciclesOfAnzhyr>();
        f.Wait(40); // the cooldown
        f.Cast<IciclesOfAnzhyr>();
        f.Wait(3); // the waves land

        var waves = f.Damage<IciclesOfAnzhyr>();

        using var check = new Checks(f);
        check.Equal(6, waves.Count);
        foreach (var wave in waves.Skip(3))
        {
            check.Ratio(3.0, wave.Value / waves[0].Value, what: $"wave at {wave.Time.TotalSeconds:F3}s");
        }
    }

    /// <summary>Curse of An'zhyr leaves your other damage alone: your own abilities deal the same to a cursed enemy.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt")]
    [InlineData("Ardeos", "Infernal Wave")]
    [InlineData("Elarion", "Focused Shot")]
    public void Curse_Of_Anzhyr_Does_Not_Raise_Your_Other_Damage(string hero, string ability)
    {
        double FirstHit(bool cursed)
        {
            var f = StepFights.Of(hero, weaponAbilityId: IciclesOfAnzhyr.AbilityId).WithoutDamageSpread();
            f.FillResources(); // enough to cast it

            if (cursed)
            {
                f.Cast<IciclesOfAnzhyr>();
                f.Wait(3); // the final wave curses the enemy
            }

            f.Cast(ability);
            f.Wait(1); // the hit lands

            return f.Damage(ability)[0].Value;
        }

        AssertRatio(1.0, FirstHit(cursed: true) / FirstHit(cursed: false));
    }
}
