namespace FellowshipSim.UI.Build;

/// <summary>
/// One choice of a build: a talent, weapon, legendary, relic passive, augmentation or glyph, by the ID the build keeps (a
/// weapon by its ability, a legendary by its item), with its icon's image on the game data CDN (null when the game data has
/// no icon for it). <see cref="Modeled"/> is false for a choice the engine has no effect for; <see cref="Note"/> says what
/// part of it is missing.
/// </summary>
public sealed record BuildOption(int Id, string Name, string? IconUrl, bool Modeled = true, int MaxRank = 1, string? Note = null);
