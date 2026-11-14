using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Weapon power 358, whose weapon ability is Zeraleth's Hunger. Item 5983 grants it: Zeraleth's Arms (Helena), Zeraleth's
/// Claws (Meiko) and Zaraleth's Mournfang (Xavian). Item 5984 grants it too: The Devourer’s Hour (Aeona), Zeraleth's Scythe
/// (Sylvie) and Zeraleth's Compendium (Vigour).
/// </summary>
public partial class ZeralethsArms : Weapon
{
    public const string WeaponName = "Zeraleth's Arms";

    public override int WeaponAbilityId => WeaponPowerAbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new ZeralethsHunger(hero);
        base.Equip(hero);
    }
}
