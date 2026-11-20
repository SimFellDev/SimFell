namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Weapon power 351, whose weapon ability is Sahril's Wrath. Item 5982 grants it: Forgefather's Axes (Gunde), Sahril's
/// Vengeance (Mara) and Sahril's Reckoning (Tariq).
/// </summary>
public partial class SahrilsWrath : Weapon
{
    public const string WeaponName = "Sahril's Wrath";

    public override int WeaponAbilityId => WeaponPowerAbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new WeaponAbilities.SahrilsWrath(hero);
        base.Equip(hero);
    }
}
