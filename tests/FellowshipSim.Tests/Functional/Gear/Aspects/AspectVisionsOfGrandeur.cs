using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Visions of Grandeur (aspect 47): "Your Weapon Ability's Cooldown is reduced by 25% / 50% / 75% / 100% when you activate your Spirit
/// Ability" (ranks 1 to 4), of its default cooldown, and "Your Weapon Ability generates 2.5 Spirit Points multiplied by a value equal to
/// your Weapon Ability's default cooldown, divided by 30" (combat logs: 15 Spirit Points per Chronoshift, 180 seconds).
/// </summary>
public class AspectVisionsOfGrandeur
{
    /// <summary>
    /// A hero with <paramref name="weaponId"/> and the aspect at <paramref name="rank"/> (none at 0), with no Spirit Points.
    /// </summary>
    private static StepDriver Fight(string hero, int weaponId, int rank)
    {
        var f = StepFights.Of(hero, weaponAbilityId: weaponId, augmentations: StepFights.Ranked(VisionsOfGrandeur.AspectId, rank));
        f.Set(Hero.SpiritPoints, 0); // room for the weapon's Spirit Points
        return f;
    }

    /// <summary>
    /// Your weapon ability gives 2.5 Spirit Points per 30 seconds of its default cooldown: 5 for Fated Strike, 15 for Chronoshift.
    /// </summary>
    [Theory]
    [InlineData("Rime", Chronoshift.AbilityId, "Chronoshift", 180.0)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0)]
    [InlineData("Rime", VoidbringersTouch.AbilityId, "Voidbringer's Touch", 90.0)]
    [InlineData("Rime", NaturesFury.AbilityId, "Nature's Fury", 60.0)]
    [InlineData("Gunde", FatedStrike.AbilityId, "Fated Strike", 60.0)]
    [InlineData("Gunde", SahrilsWrath.AbilityId, "Sahril's Wrath", 120.0)]
    public void Visions_Of_Grandeur_Weapon_Ability_Gives_Spirit_Points_By_Its_Cooldown(string hero, int weaponId, string weapon,
        double cooldown)
    {
        var plainScenario = Fight(hero, weaponId, rank: 0);
        var visionsScenario = Fight(hero, weaponId, rank: 1);

        plainScenario.Cast(weapon);
        visionsScenario.Cast(weapon);

        Assert.Equal(2.5 * cooldown / 30, visionsScenario.Amount(Hero.SpiritPoints) - plainScenario.Amount(Hero.SpiritPoints), 1e-9);
    }

    /// <summary>Only your weapon ability gives Visions of Grandeur's Spirit Points.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Ice Blitz")]
    public void Only_The_Weapon_Ability_Gives_Visions_Of_Grandeurs_Spirit_Points(string ability)
    {
        var plainScenario = Fight("Rime", Chronoshift.AbilityId, rank: 0);
        var visionsScenario = Fight("Rime", Chronoshift.AbilityId, rank: 1);

        plainScenario.Cast(ability);
        visionsScenario.Cast(ability);

        Assert.Equal(plainScenario.Amount(Hero.SpiritPoints), visionsScenario.Amount(Hero.SpiritPoints), 1e-9);
    }

    /// <summary>Your ultimate takes 25% / 50% / 75% of your weapon ability's default cooldown off its cooldown.</summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 1, 0.25)]
    [InlineData("Rime", "Wrath of Winter", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 2, 0.50)]
    [InlineData("Rime", "Wrath of Winter", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 3, 0.75)]
    [InlineData("Ardeos", "Incinerate", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 1, 0.25)]
    [InlineData("Elarion", "Event Horizon", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 1, 0.25)]
    [InlineData("Gunde", "Bloodbound Spirit", FatedStrike.AbilityId, "Fated Strike", 60.0, 1, 0.25)]
    [InlineData("Mara", "Matriarch Macabre", FatedStrike.AbilityId, "Fated Strike", 60.0, 1, 0.25)]
    [InlineData("Tariq", "Raging Tempest", FatedStrike.AbilityId, "Fated Strike", 60.0, 1, 0.25)]
    public void Visions_Of_Grandeur_Ultimate_Takes_A_Share_Off_The_Weapon_Abilitys_Cooldown(string hero, string ultimate, int weaponId,
        string weapon, double cooldown, int rank, double share)
    {
        var f = Fight(hero, weaponId, rank);

        f.Cast(weapon);
        f.FillResources(); // enough to cast it
        f.Cast(ultimate);
        double end = f.CompletedAt(weapon) + cooldown * (1 - share);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(weapon));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(weapon));
    }

    /// <summary>At rank 4 your ultimate takes the whole default cooldown off: the weapon ability is ready at once.</summary>
    [Fact]
    public void Visions_Of_Grandeur_Rank_4_Ultimate_Makes_The_Weapon_Ability_Ready()
    {
        var f = Fight("Rime", Chronoshift.AbilityId, rank: 4);

        f.Cast("Chronoshift");
        f.FillResources(); // enough to cast it
        f.Cast("Wrath of Winter");

        Assert.True(f.IsReady("Chronoshift"));
    }
}
