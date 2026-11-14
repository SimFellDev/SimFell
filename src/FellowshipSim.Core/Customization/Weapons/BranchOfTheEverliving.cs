using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>Branch of the Everliving (Rime's version, item 5982): its weapon ability is Nature's Fury.</summary>
public partial class BranchOfTheEverliving : Weapon
{
    public const string WeaponName = "Branch of the Everliving";

    public override int WeaponAbilityId => WeaponPowerAbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new NaturesFury(hero);
        base.Equip(hero);
    }
}
