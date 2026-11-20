namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Weapon power 352, whose weapon ability is Sahril's Aegis. Item 5981 grants it: Sahril's Arms (Helena), Sahril's Grasp
/// (Meiko) and Sahril's Greatsword (Xavian).
/// </summary>
public partial class SahrilsAegis : Weapon
{
    public const string WeaponName = "Sahril's Aegis";

    public override int WeaponAbilityId => WeaponPowerAbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new WeaponAbilities.SahrilsAegis(hero);
        base.Equip(hero);
    }
}
