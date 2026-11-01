using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>Zeraleth's Arms (Helena's version, item 5983; the other heroes' weapons with weapon power 358 too): its weapon ability is Zeraleth's Hunger.</summary>
public class ZeralethsArms : Weapon
{
    public const string WeaponName = "Zeraleth's Arms";

    public override int WeaponAbilityId => ZeralethsHunger.AbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new ZeralethsHunger(hero);
        base.Equip(hero);
    }
}
