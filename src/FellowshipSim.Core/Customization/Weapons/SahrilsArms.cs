using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Sahril's Arms (item 5981; Forgefather's Axes, item 5982, and the other Gunde, Mara and Tariq weapons with weapon power
/// 351): its weapon ability is Sahril's Wrath.
/// </summary>
public class SahrilsArms : Weapon
{
    public const string WeaponName = "Sahril's Arms";

    public override int WeaponAbilityId => SahrilsWrath.AbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new SahrilsWrath(hero);
        base.Equip(hero);
    }
}
