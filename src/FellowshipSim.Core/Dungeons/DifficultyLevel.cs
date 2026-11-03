namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// One difficulty a dungeon runs at: <see cref="Eternal"/> is its Eternal level (0 for Base), <see cref="Difficulty"/> the
/// game's difficulty number, and <see cref="Health"/> and <see cref="Damage"/> multiply a creature's game data health and
/// damage.
/// </summary>
public sealed record DifficultyLevel(int Eternal, int Difficulty, string Label, double Health, double Damage);
