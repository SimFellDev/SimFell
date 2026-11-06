namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 705: "Your MAJOR abilities gain Cooldown Acceleration by rank. The bonus is increased for every percent of Haste
/// you have, up to a maximum" (<see cref="HasteBonusPerPercent"/>, <see cref="MaxHasteBonus"/>).
/// </summary>
public class TheMonarch : Glyph
{
    public const int GlyphId = 705;
    public const string GlyphName = "The Monarch";
    public const double HasteBonusPerPercent = 0.001;
    public const double MaxHasteBonus = 0.05;
    public static readonly double[] AccelerationByRank = [0.03, 0.05, 0.08, 0.12];

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        double acceleration = ForRank(AccelerationByRank);
        hero.CooldownAccelerations.Add((ability, now) =>
            ability.Category == AbilityCategory.Major
                ? acceleration + Math.Min(MaxHasteBonus, HasteBonusPerPercent * hero.GetHaste(now) * 100.0)
                : 0.0);
    }
}
