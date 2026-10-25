namespace FellowshipSim.UI.Build;

/// <summary>
/// One choice of a build: a talent, weapon, legendary, relic passive, augmentation or glyph, by the ID the build keeps (a
/// weapon by its ability, a legendary by its item). <see cref="Modeled"/> is false for a choice the
/// engine has no effect for; <see cref="Note"/> says what part of it is missing.
/// </summary>
public sealed record BuildOption(int Id, string Name, string? Icon, bool Modeled = true, int MaxRank = 1, string? Note = null)
{
    /// <summary>The icon's image on the Codex CDN, or null when the datamine has no icon for it.</summary>
    public string? IconUrl => Icon == null ? null : GameIcons.Url(Icon);
}
