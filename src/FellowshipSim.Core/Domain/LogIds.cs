namespace FellowshipSim.Core.Domain;

/// <summary>
/// How combat logs number game things: abilities by their game ID, and effects (auras, effect damage), talents and augmentations
/// by their game ID plus an offset. (Glyphs are logged with per-item IDs and are matched by name.)
/// </summary>
public static class LogIds
{
    public const int EffectOffset = 1_000_000;
    public const int TalentOffset = 2_000_000;
    public const int AugmentationOffset = 3_000_000;

    /// <summary>The log ID of a game effect.</summary>
    public static int Effect(int effectId) => effectId + EffectOffset;
}
