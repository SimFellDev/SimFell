using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Weapon power 348, whose weapon ability is Al'zerac's Shackle. Item 5987 grants it: Darkfrost Arms (Helena), Darkfrost
/// Talons (Meiko) and Darkfrost Drakecarver (Xavian).
/// </summary>
public partial class DarkfrostArms : Weapon
{
    public const string WeaponName = "Darkfrost Arms";

    public override int WeaponAbilityId => WeaponPowerAbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new AlzeracsShackle(hero);
        base.Equip(hero);
    }
}
