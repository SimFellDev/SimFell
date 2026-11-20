namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 705: "Your MAJOR abilities gain Cooldown Acceleration by rank. The bonus is increased for every percent of Haste
/// you have, up to a maximum" (<see cref="HasteBonusPerPercent"/>, <see cref="MaxHasteBonus"/>).
/// </summary>
public partial class TheMonarch : Glyph
{
    /// <summary>
    /// Tooltip: "increased by +0.1% for every 1% Haste" as a fraction per percent of Haste (the game data's
    /// <see cref="Constants.BonusIncreasePerHasteInPercentage"/> is 0.1, in percent).
    /// </summary>
    public const double HasteBonusPerPercent = 0.001;
    /// <summary>
    /// Tooltip: "up to a maximum of 5%" (the game data has <see cref="Constants.HasteThreshold"/>, the Haste at which the bonus stops
    /// growing).
    /// </summary>
    public const double MaxHasteBonus = 0.05;

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        double acceleration = ForRank(Constants.CooldownAccelerationByRank);
        hero.CooldownAccelerations.Add((ability, now) =>
            ability.Category == AbilityCategory.Major
                ? acceleration + Math.Min(MaxHasteBonus, HasteBonusPerPercent * hero.GetHaste(now) * 100.0)
                : 0.0);
    }
}
