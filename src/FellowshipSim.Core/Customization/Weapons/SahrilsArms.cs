using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Sahril's Arms (the Gunde, Mara and Tariq weapons with weapon power 351: Forgefather's Axes, Sahril's Vengeance, and
/// Sahril's Reckoning, item 5982): its weapon ability is Sahril's Wrath. Item 5981 on a tank has weapon power 352,
/// Sahril's Aegis (<see cref="SahrilsGreatsword"/>).
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
