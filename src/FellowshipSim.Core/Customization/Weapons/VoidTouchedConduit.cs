using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// Weapon power 349, whose weapon ability is Voidbringer's Touch. Item 5980 grants it: Void-touched Conduit (Rime) and the
/// Void-touched weapons of Ardeos, Elarion, Mara and Tariq, and Voidbringer's Renders (Gunde). Rime's item 5986 has the
/// name Void-touched Conduit too and grants weapon power 357, so the class takes its weapon ability ID from Voidbringer's
/// Touch.
/// </summary>
public class VoidTouchedConduit : Weapon
{
    public const string WeaponName = "Void-touched Conduit";

    public override int WeaponAbilityId => VoidbringersTouch.AbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new VoidbringersTouch(hero);
        base.Equip(hero);
    }
}
