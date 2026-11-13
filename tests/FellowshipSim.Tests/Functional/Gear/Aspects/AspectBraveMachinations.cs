using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Brave Machinations (aspect 48): "Your Weapon Ability's Critical Strike chance is increased by 9.5% / 17% / 24% / 32%. If your Weapon
/// Ability deals a Critical Strike, its Cooldown is reduced by 15% / 20% / 25% / 30%" (ranks 1 to 4), of its default cooldown, once per
/// cast. The bare hero has no crit chance.
/// </summary>
public class AspectBraveMachinations
{
    /// <summary>Brave Machinations raises your weapon ability's crit chance by 9.5% / 17% / 24% / 32%.</summary>
    [Theory]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 1, 0.095)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 2, 0.17)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 3, 0.24)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 4, 0.32)]
    [InlineData("Ardeos", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 4, 0.32)]
    [InlineData("Elarion", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 4, 0.32)]
    [InlineData("Gunde", FatedStrike.AbilityId, "Fated Strike", 4, 0.32)]
    [InlineData("Mara", FatedStrike.AbilityId, "Fated Strike", 4, 0.32)]
    [InlineData("Tariq", FatedStrike.AbilityId, "Fated Strike", 4, 0.32)]
    public void Brave_Machinations_Raises_The_Weapon_Abilitys_Crit_Chance(string hero, int weaponId, string weapon, int rank,
        double critChance)
    {
        var f = StepFights.Of(hero, weaponAbilityId: weaponId, augmentations: [new(BraveMachinations.AspectId, rank)]);

        f.Cast(weapon, crit: false);
        f.Wait(3); // the hits land

        Assert.Equal(critChance, f.Damage(weapon)[0].CritChance, 1e-9);
    }

    /// <summary>
    /// A critical strike of your weapon ability takes 15% / 20% / 25% / 30% of its default cooldown off it; a weapon cast that does not
    /// crit takes nothing off.
    /// </summary>
    [Theory]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 1, 0.15, true)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 2, 0.20, true)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 3, 0.25, true)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 0.30, true)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 0.30, false)]
    [InlineData("Ardeos", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 0.30, true)]
    [InlineData("Elarion", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 0.30, true)]
    [InlineData("Gunde", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 0.30, true)]
    [InlineData("Mara", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 0.30, true)]
    [InlineData("Tariq", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 0.30, true)]
    public void A_Weapon_Critical_Strike_Takes_A_Share_Of_Its_Cooldown_Off(string hero, int weaponId, string weapon, double cooldown,
        int rank, double cut, bool crit)
    {
        var f = StepFights.Of(hero, weaponAbilityId: weaponId, augmentations: [new(BraveMachinations.AspectId, rank)]);

        f.Cast(weapon, crit: crit);
        double end = f.CompletedAt(weapon) + cooldown * (1 - (crit ? cut : 0));

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(weapon));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(weapon));
    }

    /// <summary>
    /// The cooldown comes off once per cast, however many hits crit: Icicles of An'zhyr's crits on 3 enemies take 30% off, once.
    /// </summary>
    [Fact]
    public void Brave_Machinations_Takes_The_Cooldown_Off_Once_Per_Cast()
    {
        var f = StepFights.Of("Rime", enemies: 3, weaponAbilityId: IciclesOfAnzhyr.AbilityId,
            augmentations: [new(BraveMachinations.AspectId, 4)]);

        f.Cast("Icicles of An'zhyr", crit: true);
        double end = f.CompletedAt("Icicles of An'zhyr") + 40 * (1 - 0.30);

        f.WaitUntil(end - 0.1);
        using var check = new Checks(f);
        check.Equal(9, f.Damage("Icicles of An'zhyr").Count(hit => hit.IsCrit), what: "crits");
        check.False(f.IsReady("Icicles of An'zhyr"));
        f.WaitUntil(end + 0.1);
        check.True(f.IsReady("Icicles of An'zhyr"));
    }

    /// <summary>
    /// Voidbringer's Touch always crits, so with Brave Machinations its eruption always takes 15% / 20% / 25% / 30% of its cooldown off.
    /// </summary>
    [Fact]
    public void Voidbringers_Touch_Always_Takes_Brave_Machinations_Cooldown_Off()
    {
        var f = StepFights.Of("Rime", weaponAbilityId: VoidbringersTouch.AbilityId, augmentations: [new(BraveMachinations.AspectId, 4)]);

        f.Cast<VoidbringersTouch>();
        f.Cast("Frost Bolt"); // damage to store
        double end = f.CompletedAt("Voidbringer's Touch") + 90 * (1 - 0.30);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady("Voidbringer's Touch"));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady("Voidbringer's Touch"));
    }

    /// <summary>
    /// A critical strike of Icicles of An'zhyr's curse, its damage over time, takes nothing off the cooldown.
    /// </summary>
    [Fact]
    public void A_Critical_Strike_Of_The_Icicles_Curse_Takes_Nothing_Off()
    {
        var f = StepFights.Of("Rime", crit: 1.0, weaponAbilityId: IciclesOfAnzhyr.AbilityId,
            augmentations: [new(BraveMachinations.AspectId, 4)]);

        f.Cast<IciclesOfAnzhyr>(crit: false);
        double end = f.CompletedAt("Icicles of An'zhyr") + 40;

        using var check = new Checks(f);
        f.WaitUntil(end - 0.1);
        check.True(f.Damage("Curse of An'zhyr").Any(tick => tick.IsCrit));
        check.False(f.IsReady("Icicles of An'zhyr"));
        f.WaitUntil(end + 0.1);
        check.True(f.IsReady("Icicles of An'zhyr"));
    }
}
