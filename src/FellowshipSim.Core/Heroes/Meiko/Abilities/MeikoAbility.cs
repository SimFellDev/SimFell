namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

// Each ability lists its game values as constants at the top (IDs and numbers from the game data).

/// <summary>Base for Meiko's abilities.</summary>
public abstract class MeikoAbility : Ability
{
    protected MeikoAbility(Meiko owner, int id, string name, AbilityCategory category = AbilityCategory.None)
        : base(owner, id, name, category) { }

    protected Meiko Meiko => (Meiko)Owner;
}
