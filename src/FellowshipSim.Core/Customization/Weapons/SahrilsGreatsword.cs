using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Weapon power 352, whose weapon ability is Sahril's Aegis. Item 5981 grants it: Sahril's Arms (Helena), Sahril's Grasp
/// (Meiko) and Sahril's Greatsword (Xavian).
/// </summary>
public class SahrilsGreatsword : Weapon
{
    public const string WeaponName = "Sahril's Greatsword";

    public override int WeaponAbilityId => SahrilsAegis.AbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new SahrilsAegis(hero);
        base.Equip(hero);
    }
}
