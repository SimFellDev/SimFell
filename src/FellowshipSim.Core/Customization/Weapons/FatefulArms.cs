using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>Fateful Arms (Gunde's version, item 5327; Fateful Basher and the other Strength heroes' versions too): its weapon ability is Fated Strike.</summary>
public class FatefulArms : Weapon
{
    public const string WeaponName = "Fateful Arms";

    public override int WeaponAbilityId => FatedStrike.AbilityId;
    public override string Name => WeaponName;

    public override void Equip(Hero hero)
    {
        Ability = new FatedStrike(hero);
        base.Equip(hero);
    }
}
