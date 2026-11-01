using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>Sahril's Greatsword (item 5981 on Xavian; Sahril's Arms on Helena and Sahril's Grasp on Meiko, weapon power 352): its weapon ability is Sahril's Aegis.</summary>
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
