namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 709: "Your [MAJOR] ability grants you Haste, Expertise and Critical Strike for every step of Spirit you have
/// (by rank), up to a Spirit cap, for a duration." (effect 3189; <see cref="IncreasePerStep"/>, <see cref="SpiritCap"/>,
/// <see cref="DurationSeconds"/>)
/// </summary>
public class ThePhilosopher : Glyph
{
    public const int GlyphId = 709;
    public const string GlyphName = "The Philosopher";
    public const int BuffEffectId = 3189;
    public const double DurationSeconds = 8.0;
    public const double IncreasePerStep = 0.002;
    public const double SpiritCap = 0.5;
    /// <summary>Spirit percent per step of the bonus.</summary>
    public static readonly double[] SpiritPercentPerStepByRank = [4, 2.5, 1.6, 1];
    /// <summary>
    /// Not in the game data; measured from logs: a refresh keeps up to <see cref="RefreshCarryoverFraction"/> of the buff's duration from the time left, so a refreshed
    /// buff outlasts <see cref="DurationSeconds"/> (yMAZVJL9hTpFjqbC fight 7: a refresh with enough time left is removed later than a fresh buff).
    /// </summary>
    public const double RefreshCarryoverFraction = 0.3;

    public static readonly AuraDef Buff = AuraDef.Effect(GlyphName, BuffEffectId, DurationSeconds)
        with { RefreshCarryoverFraction = RefreshCarryoverFraction };

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        double spiritPerStep = ForRank(SpiritPercentPerStepByRank);
        // Applied once the MAJOR ability resolves, so its own effects (e.g. Winter's Blessing's Spirit) count.
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability.Category != AbilityCategory.Major) return;
            double spiritPercent = Math.Min(hero.SpiritPercent(now), SpiritCap) * 100.0;
            double bonus = IncreasePerStep * spiritPercent / spiritPerStep;
            hero.Auras.Apply(Buff.WithModifiers(
                StatModifier.Add(StatType.Haste, bonus),
                StatModifier.Add(StatType.Expertise, bonus),
                StatModifier.Add(StatType.CritChance, bonus)), now);
        };
    }
}
