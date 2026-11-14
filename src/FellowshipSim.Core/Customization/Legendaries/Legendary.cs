namespace FellowshipSim.Core.Customization.Legendaries;

public abstract class Legendary
{
    /// <summary>The legendary power's game ID.</summary>
    public abstract int LegendaryPowerId { get; }
    public abstract string Name { get; }

    /// <summary>The hero whose legendary items grant this power.</summary>
    public abstract string HeroName { get; }

    /// <summary>The item IDs that grant it (the same item ID grants a different power on each hero).</summary>
    public abstract IReadOnlyList<int> ItemIds { get; }

    /// <summary>
    /// The legendary's icon URL from the game data: its first item's icon, else its power's (the generated
    /// <c>&lt;Slot&gt;ItemIcon</c> or <c>PowerIcon</c>); null for neither.
    /// </summary>
    public virtual string? Icon => null;

    /// <summary>False for legendaries the engine does not model yet (reported, no effect).</summary>
    public virtual bool IsModeled => true;

    public abstract void Bind(Hero hero);
}

