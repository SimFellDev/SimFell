using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Weapon power 357, whose weapon ability is Icicles of An'zhyr. Item 5986 grants it: Frozen Staff of An'zhyr (Ardeos),
/// Frozen Longbow of An'zhyr (Elarion) and, under the name Void-touched Conduit, Rime's.
/// </summary>
public partial class FrozenStaffOfAnzhyr : Weapon
{
    public const string WeaponName = "Frozen Staff of An'zhyr";

    public override int WeaponAbilityId => WeaponPowerAbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new IciclesOfAnzhyr(hero);
        base.Equip(hero);
    }
}
