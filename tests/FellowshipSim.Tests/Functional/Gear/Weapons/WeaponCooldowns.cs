using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The cooldowns of the weapon abilities, and which can be cast during the global cooldown, over every hero that can have each weapon: Rime, Ardeos and Elarion have Chronoshift, Icicles
/// of An'zhyr, Voidbringer's Touch and Nature's Fury; Gunde, Mara and Tariq have Fated Strike, Sahril's Wrath and Voidbringer's Touch.
///
/// What each weapon ability does is in its own file (WeaponChronoshift.cs...).
/// </summary>
public class WeaponCooldowns
{
    /// <summary>
    /// A weapon ability's cooldown is fixed: haste does not shorten it, and neither does the weapon's own cooldown recovery
    /// (Chronoshift's barrier, Fated Strike's Glorious Purpose), which speeds up your other cooldowns only.
    /// </summary>
    [Theory]
    [InlineData("Rime", Chronoshift.AbilityId, "Chronoshift", 180.0)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0)]
    [InlineData("Rime", VoidbringersTouch.AbilityId, "Voidbringer's Touch", 90.0)]
    [InlineData("Rime", NaturesFury.AbilityId, "Nature's Fury", 60.0)]
    [InlineData("Ardeos", Chronoshift.AbilityId, "Chronoshift", 180.0)]
    [InlineData("Ardeos", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0)]
    [InlineData("Ardeos", VoidbringersTouch.AbilityId, "Voidbringer's Touch", 90.0)]
    [InlineData("Ardeos", NaturesFury.AbilityId, "Nature's Fury", 60.0)]
    [InlineData("Elarion", Chronoshift.AbilityId, "Chronoshift", 180.0)]
    [InlineData("Elarion", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0)]
    [InlineData("Elarion", VoidbringersTouch.AbilityId, "Voidbringer's Touch", 90.0)]
    [InlineData("Elarion", NaturesFury.AbilityId, "Nature's Fury", 60.0)]
    [InlineData("Gunde", FatedStrike.AbilityId, "Fated Strike", 60.0)]
    [InlineData("Gunde", SahrilsWrath.AbilityId, "Sahril's Wrath", 120.0)]
    [InlineData("Gunde", VoidbringersTouch.AbilityId, "Voidbringer's Touch", 90.0)]
    [InlineData("Mara", FatedStrike.AbilityId, "Fated Strike", 60.0)]
    [InlineData("Mara", SahrilsWrath.AbilityId, "Sahril's Wrath", 120.0)]
    [InlineData("Mara", VoidbringersTouch.AbilityId, "Voidbringer's Touch", 90.0)]
    [InlineData("Tariq", FatedStrike.AbilityId, "Fated Strike", 60.0)]
    [InlineData("Tariq", SahrilsWrath.AbilityId, "Sahril's Wrath", 120.0)]
    [InlineData("Tariq", VoidbringersTouch.AbilityId, "Voidbringer's Touch", 90.0)]
    public void A_Weapon_Abilitys_Cooldown_Is_Not_Shortened_By_Haste_Or_Cooldown_Recovery(string hero, int weaponId, string weapon,
        double cooldown)
    {
        var f = StepFights.Of(hero, haste: 0.5, weaponAbilityId: weaponId);

        f.Cast(weapon);
        double end = f.CompletedAt(weapon) + cooldown;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(weapon));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(weapon));
    }

    /// <summary>
    /// Blessing of the Commander shortens your weapon ability's cooldown too: by 12% at 1500 Emerald gem power.
    ///
    /// Each weapon ability is read once, on one of its heroes. How much it shortens at each gem power is the gem's own rule, in GemEmerald.cs.
    /// </summary>
    [Theory]
    [InlineData("Rime", Chronoshift.AbilityId, "Chronoshift", 180.0)]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0)]
    [InlineData("Rime", VoidbringersTouch.AbilityId, "Voidbringer's Touch", 90.0)]
    [InlineData("Rime", NaturesFury.AbilityId, "Nature's Fury", 60.0)]
    [InlineData("Gunde", FatedStrike.AbilityId, "Fated Strike", 60.0)]
    [InlineData("Gunde", SahrilsWrath.AbilityId, "Sahril's Wrath", 120.0)]
    public void Blessing_Of_The_Commander_Shortens_A_Weapon_Abilitys_Cooldown(string hero, int weaponId, string weapon, double cooldown)
    {
        var f = StepFights.Of(hero, weaponAbilityId: weaponId, gemPower: new() { [GemColor.Emerald] = 1500 });

        f.Cast(weapon);
        double end = f.CompletedAt(weapon) + cooldown * (1 - 0.12);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(weapon));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(weapon));
    }

    /// <summary>
    /// Icicles of An'zhyr, Voidbringer's Touch and Sahril's Wrath can be cast during the global cooldown ("Can be cast during Global
    /// Cooldown").
    ///
    /// Each hero's instant ability starts the global cooldown.
    /// </summary>
    [Theory]
    [InlineData("Rime", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", "Ice Comet")]
    [InlineData("Ardeos", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", "Searing Blaze")]
    [InlineData("Elarion", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", "Multishot")]
    [InlineData("Rime", VoidbringersTouch.AbilityId, "Voidbringer's Touch", "Ice Comet")]
    [InlineData("Ardeos", VoidbringersTouch.AbilityId, "Voidbringer's Touch", "Searing Blaze")]
    [InlineData("Elarion", VoidbringersTouch.AbilityId, "Voidbringer's Touch", "Multishot")]
    [InlineData("Gunde", VoidbringersTouch.AbilityId, "Voidbringer's Touch", "Double Strike")]
    [InlineData("Mara", VoidbringersTouch.AbilityId, "Voidbringer's Touch", "Skittering Blades")]
    [InlineData("Tariq", VoidbringersTouch.AbilityId, "Voidbringer's Touch", "Heavy Strike")]
    [InlineData("Gunde", SahrilsWrath.AbilityId, "Sahril's Wrath", "Double Strike")]
    [InlineData("Mara", SahrilsWrath.AbilityId, "Sahril's Wrath", "Skittering Blades")]
    [InlineData("Tariq", SahrilsWrath.AbilityId, "Sahril's Wrath", "Heavy Strike")]
    public void A_Weapon_Ability_Can_Be_Cast_During_The_Global_Cooldown(string hero, int weaponId, string weapon, string instant)
    {
        var f = StepFights.Of(hero, weaponAbilityId: weaponId);
        f.FillResources(); // enough to cast it

        // returns at once, with the global cooldown running
        f.StartCast(f.Ability(instant));

        using var check = new Checks(f);
        check.False(f.IsFree);
        check.True(f.TryCast(weapon).Succeeded);
    }
}
