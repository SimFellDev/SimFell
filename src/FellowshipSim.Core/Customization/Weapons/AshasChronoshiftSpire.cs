using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>Asha's Chronoshift Spire: its weapon ability is Chronoshift.</summary>
public partial class AshasChronoshiftSpire : Weapon
{
    public const string WeaponName = "Asha's Chronoshift Spire";

    public override int WeaponAbilityId => WeaponPowerAbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new Chronoshift(hero);
        base.Equip(hero);
    }
}
