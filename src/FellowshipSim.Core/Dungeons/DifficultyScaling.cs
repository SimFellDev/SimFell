namespace FellowshipSim.Core.Dungeons;

/// <summary>The difficulties a dungeon runs at, with each one's creature health and damage scale.</summary>
public static class DifficultyScaling
{
    /// <summary>
    /// Base and Eternal 1 to 32. Source: game data settings <c>difficultyScaling</c>, build 26301323 (each difficulty has the
    /// health and damage of the <c>CT_CharacterScalingBaseAttributes</c> key one above it). Combat logs: Deathless Katrine has
    /// 6,581,643 health at Eternal 27, her game data health 29,075.305 times 226.36.
    /// </summary>
    public static readonly IReadOnlyList<DifficultyLevel> Levels =
    [
        new(0, 1, "Base", 1.31, 1.31),
        new(1, 21, "Eternal 1", 32.5, 32.5),
        new(2, 22, "Eternal 2", 34.68, 34.68),
        new(3, 23, "Eternal 3", 37, 37),
        new(4, 24, "Eternal 4", 39.48, 39.48),
        new(5, 25, "Eternal 5", 42.13, 42.13),
        new(6, 26, "Eternal 6", 44.95, 44.95),
        new(7, 27, "Eternal 7", 47.96, 47.96),
        new(8, 28, "Eternal 8", 51.17, 51.17),
        new(9, 29, "Eternal 9", 54.6, 54.6),
        new(10, 30, "Eternal 10", 58.26, 58.26),
        new(11, 31, "Eternal 11", 62.16, 62.16),
        new(12, 32, "Eternal 12", 66.33, 66.33),
        new(13, 33, "Eternal 13", 70.77, 70.77),
        new(14, 34, "Eternal 14", 75.51, 75.51),
        new(15, 35, "Eternal 15", 80.57, 80.57),
        new(16, 36, "Eternal 16", 85.97, 85.97),
        new(17, 37, "Eternal 17", 93.88, 91.73),
        new(18, 38, "Eternal 18", 102.52, 97.88),
        new(19, 39, "Eternal 19", 111.95, 104.43),
        new(20, 40, "Eternal 20", 122.25, 111.43),
        new(21, 41, "Eternal 21", 133.49, 118.9),
        new(22, 42, "Eternal 22", 145.78, 126.86),
        new(23, 43, "Eternal 23", 159.19, 135.36),
        new(24, 44, "Eternal 24", 173.83, 144.43),
        new(25, 45, "Eternal 25", 189.82, 154.11),
        new(26, 46, "Eternal 26", 207.29, 164.43),
        new(27, 47, "Eternal 27", 226.36, 175.45),
        new(28, 48, "Eternal 28", 247.18, 187.21),
        new(29, 49, "Eternal 29", 269.93, 199.75),
        new(30, 50, "Eternal 30", 294.76, 213.13),
        new(31, 51, "Eternal 31", 321.88, 227.41),
        new(32, 52, "Eternal 32", 351.49, 242.65),
    ];

    /// <summary>The Base difficulty.</summary>
    public static DifficultyLevel Base => Levels[0];

    /// <summary>The difficulty of Eternal level <paramref name="eternal"/> (0 for Base); null when there is none.</summary>
    public static DifficultyLevel? At(int eternal) => Levels.FirstOrDefault(l => l.Eternal == eternal);

    /// <summary>
    /// The difficulty of the game's difficulty number <paramref name="difficulty"/> (a fight's <c>difficultyLevel</c> in
    /// FellowshipLogs: 50 for Eternal 30); null when there is none.
    /// </summary>
    public static DifficultyLevel? WithDifficulty(int difficulty) => Levels.FirstOrDefault(l => l.Difficulty == difficulty);
}
