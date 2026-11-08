namespace FellowshipSim.Core.Domain;

/// <summary>
/// A creature placed in a dungeon of the game data: the creature's game ID (the <c>ID</c> of its <c>&lt;Name&gt;GameCreature</c>
/// class), a new instance of that class for each call of <see cref="NewCreature"/>, its world position's x and y, and the
/// index of the patrol path it walks (null for none).
/// </summary>
public sealed record CodexPlacement(int CreatureId, Func<GameCreature> NewCreature, double X, double Y, int? Patrol);
