using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Martial Initiative (aspect 51): "Your Weapon Ability grants you 10% increased Main Stat and 15% Damage Reduction for a duration equal to
/// 10% / 18% / 25% / 32% of your Weapon Ability's default cooldown" (ranks 1 to 4). The Damage Reduction is not tested: the sim takes no
/// damage.
/// </summary>
public class AspectMartialInitiative
{
    /// <summary>
    /// Your weapon ability gives you 10% more main stat, for 10% / 18% / 25% / 32% of its default cooldown: 4 seconds of Icicles of
    /// An'zhyr's 40 at rank 1.
    /// </summary>
    [Theory]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 1, 0.10)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 2, 0.18)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 3, 0.25)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 0.32)]
    [InlineData("Ardeos", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 0.32)]
    [InlineData("Elarion", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 0.32)]
    [InlineData("Gunde", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 0.32)]
    [InlineData("Mara", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 0.32)]
    [InlineData("Tariq", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 0.32)]
    public void Martial_Initiative_Weapon_Ability_Gives_10_Percent_Main_Stat_For_A_Share_Of_Its_Cooldown(string hero, int weaponId,
        string weapon, double cooldown, int rank, double share)
    {
        var f = StepFights.Of(hero, weaponAbilityId: weaponId, augmentations: [new(MartialInitiative.AspectId, rank)]);
        double plainMainStat = f.MainStat();

        f.Cast(weapon);
        double end = f.CompletedAt(weapon) + share * cooldown;

        using var check = new Checks(f);
        check.Ratio(1.1, f.MainStat() / plainMainStat);
        f.WaitUntil(end - 0.1);
        check.True(f.Has(MartialInitiative.Buff));
        f.WaitUntil(end + 0.1);
        check.False(f.Has(MartialInitiative.Buff));
    }

    /// <summary>Only your weapon ability gives Martial Initiative.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Ice Blitz")]
    [InlineData("Wrath of Winter")]
    public void Only_The_Weapon_Ability_Gives_Martial_Initiative(string ability)
    {
        var f = StepFights.Of("Rime", weaponAbilityId: IciclesOfAnzhyr.AbilityId, augmentations: [new(MartialInitiative.AspectId, 4)]);
        f.FillResources(); // enough to cast it

        f.Cast(ability);

        Assert.False(f.Has(MartialInitiative.Buff));
    }
}
