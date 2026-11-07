using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Inspired Allegiance (sigil 2): "Your abilities have a chance to reduce the Cooldown of your Weapon Ability by 1.5 / 4 / 6 / 8 seconds
/// and grant you and 1 / 2 / 3 / 3 allies 6 / 10 / 13 / 17 Haste rating for 8 seconds" (ranks 1 to 4). A rating point is 0.16%. The chance
/// is forced. The allies' share is not tested: the sim has no allies.
/// </summary>
public class SigilInspiredAllegiance
{
    /// <summary>Inspired Allegiance gives you 6 / 10 / 13 / 17 Haste rating for 8 seconds.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 6)]
    [InlineData("Rime", "Frost Bolt", 2, 10)]
    [InlineData("Rime", "Frost Bolt", 3, 13)]
    [InlineData("Rime", "Frost Bolt", 4, 17)]
    [InlineData("Ardeos", "Infernal Wave", 4, 17)]
    [InlineData("Elarion", "Focused Shot", 4, 17)]
    [InlineData("Gunde", "Double Strike", 4, 17)]
    [InlineData("Mara", "Skittering Blades", 4, 17)]
    [InlineData("Tariq", "Heavy Strike", 4, 17)]
    public void Inspired_Allegiance_Gives_Haste_Rating_For_8_Seconds(string hero, string ability, int rank, double rating)
    {
        var f = StepFights.Of(hero, augmentations: [new(InspiredAllegiance.SigilId, rank)]);
        f.ForceProcs(key => key == InspiredAllegiance.ProcKey ? true : null);

        f.Cast(ability);
        double start = f.CompletedAt(ability);

        using var check = new Checks(f);
        check.Equal(rating * 0.0016, f.Haste());
        f.WaitUntil(start + 8 - 0.1);
        check.True(f.Has(InspiredAllegiance.Buff));
        f.WaitUntil(start + 8 + 0.1);
        check.False(f.Has(InspiredAllegiance.Buff));
    }

    /// <summary>Inspired Allegiance takes 1.5 / 4 / 6 / 8 seconds off your weapon ability's cooldown.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 1, 1.5)]
    [InlineData("Rime", "Frost Bolt", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 2, 4.0)]
    [InlineData("Rime", "Frost Bolt", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 3, 6.0)]
    [InlineData("Rime", "Frost Bolt", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 8.0)]
    [InlineData("Ardeos", "Infernal Wave", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 8.0)]
    [InlineData("Elarion", "Focused Shot", IciclesOfAnzhyr.AbilityId, "Icicles of An'zhyr", 40.0, 4, 8.0)]
    [InlineData("Gunde", "Double Strike", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 8.0)]
    [InlineData("Mara", "Skittering Blades", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 8.0)]
    [InlineData("Tariq", "Heavy Strike", FatedStrike.AbilityId, "Fated Strike", 60.0, 4, 8.0)]
    public void Inspired_Allegiance_Shortens_The_Weapon_Abilitys_Cooldown(string hero, string ability, int weaponId, string weapon,
        double cooldown, int rank, double seconds)
    {
        var f = StepFights.Of(hero, weaponAbilityId: weaponId, augmentations: [new(InspiredAllegiance.SigilId, rank)]);
        bool armed = false;
        f.ForceProcs(key => key == InspiredAllegiance.ProcKey ? armed : null); // only the ability after the weapon procs
        f.FillResources(); // enough to cast them

        f.Cast(weapon);
        armed = true;
        f.Cast(ability);
        double end = f.CompletedAt(weapon) + cooldown - seconds;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(weapon));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(weapon));
    }
}
