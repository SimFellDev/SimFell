namespace FellowshipSim.Core.Customization.Weapons;

/// <summary>
/// A weapon. Weapons are identified by their weapon ability (combat logs list it in the weapon tree), which
/// <see cref="Equip"/> gives the hero.
/// </summary>
public abstract class Weapon
{
    /// <summary>The weapon ability's game ID.</summary>
    public abstract int WeaponAbilityId { get; }
    public abstract string Name { get; }

    /// <summary>The weapon ability's icon URL from the game data (the generated <c>WeaponPowerAbilityIcon</c>), or null.</summary>
    public virtual string? Icon => null;

    public virtual Ability? Ability { get; protected set; }

    public virtual void Equip(Hero hero)
    {
        hero.EquippedWeapon = this;
        if (Ability != null)
        {
            hero.WeaponAbility = Ability;
            if (!hero.Abilities.Any(a => a.Name.Equals(Ability.Name, StringComparison.OrdinalIgnoreCase)))
            {
                hero.Abilities.Add(Ability);
            }
        }
    }
}
