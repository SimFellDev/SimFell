using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Weapon power 354, whose weapon ability is Fated Strike. Item 5985 grants it: Fateful Arms (Helena), Fists of Fate (Meiko)
/// and Fateful Dawnrender (Xavian). Item 5984 grants it too: Fateful Cleavers (Gunde), Fateful Blades (Mara) and Fateful
/// Basher (Tariq).
/// </summary>
public partial class FatefulArms : Weapon
{
    public const string WeaponName = "Fateful Arms";

    public override int WeaponAbilityId => WeaponPowerAbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new FatedStrike(hero);
        base.Equip(hero);
    }
}
