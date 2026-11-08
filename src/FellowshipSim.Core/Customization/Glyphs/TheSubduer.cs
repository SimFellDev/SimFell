namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 704: "You deal more damage to Low Health Enemies. Your [MAJOR] abilities reduce the
/// cooldown of one of your [CONTROL] abilities ..." (effect 3185; the Control cooldowns do not affect damage).
/// UNVERIFIED: no log has this glyph; low health is taken as <see cref="LowHealthThreshold"/> or less.
/// </summary>
public partial class TheSubduer : Glyph
{
    public const double LowHealthThreshold = 0.30;
    /// <summary>
    /// Tooltip: "5% / 10% / 15% / 20% more damage to Low Health Enemies" (the game data's
    /// <see cref="DamageBonusAgainstLowHealthEnemiesByRank"/> has the multipliers, 1.05 for 5%).
    /// </summary>
    public static readonly double[] DamageByRank = [0.05, 0.10, 0.15, 0.20];

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        double multiplier = 1.0 + ForRank(DamageByRank);
        hero.TargetDamageMultipliers.Add((target, _) => target.HealthFraction <= LowHealthThreshold ? multiplier : 1.0);
    }
}
