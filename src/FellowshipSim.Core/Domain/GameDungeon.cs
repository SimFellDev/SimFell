namespace FellowshipSim.Core.Domain;

/// <summary>
/// A dungeon of the game data. FellowshipSim.Codex.Generators writes a <c>&lt;Name&gt;GameDungeon</c> class for each dungeon
/// record, with its values, its creature placements (each with its creature's ID and a factory of its
/// <c>&lt;Name&gt;GameCreature</c> class) and these overrides, and the list of every dungeon (<see cref="All"/>).
/// A dungeon's mechanics go in a hand-written <c>partial</c> of its class. One instance of each class is in <see cref="All"/>,
/// so a dungeon class has no state of a fight.
/// </summary>
public abstract partial class GameDungeon
{
    /// <summary>The dungeon's ID: a FellowshipLogs fight's gameZone ID.</summary>
    public abstract int Id { get; }

    public abstract string Name { get; }

    public abstract IReadOnlyList<CodexPlacement> Placements { get; }
}
