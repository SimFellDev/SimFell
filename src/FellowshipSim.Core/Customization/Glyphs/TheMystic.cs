namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 706: "Your POWER abilities gain increased power by rank for every step of Spirit you have, up to a Spirit cap"
/// (<see cref="SpiritPercentPerStep"/>, <see cref="SpiritCap"/>).
/// </summary>
public class TheMystic : Glyph
{
    public const int GlyphId = 706;
    public const string GlyphName = "The Mystic";
    public const double SpiritPercentPerStep = 3.0;
    public const double SpiritCap = 0.5;
    public static readonly double[] PowerPerStepByRank = [0.003, 0.005, 0.008, 0.012];

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        double perStep = ForRank(PowerPerStepByRank);
        hero.AbilityDamageMultipliers.Add((ability, now) =>
            ability.Category == AbilityCategory.Power
                ? 1.0 + perStep * (Math.Min(hero.SpiritPercent(now), SpiritCap) * 100.0 / SpiritPercentPerStep)
                : 1.0);
    }
}
