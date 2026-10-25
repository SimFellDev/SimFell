namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 709: "Your [MAJOR] ability grants you +0.2% Haste, Expertise and Critical Strike for every
/// 4.0% / 2.5% / 1.6% / 1.0% Spirit you have, up to 50% Spirit, for 8 seconds." (effect 3189)
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
    /// Not in the game data; measured from logs: a refresh keeps up to 30% (2.4s) of the time left, so a refreshed buff
    /// lasts up to 10.4s (yMAZVJL9hTpFjqbC fight 7: every refresh with over 2.4s left is removed 10.42s later).
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
