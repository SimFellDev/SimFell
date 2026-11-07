using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Fated Strike (Fateful Arms), over every hero that can have it (Gunde, Mara, Tariq): "Strike target enemy, dealing 739% damage to it
/// and cleaving for 287.4% damage to nearby enemies. Fated Strike applies Glorious Purpose to you for 6 seconds, granting you +200%
/// increased Cooldown Acceleration for the duration."
///
/// Its cooldown, which Glorious Purpose does not speed up, is in WeaponCooldowns.cs.
/// </summary>
public class WeaponFatedStrike
{
    // ---- Fated Strike

    /// <summary>
    /// Fated Strike hits its target for 739% of your main stat, times your class's balancing aura (Gunde 1.0, Mara 1.04, Tariq 1.0).
    /// </summary>
    [Theory]
    [InlineData("Gunde", 1.0)]
    [InlineData("Mara", 1.04)]
    [InlineData("Tariq", 1.0)]
    public void Fated_Strike_Hits_Its_Target_For_739_Percent_Main_Stat(string hero, double balancing)
    {
        var f = StepFights.Of(hero, weaponAbilityId: FatedStrike.AbilityId).WithoutDamageSpread();

        f.Cast<FatedStrike>();

        AssertRatio(7.39 * balancing, f.Damage<FatedStrike>().Single().Value / f.MainStat());
    }

    /// <summary>
    /// Fated Strike cleaves each nearby enemy for 287.4%, and beyond 3 of them each takes sqrt(3 / nearby enemies) of it. Its target's hit
    /// stays whole.
    ///
    /// The count leaves the target out: in combat logs, each cleave hit on 26 other enemies is 2.874 / 7.39 x sqrt(3/26) of the target's.
    /// </summary>
    [Theory]
    [InlineData("Gunde", 4)]
    [InlineData("Gunde", 6)]
    [InlineData("Mara", 4)]
    [InlineData("Mara", 6)]
    [InlineData("Tariq", 4)]
    [InlineData("Tariq", 6)]
    public void Fated_Strike_Cleaves_Nearby_Enemies_For_287_4_Percent_Reduced_Beyond_3(string hero, int enemies)
    {
        var f = StepFights.Of(hero, enemies: enemies, weaponAbilityId: FatedStrike.AbilityId).WithoutDamageSpread();

        f.Cast<FatedStrike>();

        var hits = f.FirstHitOnEachEnemy("Fated Strike");
        int nearby = enemies - 1;

        using var check = new Checks(f);
        check.Equal(enemies, hits.Count);
        foreach (double cleave in hits.Skip(1))
        {
            check.Ratio(2.874 / 7.39 * Math.Min(1, Math.Sqrt(3.0 / nearby)), cleave / hits[0]);
        }
    }

    // ---- Glorious Purpose

    /// <summary>Fated Strike gives you Glorious Purpose for 6 seconds.</summary>
    [Theory]
    [InlineData("Gunde")]
    [InlineData("Mara")]
    [InlineData("Tariq")]
    public void Fated_Strike_Gives_Glorious_Purpose_For_6_Seconds(string hero)
    {
        var f = StepFights.Of(hero, weaponAbilityId: FatedStrike.AbilityId);

        f.Cast<FatedStrike>();
        double start = f.CompletedAt("Fated Strike");

        f.WaitUntil(start + 6 - 0.1);
        Assert.True(f.Has(FatedStrike.GloriousPurpose));
        f.WaitUntil(start + 6 + 0.1);
        Assert.False(f.Has(FatedStrike.GloriousPurpose));
    }

    /// <summary>
    /// Glorious Purpose gives +200% cooldown acceleration: your other cooldowns recover 3 times as fast while it lasts.
    ///
    /// Each hero's major ability, cast right after Fated Strike, is the other cooldown.
    /// </summary>
    [Theory]
    [InlineData("Gunde", "Slaughter", 30.0)]
    [InlineData("Mara", "Maiden of Death", 60.0)]
    [InlineData("Tariq", "Thunder Call", 45.0)]
    public void Glorious_Purpose_Makes_Other_Cooldowns_Recover_3_Times_As_Fast(string hero, string major, double cooldown)
    {
        var f = StepFights.Of(hero, weaponAbilityId: FatedStrike.AbilityId);
        f.FillResources(); // enough to cast it

        f.Cast<FatedStrike>();
        double purposeEnd = f.CompletedAt("Fated Strike") + 6;
        f.Cast(major);
        double start = f.CompletedAt(major);
        double end = purposeEnd + cooldown - 3 * (purposeEnd - start);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(major));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(major));
    }
}
