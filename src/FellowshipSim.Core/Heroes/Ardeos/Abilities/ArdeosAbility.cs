namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

// Each ability lists its game values as constants at the top (IDs and numbers from the game data).

/// <summary>Base for Ardeos's abilities.</summary>
public abstract class ArdeosAbility : Ability
{
    protected ArdeosAbility(Ardeos owner, int id, string name, AbilityCategory category = AbilityCategory.None)
        : base(owner, id, name, category) { }

    protected Ardeos Ardeos => (Ardeos)Owner;
}
