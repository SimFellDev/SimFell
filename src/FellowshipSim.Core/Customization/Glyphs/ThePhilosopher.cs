namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 709: "Your [MAJOR] ability grants you Haste, Expertise and Critical Strike for every step of Spirit you have
/// (by rank), up to a Spirit cap, for a duration." (effect 3189; <see cref="Constants.IncreasePerSpirit"/>,
/// <see cref="Constants.CurrentSpiritPerIncreaseByRank"/>, <see cref="Constants.SpiritThreshold"/>, <see cref="Constants.Duration"/>)
/// </summary>
public partial class ThePhilosopher : Glyph
{
    public const int BuffEffectId = 3189;
    /// <summary>
    /// Not in the game data; measured from logs: a refresh keeps up to <see cref="RefreshCarryoverFraction"/> of the buff's duration from the time left, so a refreshed
    /// buff outlasts <see cref="Constants.Duration"/> (yMAZVJL9hTpFjqbC fight 7: a refresh with enough time left is removed later than a fresh buff).
    /// </summary>
    public const double RefreshCarryoverFraction = 0.3;

    public static readonly AuraDef Buff = AuraDef.Effect(GlyphName, BuffEffectId, Constants.Duration)
        with { RefreshCarryoverFraction = RefreshCarryoverFraction };

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        double spiritPerStep = ForRank(Constants.CurrentSpiritPerIncreaseByRank);
        // Applied once the MAJOR ability resolves, so its own effects (e.g. Winter's Blessing's Spirit) count.
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability.Category != AbilityCategory.Major) return;
            double spiritPercent = Math.Min(hero.SpiritPercent(now), Constants.SpiritThreshold) * 100.0;
            double bonus = Constants.IncreasePerSpirit * spiritPercent / spiritPerStep;
            hero.Auras.Apply(Buff.WithModifiers(
                StatModifier.Add(StatType.Haste, bonus),
                StatModifier.Add(StatType.Expertise, bonus),
                StatModifier.Add(StatType.CritChance, bonus)), now);
        };
    }
}
