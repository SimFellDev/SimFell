using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>Darkfrost Arms (Helena's version; Darkfrost Talons and Darkfrost Drakecarver too, weapon power 348): its weapon ability is Al'zerac's Shackle.</summary>
public class DarkfrostArms : Weapon
{
    public const string WeaponName = "Darkfrost Arms";

    public override int WeaponAbilityId => AlzeracsShackle.AbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new AlzeracsShackle(hero);
        base.Equip(hero);
    }
}
